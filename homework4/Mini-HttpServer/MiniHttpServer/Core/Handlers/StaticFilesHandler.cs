using MiniHttpServer.Core.Abstracts;
using MiniHttpServer.Shared;
using System;
using System.Linq;
using System.Net;
using System.Text;

namespace MiniHttpServer.Core.Handlers
{
    internal sealed class StaticFilesHandler : Handler
    {
        public async override void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var isGetMethod = request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase);
            var absolutePath = request.Url.AbsolutePath;


            var isStaticFile = absolutePath.Split('/').Any(seg => seg.Contains('.'));

            if (isGetMethod && isStaticFile)
            {
                var response = context.Response;

                byte[]? buffer = null;
                var path = absolutePath.Trim('/');


                buffer = GetResponseBytes.Invoke(path);


                response.ContentType = ContentType.GetContentType(path);

                if (buffer == null)
                {
                    response.StatusCode = 404;
                    const string notFound = "<html><body>404 - Not Found</body></html>";
                    buffer = Encoding.UTF8.GetBytes(notFound);
                }

                response.ContentLength64 = buffer.Length;

                using var output = response.OutputStream;
                await output.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                await output.FlushAsync().ConfigureAwait(false);

                if (response.StatusCode == 200)
                    Console.WriteLine($"Запрос обработан: {absolutePath} - Status: {response.StatusCode}");
                else
                    Console.WriteLine($"Ошибка запроса: {absolutePath} - Status: {response.StatusCode}");
            }

            else if (Successor != null)
            {
                Successor.HandleRequest(context);
                context.Response.Close();
            }
        }
    }
}
