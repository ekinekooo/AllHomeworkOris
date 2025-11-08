using System;

namespace MiniHttpServer.Core.Atributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class HttpPost : Attribute
    {

        public string Route { get; }
        public HttpPost() : this(string.Empty) { }
        public HttpPost(string route)
        {
            Route = route ?? string.Empty;
        }
    }
}
