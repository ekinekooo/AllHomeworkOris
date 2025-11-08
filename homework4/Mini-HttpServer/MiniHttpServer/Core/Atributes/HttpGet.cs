using System;

namespace MiniHttpServer.Core.Atributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class HttpGet : Attribute
    {

        public string Route { get; }

        public HttpGet() : this(string.Empty) { }


        public HttpGet(string route)
        {
            Route = route ?? string.Empty;
        }
    }
}
