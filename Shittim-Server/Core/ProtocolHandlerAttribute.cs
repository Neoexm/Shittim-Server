using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Core;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class ProtocolHandlerAttribute : Attribute
{
    public Protocol Protocol { get; }
    public string? Description { get; set; }

    public ProtocolHandlerAttribute(Protocol protocol)
    {
        Protocol = protocol;
    }
}
