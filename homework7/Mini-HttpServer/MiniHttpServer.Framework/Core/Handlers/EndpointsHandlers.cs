using MiniHttpServer.Framework.Core.Abstracts;
using MiniHttpServer.Framework.Core.Atributes;
using MiniHttpServer.Framework.Core.HttpResponse;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MiniHttpServer.Framework.Core.Abstracts
{

    sealed class EndpointsHandlers : Handler
    {
        public override async Task HandleRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var url = request.Url;
                var pathParts = url?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                var endpointName = pathParts.FirstOrDefault();


                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var endpointType = assembly
                    .GetTypes()
                    .FirstOrDefault(t =>
                        t.GetCustomAttribute<EndpointAttribute>() != null &&
                        IsCheckedEndpoint(t.Name, endpointName));

                if (endpointType is null)
                {
                    await WriteResponseAsync(context.Response, "Endpoint not found");
                    return;
                }

                MethodInfo? selectedMethod = null;
                Dictionary<string, string>? selectedRouteValues = null;
                var bestScore = -1;

                foreach (var m in endpointType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {

                    var httpAttr = m.GetCustomAttributes(inherit: true)
                        .FirstOrDefault(a => a.GetType().Name.Equals($"Http{request.HttpMethod}", StringComparison.OrdinalIgnoreCase));
                    if (httpAttr is null) continue;


                    var attrRoute = httpAttr.GetType().GetProperty("Route")?.GetValue(httpAttr)?.ToString()?.Trim('/') ?? string.Empty;


                    var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(attrRoute)) candidates.Add(attrRoute);

                    if (!string.IsNullOrEmpty(endpointName))
                    {
                        if (string.IsNullOrEmpty(attrRoute))
                        {
                            candidates.Add(endpointName);
                        }
                        else
                        {
                            candidates.Add($"{endpointName}/{attrRoute}");
                        }
                    }

                    foreach (var candidate in candidates)
                    {
                        if (TryMatchRoute(candidate, pathParts, out var routeValues))
                        {
                            var score = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;
                            if (score > bestScore)
                            {
                                bestScore = score;
                                selectedMethod = m;
                                selectedRouteValues = routeValues;
                            }
                        }
                    }
                }

                if (selectedMethod is null)
                {
                    await WriteResponseAsync(context.Response, "Endpoint method not found");
                    return;
                }


                string? body = null;
                if (request.HasEntityBody)
                {
                    using var reader = new StreamReader(request.InputStream, request.ContentEncoding, detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);
                    body = await reader.ReadToEndAsync();
                }


                var formParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(body) &&
                    !string.IsNullOrEmpty(request.ContentType) &&
                    request.ContentType.IndexOf("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (var pair in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var kv = pair.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            formParams[WebUtility.UrlDecode(kv[0])] = WebUtility.UrlDecode(kv[1]);
                        }
                    }
                }


                var queryParams = ParseQueryString(url?.Query);


                JsonDocument? jsonDoc = null;
                if (!string.IsNullOrEmpty(body) &&
                    !string.IsNullOrEmpty(request.ContentType) &&
                    request.ContentType.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    try
                    {
                        jsonDoc = JsonDocument.Parse(body);
                    }
                    catch (Exception jex)
                    {
                        Console.WriteLine("JSON parse error: " + jex);
                    }
                }


                var allParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (selectedRouteValues is not null)
                    foreach (var kv in selectedRouteValues) allParams[kv.Key] = kv.Value;
                foreach (var kv in queryParams) allParams[kv.Key] = kv.Value;
                foreach (var kv in formParams) allParams[kv.Key] = kv.Value;


                var endpointInstance = Activator.CreateInstance(endpointType);
                if (endpointInstance is EndpointBase baseEndpoint)
                    baseEndpoint.SetContext(context);


                var paramInfos = selectedMethod.GetParameters();
                var invokeArgs = new object?[paramInfos.Length];
                var complexParams = paramInfos.Where(p => !IsSimpleType(p.ParameterType)).ToArray();

                for (int i = 0; i < paramInfos.Length; i++)
                {
                    var p = paramInfos[i];


                    if (allParams.TryGetValue(p.Name!, out var strVal))
                    {
                        invokeArgs[i] = TryConvert(strVal, p.ParameterType, out var converted)
                            ? converted
                            : GetDefault(p.ParameterType);
                        continue;
                    }

                    if (jsonDoc is not null &&
                        IsSimpleType(p.ParameterType) &&
                        jsonDoc.RootElement.ValueKind == JsonValueKind.Object &&
                        jsonDoc.RootElement.TryGetProperty(p.Name!, out var propEl) &&
                        ConvertJsonElement(propEl, p.ParameterType, out var simpleConverted))
                    {
                        invokeArgs[i] = simpleConverted;
                        continue;
                    }


                    if (jsonDoc is not null && !IsSimpleType(p.ParameterType))
                    {
                        try
                        {
                            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object &&
                                jsonDoc.RootElement.TryGetProperty(p.Name!, out var complexEl))
                            {
                                var propJson = complexEl.GetRawText();
                                invokeArgs[i] = JsonSerializer.Deserialize(propJson, p.ParameterType, JsonOpts);
                                continue;
                            }

                            if (complexParams.Length == 1 && !string.IsNullOrEmpty(body))
                            {
                                invokeArgs[i] = JsonSerializer.Deserialize(body, p.ParameterType, JsonOpts);
                                continue;
                            }
                        }
                        catch (Exception dex)
                        {
                            Console.WriteLine($"JSON->type conversion error for param '{p.Name}': {dex}");
                            invokeArgs[i] = GetDefault(p.ParameterType);
                            continue;
                        }
                    }

                    invokeArgs[i] = GetDefault(p.ParameterType);
                }

                var result = selectedMethod.Invoke(endpointInstance, invokeArgs);

                if (result is Task task)
                {
                    await task;
                    var resultProp = task.GetType().GetProperty("Result");
                    result = resultProp?.GetValue(task);
                }

                await EndpointResponseHandler.HandleResultAsync(context, result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Handler exception: " + ex);
                try
                {
                    if (context?.Response?.OutputStream?.CanWrite == true)
                    {
                        context.Response.StatusCode = 500;
                        await WriteResponseAsync(context.Response, $"Внутренняя ошибка сервера: {ex.Message}\n{ex.StackTrace}");
                    }
                }
                catch { /* поток уже мог закрыться */ }
            }
        }


        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        private static bool TryMatchRoute(string template, string[] actualParts, out Dictionary<string, string> routeValues)
        {
            routeValues = new(StringComparer.OrdinalIgnoreCase);
            var tplParts = (template ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (tplParts.Length == 0 || actualParts.Length == 0) return false;
            if (tplParts.Length > actualParts.Length) return false;

            for (int i = 0; i < tplParts.Length; i++)
            {
                var tpl = tplParts[i];
                var actual = actualParts.ElementAtOrDefault(i);

                if (tpl.StartsWith("{") && tpl.EndsWith("}"))
                {
                    var name = tpl[1..^1];
                    if (name.StartsWith("*"))
                    {
                        name = name[1..];
                        var rest = string.Join('/', actualParts.Skip(i));
                        routeValues[name] = WebUtility.UrlDecode(rest ?? string.Empty);
                        return true;
                    }
                    routeValues[name] = WebUtility.UrlDecode(actual ?? string.Empty);
                }
                else
                {
                    if (!string.Equals(tpl, actual, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }

            if (actualParts.Length > tplParts.Length)
            {
                var tail = string.Join('/', actualParts.Skip(tplParts.Length));
                routeValues["__tail"] = WebUtility.UrlDecode(tail);
            }

            return true;
        }

        private static Dictionary<string, string> ParseQueryString(string? rawQuery)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(rawQuery)) return dict;

            var q = rawQuery[0] == '?' ? rawQuery[1..] : rawQuery;
            foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2)
                    dict[WebUtility.UrlDecode(kv[0])] = WebUtility.UrlDecode(kv[1]);
                else
                    dict[WebUtility.UrlDecode(kv[0])] = string.Empty;
            }
            return dict;
        }

        private static bool IsSimpleType(Type t)
        {
            var u = Nullable.GetUnderlyingType(t) ?? t;
            return u.IsPrimitive
                || u.IsEnum
                || u == typeof(string)
                || u == typeof(decimal)
                || u == typeof(DateTime)
                || u == typeof(Guid);
        }

        private static bool TryConvert(string str, Type targetType, out object? result)
        {
            result = null;

            if (targetType == typeof(string))
            {
                result = str;
                return true;
            }

            var u = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                if (u.IsEnum)
                {
                    result = Enum.Parse(u, str, ignoreCase: true);
                    return true;
                }

                if (u == typeof(Guid))
                {
                    if (Guid.TryParse(str, out var g)) { result = g; return true; }
                    return false;
                }

                if (u == typeof(DateTime))
                {
                    if (DateTime.TryParse(str, out var dt)) { result = dt; return true; }
                    return false;
                }

                result = Convert.ChangeType(str, u);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ConvertJsonElement(JsonElement el, Type targetType, out object? converted)
        {
            converted = null;

            if (targetType == typeof(string))
            {
                converted = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
                return true;
            }

            var candidate = el.ValueKind == JsonValueKind.String ? el.GetString() ?? string.Empty : el.GetRawText();
            return TryConvert(candidate.Trim('"'), targetType, out converted);
        }

        private static object? GetDefault(Type t) =>
            t.IsValueType ? Activator.CreateInstance(t) : null;

        private static bool IsCheckedEndpoint(string className, string? endpointName) =>
             !string.IsNullOrEmpty(endpointName) &&
            (className.Equals(endpointName, StringComparison.OrdinalIgnoreCase)
             || className.Equals($"{endpointName}Endpoint", StringComparison.OrdinalIgnoreCase));

        private static async Task WriteResponseAsync(HttpListenerResponse response, string content)
        {
            var buffer = Encoding.UTF8.GetBytes(content ?? string.Empty);
            response.ContentLength64 = buffer.LongLength;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            await response.OutputStream.FlushAsync();
        }
    }
}
