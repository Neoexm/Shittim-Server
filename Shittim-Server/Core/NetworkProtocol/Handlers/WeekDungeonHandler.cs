using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class WeekDungeonHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly WeekDungeonManager _weekDungeonManager;
    private readonly IMapper _mapper;

    public WeekDungeonHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        WeekDungeonManager weekDungeonManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _weekDungeonManager = weekDungeonManager;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.WeekDungeon_List)]
    public async Task<WeekDungeonListResponse> List(
        SchaleDataContext db,
        WeekDungeonListRequest request,
        WeekDungeonListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.AdditionalStageIdList = [];
        response.WeekDungeonStageHistoryDBList = db.GetAccountWeekDungeonStageHistories(account.ServerId)
            .ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.WeekDungeon_EnterBattle)]
    public async Task<WeekDungeonEnterBattleResponse> EnterBattle(
        SchaleDataContext db,
        WeekDungeonEnterBattleRequest request,
        WeekDungeonEnterBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ParcelResultDB = await _weekDungeonManager.WeekDungeonEnterStage(db, account, request.StageUniqueId);

        return response;
    }

    [ProtocolHandler(Protocol.WeekDungeon_BattleResult)]
    public async Task<WeekDungeonBattleResultResponse> BattleResult(
        SchaleDataContext db,
        WeekDungeonBattleResultRequest request,
        WeekDungeonBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (historyDb, parcelResultDb) = await _weekDungeonManager.WeekDungeonBattleResult(db, account, request);

        response.WeekDungeonStageHistoryDB = historyDb.ToMap(_mapper);
        response.LevelUpCharacterDBs = new();
        response.ParcelResultDB = parcelResultDb;

        return response;
    }

    [ProtocolHandler(Protocol.WeekDungeon_Retreat)]
    public async Task<WeekDungeonRetreatResponse> Retreat(
        SchaleDataContext db,
        WeekDungeonRetreatRequest request,
        WeekDungeonRetreatResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ParcelResultDB = await _weekDungeonManager.WeekDungeonRetreat(db, account, request.StageUniqueId);

        return response;
    }
}
