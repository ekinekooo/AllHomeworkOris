using System.Net;
using System.Text;
using MiniTemplateEngine;

namespace MiniHttpServer.Framework.Core.HttpResponse
{

    public sealed class PageResult : IActionResult
    {
        private readonly string _pathTemplate;
        private readonly object? _data;
        private readonly int _statusCode;

        public PageResult(string pathTemplate, object? data, int statusCode = 200)
        {
            _pathTemplate = pathTemplate;
            _data = data;
            _statusCode = statusCode;
        }

        public async Task ExecuteAsync(HttpListenerContext context)
        {

            IHtmlTemplateRenderer renderer = new HtmlTemplateRenderer();

            string html = renderer.RenderFromFile(_pathTemplate, _data ?? new { });

            byte[] payload = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.StatusCode = _statusCode;
            context.Response.ContentLength64 = payload.Length;

            await context.Response.OutputStream.WriteAsync(payload, 0, payload.Length);
            await context.Response.OutputStream.FlushAsync();
        }
    }
}
