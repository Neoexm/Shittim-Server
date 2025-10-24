using BlueArchiveAPI.NetworkModels;
using Protocol = Plana.MX.NetworkProtocol.Protocol;

namespace BlueArchiveAPI.Handlers
{
    /// <summary>
    /// Marks a handler class or method with its associated protocol
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class ProtocolHandlerAttribute : Attribute
    {
        public Protocol Protocol { get; }

        public ProtocolHandlerAttribute(Protocol protocol)
        {
            Protocol = protocol;
        }
    }
}