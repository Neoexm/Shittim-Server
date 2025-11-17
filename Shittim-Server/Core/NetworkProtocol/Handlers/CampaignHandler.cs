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
using Shittim_Server.Managers;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class CampaignHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;
    private readonly CampaignManager _campaignManager;
    private readonly IMapper _mapper;

    public CampaignHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        CampaignManager campaignManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _campaignManager = campaignManager;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.Campaign_List)]
    public async Task<CampaignListResponse> List(
        SchaleDataContext db,
        CampaignListRequest request,
        CampaignListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.CampaignChapterClearRewardHistoryDBs = db.GetAccountCampaignChapterClearRewardHistories(account.ServerId).ToMapList(_mapper);
        response.StageHistoryDBs = db.GetAccountCampaignStageHistories(account.ServerId).ToMapList(_mapper);
        response.StrategyObjecthistoryDBs = db.GetAccountStrategyObjectHistories(account.ServerId).ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_EnterTutorialStage)]
    public async Task<CampaignEnterTutorialStageResponse> EnterTutorialStage(
        SchaleDataContext db,
        CampaignEnterTutorialStageRequest request,
        CampaignEnterTutorialStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var parcelInfo = _campaignManager.TemporaryCampaignParcelInit(db, account, request.StageUniqueId);

        response.SaveDataDB = new()
        {
            AccountServerId = account.ServerId,
            CreateTime = account.GameSettings.ServerDateTime(),
            StageUniqueId = request.StageUniqueId,
            StageEntranceFee = parcelInfo
        };

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_TutorialStageResult)]
    public async Task<CampaignTutorialStageResultResponse> TutorialStageResult(
        SchaleDataContext db,
        CampaignTutorialStageResultRequest request,
        CampaignTutorialStageResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (historyDb, parcelResultDb) = await _campaignManager.CampaignTutorialStageResult(db, account, request);

        response.CampaignStageHistoryDB = historyDb.ToMap(_mapper);
        response.ParcelResultDB = parcelResultDb;
        response.ClearReward = new();
        response.FirstClearReward = new();

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_EnterSubStage)]
    public async Task<CampaignEnterSubStageResponse> EnterSubStage(
        SchaleDataContext db,
        CampaignEnterSubStageRequest request,
        CampaignEnterSubStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (parcelInfo, parcelResultDb) = await _campaignManager.CampaignEnterStage(db, account, request.StageUniqueId);

        response.ParcelResultDB = parcelResultDb;
        response.SaveDataDB = new()
        {
            AccountServerId = account.ServerId,
            CreateTime = account.GameSettings.ServerDateTime(),
            StageUniqueId = request.StageUniqueId,
            StageEntranceFee = parcelInfo
        };

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_SubStageResult)]
    public async Task<CampaignSubStageResultResponse> SubStageResult(
        SchaleDataContext db,
        CampaignSubStageResultRequest request,
        CampaignSubStageResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (historyDb, parcelResultDb) = await _campaignManager.CampaignSubStageResult(db, account, request);

        response.TacticRank = 0;
        response.CampaignStageHistoryDB = historyDb.ToMap(_mapper);
        response.LevelUpCharacterDBs = new();
        response.ParcelResultDB = parcelResultDb;
        response.FirstClearReward = new();
        response.ThreeStarReward = new();

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_EnterMainStageStrategySkip)]
    public async Task<CampaignEnterMainStageStrategySkipResponse> EnterMainStageStrategySkip(
        SchaleDataContext db,
        CampaignEnterMainStageStrategySkipRequest request,
        CampaignEnterMainStageStrategySkipResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (_, parcelResultDb) = await _campaignManager.CampaignEnterStage(db, account, request.StageUniqueId);

        response.ParcelResultDB = parcelResultDb;

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_MainStageStrategySkipResult)]
    public async Task<CampaignMainStageStrategySkipResultResponse> MainStageStrategySkipResult(
        SchaleDataContext db,
        CampaignMainStageStrategySkipResultRequest request,
        CampaignMainStageStrategySkipResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (historyDb, parcelResultDb) = await _campaignManager.CampaignMainStageStrategySkipResult(db, account, request);

        response.TacticRank = 0;
        response.CampaignStageHistoryDB = historyDb.ToMap(_mapper);
        response.LevelUpCharacterDBs = new();
        response.ParcelResultDB = parcelResultDb;
        response.FirstClearReward = new();
        response.ThreeStarReward = new();

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_ChapterClearReward)]
    public async Task<CampaignChapterClearRewardResponse> ChapterClearReward(
        SchaleDataContext db,
        CampaignChapterClearRewardRequest request,
        CampaignChapterClearRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (rewardHistory, parcelResultDB) = await _campaignManager.CampaignChapterClearReward(db, account, request);

        response.CampaignChapterClearRewardHistoryDB = rewardHistory.ToMap(_mapper);
        response.ParcelResultDB = parcelResultDB;

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_RestartMainStage)]
    public async Task<CampaignRestartMainStageResponse> RestartMainStage(
        SchaleDataContext db,
        CampaignRestartMainStageRequest request,
        CampaignRestartMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var parcelInfo = _campaignManager.TemporaryCampaignParcelInit(db, account, request.StageUniqueId);

        response.SaveDataDB = new()
        {
            AccountServerId = account.ServerId,
            CreateTime = account.GameSettings.ServerDateTime(),
            StageUniqueId = request.StageUniqueId,
            StageEntranceFee = parcelInfo
        };

        return response;
    }

    [ProtocolHandler(Protocol.Campaign_Retreat)]
    public async Task<CampaignRetreatResponse> Retreat(
        SchaleDataContext db,
        CampaignRetreatRequest request,
        CampaignRetreatResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var parcelResultDb = await _campaignManager.CampaignRetreat(db, account, request.StageUniqueId);

        response.ParcelResultDB = parcelResultDb;

        return response;
    }
}
