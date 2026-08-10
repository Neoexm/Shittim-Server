using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.Data;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ManagementHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;

    public ManagementHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
    }

    [ProtocolHandler(Protocol.Management_BannerList)]
    public async Task<ManagementBannerListResponse> BannerList(
        SchaleDataContext db,
        ManagementBannerListRequest request,
        ManagementBannerListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var server = Config.Instance.ServerConfiguration;
        var banners = new List<BannerDB>();

        if (server.KoyukiIncident)
        {
            // OpenWebView is the one banner type with no content behind it, so the tap goes straight to WebViewUrl instead of resolving a stage/shop/raid id.
            banners.Add(new BannerDB
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
            });
        }

        // An interactive world raid has no door of its own: UILobbyEventScrollElement is the only thing that starts InteractiveWorldRaidTask and it takes the season off LinkedLobbyBannerId, so a season nobody sends a banner for is live and unreachable. Classic seasons are entered from the raid menu and WorldRaid is not one of the types the banner element resolves season info for, so they stay out of this list. Exposed..extension rather than the playable window, so the banner is up for exactly as long as the season rows patched into ExcelDB say the event is activated.
        var raid = WorldRaidService.Manifest;
        if (raid != null
            && _excelService.GetTable<InteractiveWorldRaidSeasonManageExcelT>().Any(x => x.SeasonId == raid.seasonId)
            && WorldRaidService.TryLocal(string.IsNullOrEmpty(raid.exposed) ? raid.open : raid.exposed, out var raidFrom)
            && WorldRaidService.TryLocal(string.IsNullOrEmpty(raid.extension) ? raid.close : raid.extension, out var raidUntil))
        {
            banners.Add(new BannerDB
            {
                BannerOrder = 2,
                StartDate = raidFrom,
                EndDate = raidUntil,
                Url = $"http://{server.HostAddress}:{server.HostPort}/banner/",
                FileName = $"Event_Banner_{raid.seasonId}.png",
                LinkedLobbyBannerId = (int)raid.seasonId,
                BannerType = EventContentType.InteractiveWorldRaid,
                BannerDisplayType = BannerDisplayType.Lobby
            });
        }

        response.BannerDBs = banners;

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
