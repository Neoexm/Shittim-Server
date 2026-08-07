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

    [ProtocolHandler(Protocol.System_Version)]
    public Task<SystemVersionResponse> Version(
        SchaleDataContext db,
        SystemVersionRequest request,
        SystemVersionResponse response)
    {
        // Same answers Account_Auth gives: the server's data-build number, no minimum, not a dev build.
        response.CurrentVersion = Config.Instance.ServerConfiguration.AuthCurrentVersion;
        response.MinimumVersion = 0;
        response.IsDevelopment = false;

        return Task.FromResult(response);
    }
}
