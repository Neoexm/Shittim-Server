using BlueArchiveAPI;
using Schale.MX.NetworkProtocol;
using System.Text;

namespace Shittim_Server.Core;

public class HandlerManager
{
    private readonly IProtocolHandlerRegistry _registry;
    private readonly ILogger<HandlerManager> _logger;

    public HandlerManager(IProtocolHandlerRegistry registry, ILogger<HandlerManager> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public void Initialize()
    {
        _logger.LogInformation("HandlerManager initialized");
    }

    public HandlerLease GetHandlerLease(Protocol protocol)
    {
        var handler = _registry.GetHandler(protocol);
        if (handler == null)
        {
            return new HandlerLease(null, protocol, _registry, _logger);
        }
        return new HandlerLease(handler, protocol, _registry, _logger);
    }

    public Type? GetRequestType(Protocol protocol)
    {
        return _registry.GetRequestType(protocol);
    }
}

public readonly struct HandlerLease : IDisposable
{
    private readonly System.Reflection.MethodInfo? _handler;
    private readonly Protocol _protocol;
    private readonly IProtocolHandlerRegistry _registry;
    private readonly ILogger _logger;

    public bool IsValid => _handler != null;
    public IProtocolHandler Handler { get; }

    internal HandlerLease(System.Reflection.MethodInfo? handler, Protocol protocol, IProtocolHandlerRegistry registry, ILogger logger)
    {
        _handler = handler;
        _protocol = protocol;
        _registry = registry;
        _logger = logger;
        Handler = new ProtocolHandlerWrapper(protocol, registry, logger);
    }

    public void Dispose()
    {
    }

    private class ProtocolHandlerWrapper : IProtocolHandler
    {
        private readonly Protocol _protocol;
        private readonly IProtocolHandlerRegistry _registry;
        private readonly ILogger _logger;

        public ProtocolHandlerWrapper(Protocol protocol, IProtocolHandlerRegistry registry, ILogger logger)
        {
            _protocol = protocol;
            _registry = registry;
            _logger = logger;
        }

        public async Task<ResponsePacket?> Handle(RequestPacket request)
        {
            try
            {
                var response = await _registry.InvokeHandler(_protocol, request);
                if (response == null)
                {
                    throw new InvalidOperationException($"Handler returned null for protocol {_protocol}");
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling protocol {Protocol}", _protocol);
                throw;
            }
        }
    }
}

public interface IProtocolHandler
{
    Task<ResponsePacket?> Handle(RequestPacket request);
}
