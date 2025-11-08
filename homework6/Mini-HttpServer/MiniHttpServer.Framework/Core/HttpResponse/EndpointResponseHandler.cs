using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiniHttpServer.Framework.Core.HttpResponse
{
    public static class EndpointResponseHandler
    {
        public static async Task HandleResultAsync(HttpListenerContext context, object? result)
        {
            try
            {

                if (result is IActionResult actionResult)
                {
                    await actionResult.ExecuteAsync(context);
                    return;
                }


                if (result is string text)
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    await WriteResponseAsync(context.Response, text);
                    return;
                }

                if (result is byte[] bytes)
                {
                    var response = context.Response;
                    response.ContentType = "application/octet-stream";
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.ContentLength64 = bytes.Length;


                    if (response.OutputStream?.CanWrite == true)
                    {
                        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                        await response.OutputStream.FlushAsync();
                    }
                    return;
                }

                if (result is null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    return;
                }

                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.StatusCode = (int)HttpStatusCode.OK;

                var payload = JsonSerializer.Serialize(new { data = result });
                await WriteResponseAsync(context.Response, payload);
            }
            catch (Exception ex)
            {
                context.Response.ContentType = "text/plain; charset=utf-8";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await WriteResponseAsync(context.Response, $"Внутренняя ошибка сервера: {ex.Message}");
            }
        }

        private static async Task WriteResponseAsync(HttpListenerResponse response, string content)
        {

            var buffer = Encoding.UTF8.GetBytes(content ?? string.Empty);
            response.ContentLength64 = buffer.Length;

            if (response.OutputStream?.CanWrite == true)
            {
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                await response.OutputStream.FlushAsync();
            }
        }
    }
}
