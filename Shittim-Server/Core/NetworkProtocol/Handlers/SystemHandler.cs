using BlueArchiveAPI.Configuration;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class SystemHandler : ProtocolHandlerBase
{
    public SystemHandler(IProtocolHandlerRegistry registry) : base(registry)
    {
    }

}
