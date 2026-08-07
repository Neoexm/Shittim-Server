using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class NotificationHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly MailManager _mailManager;

    public NotificationHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        MailManager mailManager) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mailManager = mailManager;
    }

    [ProtocolHandler(Protocol.Notification_LobbyCheck)]
    public async Task<NotificationLobbyCheckResponse> LobbyCheck(
        SchaleDataContext db,
        NotificationLobbyCheckRequest request,
        NotificationLobbyCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.UnreadMailCount = await _mailManager.GetUnreadMailCount(account);
        response.EventRewardIncreaseDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Notification_EventContentReddotCheck)]
    public async Task<NotificationEventContentReddotResponse> EventContentReddotCheck(
        SchaleDataContext db,
        NotificationEventContentReddotRequest request,
        NotificationEventContentReddotResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.Reddots = new();
        // Official responses carry Reddots: {} but never EventContentUnlockCGDBs (omitted, not {}).
        if (account.GameSettings.EnableMultiFloorRaid)
            response.ServerTimeTicks = MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks;

        return response;
    }
}
