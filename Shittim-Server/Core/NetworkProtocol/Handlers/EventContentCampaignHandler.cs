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

public class EventContentCampaignHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;
    private readonly EventContentCampaignManager _eventContentCampaignManager;
    private readonly IMapper _mapper;

    public EventContentCampaignHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        EventContentCampaignManager eventContentCampaignManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _eventContentCampaignManager = eventContentCampaignManager;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.EventContent_AdventureList)]
    public async Task<EventContentAdventureListResponse> AdventureList(
        SchaleDataContext db,
        EventContentAdventureListRequest request,
        EventContentAdventureListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageHistories = db.GetAccountCampaignStageHistories(account.ServerId).ToList();

        response.StageHistoryDBs = _mapper.Map<List<CampaignStageHistoryDB>>(stageHistories);

        var strategyHistories = db.GetAccountStrategyObjectHistories(account.ServerId).ToList();

        response.StrategyObjecthistoryDBs = _mapper.Map<List<StrategyObjectHistoryDB>>(strategyHistories);
        response.AlreadyReceiveRewardId = [];
        response.EventContentBonusRewardDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_EnterStoryStage)]
    public async Task<EventContentEnterStoryStageResponse> EnterStoryStage(
        SchaleDataContext db,
        EventContentEnterStoryStageRequest request,
        EventContentEnterStoryStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (parcelInfos, parcelResult) = await _eventContentCampaignManager.EventContentEnterStage(
            db, account, request.StageUniqueId);

        response.ParcelResultDB = parcelResult;
        response.SaveDataDB = new()
        {
            AccountServerId = account.ServerId,
            CreateTime = account.GameSettings.ServerDateTime(),
            StageUniqueId = request.StageUniqueId,
            StageEntranceFee = parcelInfos,
            EnemyKillCountByUniqueId = new()
        };

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_StoryStageResult)]
    public async Task<EventContentStoryStageResultResponse> StoryStageResult(
        SchaleDataContext db,
        EventContentStoryStageResultRequest request,
        EventContentStoryStageResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (historyDb, parcelResultDb, existingMission) = await _eventContentCampaignManager
            .EventContentStoryStageResult(db, account, request);

        response.CampaignStageHistoryDB = historyDb.ToMap(_mapper);
        response.ParcelResultDB = parcelResultDb;
        response.FirstClearReward = new List<ParcelInfo>();
        response.EventMissionProgressDBDict = existingMission;

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_EnterMainGroundStage)]
    public async Task<EventContentEnterMainGroundStageResponse> EnterMainGroundStage(
        SchaleDataContext db,
        EventContentEnterMainGroundStageRequest request,
        EventContentEnterMainGroundStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (parcelInfos, parcelResult) = await _eventContentCampaignManager.EventContentEnterStage(
            db, account, request.StageUniqueId);

        response.ParcelResultDB = parcelResult;
        response.SaveDataDB = new()
        {
            AccountServerId = account.ServerId,
            CreateTime = account.GameSettings.ServerDateTime(),
            StageUniqueId = request.StageUniqueId,
            StageEntranceFee = parcelInfos
        };

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_EnterSubStage)]
    public async Task<EventContentEnterSubStageResponse> EnterSubStage(
        SchaleDataContext db,
        EventContentEnterSubStageRequest request,
        EventContentEnterSubStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (parcelInfos, parcelResult) = await _eventContentCampaignManager.EventContentEnterStage(
            db, account, request.StageUniqueId);

        response.ParcelResultDB = parcelResult;
        response.SaveDataDB = new()
        {
            AccountServerId = account.ServerId,
            CreateTime = account.GameSettings.ServerDateTime(),
            StageUniqueId = request.StageUniqueId,
            StageEntranceFee = parcelInfos
        };

        return response;
    }

    [ProtocolHandler(Protocol.EventContent_MainGroundStageResult)]
    public async Task<EventContentMainGroundStageResultResponse> MainGroundStageResult(
        SchaleDataContext db,
        EventContentMainGroundStageResultRequest request,
        EventContentMainGroundStageResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (historyDb, parcelResultDb, existingMission) = await _eventContentCampaignManager
            .EventContentMainGroundStageResult(db, account, request);

        response.TacticRank = 0;
        response.CampaignStageHistoryDB = historyDb.ToMap(_mapper);
        response.LevelUpCharacterDBs = new List<CharacterDB>();
        response.ParcelResultDB = parcelResultDb;
        response.FirstClearReward = new List<ParcelInfo>();
        response.ThreeStarReward = new List<ParcelInfo>();
        response.BonusReward = new List<ParcelInfo>();
        response.EventMissionProgressDBDict = existingMission;

        return response;
    }
}
