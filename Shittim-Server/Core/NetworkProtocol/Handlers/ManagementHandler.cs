using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.Data;
using Schale.MX.GameLogic.DBModel;
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

        var server = Config.Instance.ServerConfiguration;

        // OpenWebView is the one banner type with no content behind it, so the tap goes straight to WebViewUrl instead of resolving a stage/shop/raid id.
        response.BannerDBs =
        [
            new BannerDB
            {
                BannerOrder = 1,
                StartDate = DateTime.Now.AddYears(-1),
                EndDate = DateTime.Now.AddYears(1),
                Url = $"http://{server.HostAddress}:{server.HostPort}/banner/",
                FileName = "koyuki.png",
                WebViewTitle = "nihahahaha",
                WebViewUrl = "https://zerofps-hk.github.io/koyuki-clicker/",
                BannerType = EventContentType.OpenWebView,
                BannerDisplayType = BannerDisplayType.Lobby
            }
        ];

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
