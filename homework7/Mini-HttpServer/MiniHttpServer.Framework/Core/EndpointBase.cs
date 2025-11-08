using System.Net;
using MiniHttpServer.Framework.Core.HttpResponse;

namespace MiniHttpServer.Framework.Core
{

    public abstract class EndpointBase
    {

        protected HttpListenerContext Context { get; private set; } = default!;

        internal void SetContext(HttpListenerContext context)
        {

            if (context is null) throw new ArgumentNullException(nameof(context));
            Context = context;
        }

        protected IActionResult Page(string pathTemplate, object data) =>
            new PageResult(pathTemplate, data);

        protected IActionResult Json(object data) =>
            new JsonResult(data);

        protected IActionResult Page(string pathTemplate, object data, int statusCode) =>
            new PageResult(pathTemplate, data, statusCode);

        protected IActionResult Json(object data, int statusCode) =>
            new JsonResult(data, statusCode);
    }
}
