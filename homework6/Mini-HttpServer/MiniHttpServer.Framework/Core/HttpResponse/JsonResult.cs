using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace MiniHttpServer.Framework.Core.HttpResponse
{

    public sealed class JsonResult : IActionResult
    {
        public object? Data { get; }
        public int StatusCode { get; }

        public JsonResult(object? data, int statusCode = 200)
        {
            Data = data;
            StatusCode = statusCode;
        }

        public async Task ExecuteAsync(HttpListenerContext context)
        {

            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.StatusCode = StatusCode;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(Data, options);
            var payload = Encoding.UTF8.GetBytes(json);

            context.Response.ContentLength64 = payload.Length;
            await context.Response.OutputStream.WriteAsync(payload, 0, payload.Length);
            await context.Response.OutputStream.FlushAsync();
        }
    }
}
