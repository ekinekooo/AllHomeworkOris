using System.Net;
using System.Threading.Tasks;

namespace MiniHttpServer.Framework.Core.Abstracts
{
    internal abstract class Handler
    {
        public Handler? Successor { get; set; }

        protected Handler(Handler? successor = null) => Successor = successor;
        public abstract Task HandleRequest(HttpListenerContext context);
        protected Task NextAsync(HttpListenerContext context) =>
            Successor is null ? Task.CompletedTask : Successor.HandleRequest(context);
        public Handler SetNext(Handler next)
        {
            Successor = next;
            return next;
        }
    }
}
