using System.Net;

namespace MiniHttpServer.Core.Abstracts
{
    abstract class Handler
    {

        public Handler? Successor { get; set; }
        public Handler SetNext(Handler next)
        {
            Successor = next;
            return next;
        }
        protected void PassToSuccessor(HttpListenerContext context) =>
            Successor?.HandleRequest(context);

        public abstract void HandleRequest(HttpListenerContext context);
    }
}
