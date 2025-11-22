using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ManagementHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public ManagementHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.Management_BannerList)]
    public async Task<ManagementBannerListResponse> BannerList(
        SchaleDataContext db,
        ManagementBannerListRequest request,
        ManagementBannerListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.BannerDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Management_ProtocolLockList)]
    public async Task<ManagementProtocolLockListResponse> ProtocolLockList(
        SchaleDataContext db,
        ManagementProtocolLockListRequest request,
        ManagementProtocolLockListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ProtocolLockDBs = [];

        return response;
    }
}
