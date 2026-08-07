using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class PermanentRaidHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;

    public PermanentRaidHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
    }

    [ProtocolHandler(Protocol.PermanentRaid_Lobby)]
    public async Task<PermanentRaidLobbyResponse> Lobby(
        SchaleDataContext db,
        PermanentRaidLobbyRequest request,
        PermanentRaidLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // MaxValue lock dates read as "the lock never starts", so every managed group stays open.
        response.BossManageDBs = _excelService.GetTable<PermanentRaidManageExcelT>()
            .Select(x => new PermanentRaidBossManageDB { GroupType = x.Type, LockStartDate = DateTime.MaxValue, LockEndDate = DateTime.MaxValue })
            .ToList();
        response.BestScoreHistoryDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.PermanentRaid_EnterBattle)]
    public async Task<PermanentRaidEnterBattleResponse> EnterBattle(
        SchaleDataContext db,
        PermanentRaidEnterBattleRequest request,
        PermanentRaidEnterBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var now = account.GameSettings.ServerDateTime();

        response.BattleHistoryDB = new PermanentRaidBattleHistoryDB
        {
            StageId = request.StageId,
            StartDate = now,
            EndDate = DateTime.MaxValue,
            Status = RaidStatus.Playing
        };

        if (request.AssistUseInfo != null)
        {
            response.AssistCharacterDB = new AssistCharacterDB
            {
                AccountId = account.ServerId,
                ServerId = request.AssistUseInfo.CharacterDBId,
                UniqueId = 10000,
                SlotNumber = 1,
                Level = 1,
                StarGrade = 3,
                CombatStyleIndex = request.AssistUseInfo.CombatStyleIndex,
                IsMulligan = request.AssistUseInfo.IsMulligan,
                IsTSAInteraction = request.AssistUseInfo.IsTSAInteraction
            };
        }

        return response;
    }

    [ProtocolHandler(Protocol.PermanentRaid_EndBattle)]
    public async Task<PermanentRaidEndBattleResponse> EndBattle(
        SchaleDataContext db,
        PermanentRaidEndBattleRequest request,
        PermanentRaidEndBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var now = account.GameSettings.ServerDateTime();

        // Nothing permanent-raid-shaped is persisted yet, so no score is banked and the history just closes out the battle the client reported.
        response.ScoreInfo = new RaidScoreInfo();
        response.BattleHistoryDB = new PermanentRaidBattleHistoryDB
        {
            StartDate = now,
            EndDate = now,
            Status = RaidStatus.Close
        };

        return response;
    }

    [ProtocolHandler(Protocol.PermanentRaid_GiveUp)]
    public async Task<PermanentRaidGiveUpResponse> GiveUp(
        SchaleDataContext db,
        PermanentRaidGiveUpRequest request,
        PermanentRaidGiveUpResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }
}
