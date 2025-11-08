using System;

namespace MiniHttpServer.Framework.Core.Abstracts
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class HttpPost : Attribute
    {

        public string? Route { get; }

        public HttpPost(string route)
        {
            Route = route;
        }


        public HttpPost() { }
    }
}
