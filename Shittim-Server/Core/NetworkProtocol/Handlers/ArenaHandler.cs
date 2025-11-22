using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ArenaHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;

    public ArenaHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.Arena_EnterLobby)]
    public async Task<ArenaEnterLobbyResponse> EnterLobby(
        SchaleDataContext db,
        ArenaEnterLobbyRequest request,
        ArenaEnterLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ArenaPlayerInfoDB = new ArenaPlayerInfoDB
        {
            CurrentSeasonId = 1,
            PlayerGroupId = 1,
            CurrentRank = 1,
            SeasonRecord = 1,
            AllTimeRecord = 1
        };
        response.OpponentUserDBs = ArenaService.CreateOpponents(db, account, _mapper);
        response.MapId = 1001;

        return response;
    }

    [ProtocolHandler(Protocol.Arena_OpponentList)]
    public async Task<ArenaOpponentListResponse> OpponentList(
        SchaleDataContext db,
        ArenaOpponentListRequest request,
        ArenaOpponentListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.PlayerRank = 1;
        response.OpponentUserDBs = ArenaService.CreateOpponents(db, account, _mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Arena_SyncEchelonSettingTime)]
    public async Task<ArenaSyncEchelonSettingTimeResponse> SyncEchelonSettingTime(
        SchaleDataContext db,
        ArenaSyncEchelonSettingTimeRequest request,
        ArenaSyncEchelonSettingTimeResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.EchelonSettingTime = account.GameSettings.ServerDateTime();

        return response;
    }

    [ProtocolHandler(Protocol.Arena_EnterBattlePart1)]
    public async Task<ArenaEnterBattlePart1Response> EnterBattlePart1(
        SchaleDataContext db,
        ArenaEnterBattlePart1Request request,
        ArenaEnterBattlePart1Response response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var attackingUser = new ArenaUserDB
        {
            AccountServerId = account.ServerId,
            NickName = account.Nickname ?? "Player",
            Rank = 1,
            Level = account.Level,
            RepresentCharacterUniqueId = account.RepresentCharacterServerId,
            AccountAttachmentDB = db.GetAccountAttachments(account.ServerId).FirstMapTo(_mapper),
            TeamSettingDB = ArenaService.CreateArenaTeamSetting(db, account, _mapper, false)
        };

        var opponentAccount = await db.Accounts.FirstOrDefaultAsync(x => x.ServerId == request.OpponentAccountServerId);
        
        ArenaUserDB defendingUser;
        if (opponentAccount != null)
        {
            defendingUser = ArenaService.CreateArenaUser(
                opponentAccount.ServerId,
                opponentAccount.RepresentCharacterServerId,
                opponentAccount.Nickname ?? "Opponent",
                request.OpponentRank,
                opponentAccount.Level,
                ArenaService.CreateArenaTeamSetting(db, opponentAccount, _mapper, true));
        }
        else
        {
            defendingUser = ArenaService.CreateArenaUser(
                request.OpponentAccountServerId,
                10065,
                "Dummy Opponent",
                request.OpponentRank,
                90,
                ArenaService.DummyTeamFormation);
        }

        response.ArenaBattleDB = new ArenaBattleDB
        {
            Season = 1,
            Group = 1,
            BattleStartTime = account.GameSettings.ServerDateTime(),
            Seed = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AttackingUserDB = attackingUser,
            DefendingUserDB = defendingUser
        };

        return response;
    }

    [ProtocolHandler(Protocol.Arena_EnterBattlePart2)]
    public async Task<ArenaEnterBattlePart2Response> EnterBattlePart2(
        SchaleDataContext db,
        ArenaEnterBattlePart2Request request,
        ArenaEnterBattlePart2Response response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ArenaBattleDB = request.ArenaBattleDB;
        response.ArenaPlayerInfoDB = new ArenaPlayerInfoDB
        {
            CurrentSeasonId = 1,
            PlayerGroupId = 1,
            CurrentRank = 1,
            SeasonRecord = 1,
            AllTimeRecord = 1
        };

        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper);

        return response;
    }
}
