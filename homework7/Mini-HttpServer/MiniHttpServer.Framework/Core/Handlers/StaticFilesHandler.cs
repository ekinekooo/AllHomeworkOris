using MiniHttpServer.Framework.Core.Abstracts;
using MiniHttpServer.Framework.Shared;
using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MiniHttpServer.Framework.Core.Handlers
{
    class StaticFilesHandler : Handler
    {
        public override async Task HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;

            var isGetMethod = request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase);
            var isStaticFile = request.Url?.AbsolutePath.Split('/').Any(x => x.Contains('.')) == true;

            if (isGetMethod && isStaticFile)
            {
                var response = context.Response;


                var path = (request.Url?.AbsolutePath ?? string.Empty).Trim('/');


                var buffer = GetResponseBytes.Invoke(path);


                response.ContentType = ContentType.GetContentType(path);

                if (buffer == null)
                {
                    response.StatusCode = 404;
                    var errorHtml = "<html><body>404 - Not Found</body></html>";
                    buffer = Encoding.UTF8.GetBytes(errorHtml);
                }

                response.ContentLength64 = buffer.Length;

                using var output = response.OutputStream;
                await output.WriteAsync(buffer, 0, buffer.Length);
                await output.FlushAsync();

                if (response.StatusCode == 200)
                    Console.WriteLine($"Запрос обработан: {request.Url?.AbsolutePath} - Status: {response.StatusCode}");
                else
                    Console.WriteLine($"Ошибка запроса: {request.Url?.AbsolutePath} - Status: {response.StatusCode}");
            }
            else if (Successor != null)
            {

                await Successor.HandleRequest(context);
                context.Response.Close();
            }
        }
    }
}
