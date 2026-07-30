using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Core;

public class HandlerManager
{
    private readonly IProtocolHandlerRegistry _registry;

    public HandlerManager(IProtocolHandlerRegistry registry)
    {
        _registry = registry;
    }

    public bool IsImplemented(Protocol protocol) => _registry.GetHandler(protocol) != null;

    public Type? GetRequestType(Protocol protocol) => _registry.GetRequestType(protocol);

    public Task<ResponsePacket?> Dispatch(Protocol protocol, RequestPacket request) =>
        _registry.InvokeHandler(protocol, request);
}
