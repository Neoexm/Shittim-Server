using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class NotificationHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;

    public NotificationHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
    }

    [ProtocolHandler(Protocol.Notification_LobbyCheck)]
    public async Task<NotificationLobbyCheckResponse> LobbyCheck(
        SchaleDataContext db,
        NotificationLobbyCheckRequest request,
        NotificationLobbyCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // same unclaimed-unexpired mailbox count Mail_Check reports
        response.UnreadMailCount = db.GetAccountMailbox(account.ServerId, account.GameSettings.ServerDateTime()).Count();
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
