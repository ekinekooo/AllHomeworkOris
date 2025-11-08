using System;

namespace MiniHttpServer.Framework.Core.Abstracts
{

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HttpGet : Attribute
    {

        public string? Route { get; }

        public HttpGet(string route)
        {
            Route = route;
        }
        public HttpGet() { }
    }
}
