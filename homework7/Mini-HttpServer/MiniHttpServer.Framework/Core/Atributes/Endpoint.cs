using System;

namespace MiniHttpServer.Framework.Core.Abstracts
{

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class EndpointAttribute : Attribute
    {
        public EndpointAttribute() { }
    }
}
