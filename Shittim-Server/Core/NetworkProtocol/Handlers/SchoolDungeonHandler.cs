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

public class SchoolDungeonHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly SchoolDungeonManager _schoolDungeonManager;
    private readonly IMapper _mapper;

    public SchoolDungeonHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        SchoolDungeonManager schoolDungeonManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _schoolDungeonManager = schoolDungeonManager;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.SchoolDungeon_List)]
    public async Task<SchoolDungeonListResponse> List(
        SchaleDataContext db,
        SchoolDungeonListRequest request,
        SchoolDungeonListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.SchoolDungeonStageHistoryDBList = db.GetAccountSchoolDungeonStageHistories(account.ServerId)
            .ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.SchoolDungeon_EnterBattle)]
    public async Task<SchoolDungeonEnterBattleResponse> EnterBattle(
        SchaleDataContext db,
        SchoolDungeonEnterBattleRequest request,
        SchoolDungeonEnterBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ParcelResultDB = await _schoolDungeonManager.SchoolDungeonEnterStage(db, account, request.StageUniqueId);

        return response;
    }

    [ProtocolHandler(Protocol.SchoolDungeon_BattleResult)]
    public async Task<SchoolDungeonBattleResultResponse> BattleResult(
        SchaleDataContext db,
        SchoolDungeonBattleResultRequest request,
        SchoolDungeonBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (historyDb, parcelResultDb) = await _schoolDungeonManager.SchoolDungeonBattleResult(db, account, request);

        response.SchoolDungeonStageHistoryDB = historyDb.ToMap(_mapper);
        response.LevelUpCharacterDBs = new();
        response.ParcelResultDB = parcelResultDb;

        return response;
    }

    [ProtocolHandler(Protocol.SchoolDungeon_Retreat)]
    public async Task<SchoolDungeonRetreatResponse> Retreat(
        SchaleDataContext db,
        SchoolDungeonRetreatRequest request,
        SchoolDungeonRetreatResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ParcelResultDB = await _schoolDungeonManager.SchoolDungeonRetreat(db, account, request.StageUniqueId);

        return response;
    }
}
