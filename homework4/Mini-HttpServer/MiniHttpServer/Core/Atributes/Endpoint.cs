using System;

namespace MiniHttpServer.Core.Atributes
{

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class EndpointAttribute : Attribute
    {

        public string Name { get; }

        public EndpointAttribute() : this(string.Empty) { }

        public EndpointAttribute(string name)
        {
            Name = name ?? string.Empty;
        }
    }
}
