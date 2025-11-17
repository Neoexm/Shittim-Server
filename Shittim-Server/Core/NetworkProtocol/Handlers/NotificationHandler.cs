using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class NotificationHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;

    public NotificationHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        ExcelTableService excelService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
    }

    [ProtocolHandler(Protocol.Notification_EventContentReddotCheck)]
    public async Task<NotificationEventContentReddotResponse> EventContentReddotCheck(
        SchaleDataContext db,
        NotificationEventContentReddotRequest request,
        NotificationEventContentReddotResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.Reddots = new();
        response.EventContentUnlockCGDBs = new();

        if (account.GameSettings.EnableMultiFloorRaid)
        {
            var seasonData = _excelService.GetTable<MultiFloorRaidSeasonManageExcelT>()
                .FirstOrDefault(s => s.SeasonId == account.ContentInfo.MultiFloorRaidDataInfo.SeasonId);

            response.ServerTimeTicks = seasonData != null
                ? DateTime.Parse(seasonData.SeasonStartDate).AddDays(2).Ticks
                : account.GameSettings.ServerDateTimeTicks();
        }

        return response;
    }
}
