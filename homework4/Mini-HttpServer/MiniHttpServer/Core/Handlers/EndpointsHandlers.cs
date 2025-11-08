using MiniHttpServer.Core.Abstracts;
using MiniHttpServer.Core.Atributes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;

namespace MiniHttpServer.Core.Handlers
{
    internal sealed class EndpointsHandlers : Handler
    {
        public override void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var absolutePath = request.Url?.AbsolutePath ?? string.Empty;


            var parts = absolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var endpointName = parts.FirstOrDefault();


            if (string.IsNullOrWhiteSpace(endpointName))
            {
                Successor?.HandleRequest(context);
                return;
            }


            var assembly = Assembly.GetExecutingAssembly();
            var endpointType = assembly
                .GetTypes()
                .Where(t => t.GetCustomAttribute<EndpointAttribute>() != null)
                .FirstOrDefault(t => IsCheckedEndpoint(t.Name, endpointName));

            if (endpointType == null)
            {
                Successor?.HandleRequest(context);
                return;
            }

            var httpMethodName = $"Http{request.HttpMethod}";
            var targetMethod = endpointType
                .GetMethods()
                .FirstOrDefault(m => m
                    .GetCustomAttributes(inherit: true)
                    .Any(attr => attr.GetType().Name.Equals(httpMethodName, StringComparison.OrdinalIgnoreCase)));

            if (targetMethod == null)
            {
                Successor?.HandleRequest(context);
                return;
            }


            var body = string.Empty;
            if (request.HasEntityBody)
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                body = reader.ReadToEnd();
            }

            var postParams = ParseFormUrlEncoded(body);


            var methodParameters = targetMethod
                .GetParameters()
                .Select(p => postParams.TryGetValue(p.Name ?? string.Empty, out var val) ? val : null)
                .ToArray();


            var instance = Activator.CreateInstance(endpointType);
            _ = targetMethod.Invoke(instance, methodParameters);
        }

        private static Dictionary<string, string> ParseFormUrlEncoded(string body)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(body)) return dict;

            var pairs = body.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kv = pair.Split('=', 2, StringSplitOptions.None);

                var key = WebUtility.UrlDecode(kv[0]);
                var val = kv.Length > 1 ? WebUtility.UrlDecode(kv[1]) : string.Empty;

                if (!string.IsNullOrEmpty(key))
                    dict[key] = val;
            }

            return dict;
        }

        private static bool IsCheckedEndpoint(string className, string endpointName) =>
            className.Equals(endpointName, StringComparison.OrdinalIgnoreCase) ||
            className.Equals($"{endpointName}Endpoint", StringComparison.OrdinalIgnoreCase);
    }
}
