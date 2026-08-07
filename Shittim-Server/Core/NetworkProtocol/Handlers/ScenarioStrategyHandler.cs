using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

// Story battles with a strategy phase (the ones scripted into an episode, StoryStrategyExcel) are yet another skin over the campaign hex machinery, so everything map-shaped delegates to ConcentrateCampaignManager. The stage rows carry no entrance cost and no reward table - the episode's scenario script owns the payout - which is why TacticResult here is the bare state machine with an empty clear payload where Campaign_TacticResult rolls drops.
public class ScenarioStrategyHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ConcentrateCampaignManager _concentrateCampaignManager;
    private readonly EventContentCampaignManager _eventContentCampaignManager;
    private readonly HexaMapService _hexaMapService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;

    public ScenarioStrategyHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ConcentrateCampaignManager concentrateCampaignManager,
        EventContentCampaignManager eventContentCampaignManager,
        HexaMapService hexaMapService,
        ExcelTableService excelService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _concentrateCampaignManager = concentrateCampaignManager;
        _eventContentCampaignManager = eventContentCampaignManager;
        _hexaMapService = hexaMapService;
        _excelService = excelService;
        _mapper = mapper;
    }

    private StoryStrategyStageSaveDB Wire(CampaignMainStageSaveDBServer save)
        => (StoryStrategyStageSaveDB)ConcentrateCampaignManager.ShapeForWire(_mapper.Map<StoryStrategyStageSaveDB>(save));

    [ProtocolHandler(Protocol.Scenario_Enter)]
    public async Task<ScenarioEnterResponse> Enter(
        SchaleDataContext db,
        ScenarioEnterRequest request,
        ScenarioEnterResponse response)
    {
        // the client announces which episode it is opening; playback and history both run through other protocols
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_EnterMainStage)]
    public async Task<ScenarioEnterMainStageResponse> EnterMainStage(
        SchaleDataContext db,
        ScenarioEnterMainStageRequest request,
        ScenarioEnterMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await CreateStoryStrategySave(db, account, request.StageUniqueId);

        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    private async Task<CampaignMainStageSaveDBServer> CreateStoryStrategySave(SchaleDataContext db, AccountDBServer account, long stageUniqueId)
    {
        var hexaData = await _hexaMapService.LoadState(stageUniqueId);

        // entering a stage abandons whatever the player was in before it - only one campaign run can be open at a time, and the new one is what ContentSave_Get owes from here on
        await _concentrateCampaignManager.CloseConcentrateCampaigns(db, account);

        // DB row keeps every collection non-null (NOT NULL columns); ShapeForWire trims the wire copy down to official's key set at the handler
        var stageSave = new CampaignMainStageSaveDBServer
        {
            ContentType = Schale.FlatData.ContentType.StoryStrategyStage,
            LastEnemyEntityId = hexaData.LastEntityId,
            EnemyInfos = HexaMapService.AddHexaUnitList(hexaData.HexaUnitList),
            EchelonInfos = new Dictionary<long, HexaUnit>(),
            WithdrawInfos = new Dictionary<long, List<long>>(),
            StrategyObjects = HexaMapService.AddHexaStrategyList(hexaData.HexaStrageyList),
            StrategyObjectRewards = new Dictionary<long, List<ParcelInfo>>(),
            StrategyObjectHistory = new List<long>(),
            ActivatedHexaEventsAndConditions = ConcentrateCampaignManager.BuildStartEventActivations(hexaData),
            HexaEventDelayedExecutions = new Dictionary<long, List<long>>(),
            TileMapStates = HexaMapService.AddHexaTileList(hexaData),
            DisplayInfos = new List<HexaDisplayInfo>(),
            DeployedEchelonInfos = new List<HexaUnit>(),
            CreateTime = account.GameSettings.ServerDateTime(),
            StageUniqueId = stageUniqueId,
            StageEntranceFee = [],
            EnemyKillCountByUniqueId = new(),
            IsOpen = true
        };
        stageSave.AccountServerId = account.ServerId;

        db.CampaignMainStageSaves.Add(stageSave);
        await db.SaveChangesAsync();

        return stageSave;
    }

    [ProtocolHandler(Protocol.Scenario_ConfirmMainStage)]
    public async Task<ScenarioConfirmMainStageResponse> ConfirmMainStage(
        SchaleDataContext db,
        ScenarioConfirmMainStageRequest request,
        ScenarioConfirmMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.StartConcentrateCampaign(db, account, new CampaignConfirmMainStageRequest
        {
            StageUniqueId = request.StageUniqueId
        });

        // the save was created with an empty StageEntranceFee, so StartConcentrateCampaign charges nothing and this is a sync
        response.ParcelResultDB = new()
        {
            AccountDB = account.ToMap(_mapper),
            AccountCurrencyDB = db.Currencies.Where(x => x.AccountServerId == account.ServerId).FirstOrDefault()?.ToMap(_mapper) ?? new()
        };
        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_DeployEchelon)]
    public async Task<ScenarioDeployEchelonResponse> DeployEchelon(
        SchaleDataContext db,
        ScenarioDeployEchelonRequest request,
        ScenarioDeployEchelonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.DeployEchelon(db, account, new CampaignDeployEchelonRequest
        {
            StageUniqueId = request.StageUniqueId,
            DeployedEchelons = request.DeployedEchelons
        });

        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_MapMove)]
    public async Task<ScenarioMapMoveResponse> MapMove(
        SchaleDataContext db,
        ScenarioMapMoveRequest request,
        ScenarioMapMoveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, preMove) = await _concentrateCampaignManager.MoveTarget(db, account, new CampaignMapMoveRequest
        {
            StageUniqueId = request.StageUniqueId,
            EchelonEntityId = request.EchelonEntityId,
            DestPosition = request.DestPosition
        });

        // Official echoes the mover back on every MapMove reply.
        response.EchelonEntityId = request.EchelonEntityId;

        // Rewind before shaping: the response reports the mover as it stood at the start of this step, and the DisplayInfos entry is what walks it to the destination.
        response.SaveDataDB = (StoryStrategyStageSaveDB)ConcentrateCampaignManager.ShapeForWire(
            ConcentrateCampaignManager.RewindMovedEchelonForWire(
                _mapper.Map<StoryStrategyStageSaveDB>(stageSave), request.EchelonEntityId, preMove));

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_EnterTactic)]
    public async Task<ScenarioEnterTacticResponse> EnterTactic(
        SchaleDataContext db,
        ScenarioEnterTacticRequest request,
        ScenarioEnterTacticResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // Official replies with nothing but the header here; the work is remembering which enemy was engaged so Campaign_TacticResult can clear it off the map.
        await _concentrateCampaignManager.EnterTactic(db, account, new CampaignEnterTacticRequest
        {
            StageUniqueId = request.StageUniqueId,
            EchelonIndex = request.EchelonIndex,
            EnemyIndex = request.EnemyIndex
        });

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_TacticResult)]
    public async Task<ScenarioTacticResultResponse> TacticResult(
        SchaleDataContext db,
        ScenarioTacticResultRequest request,
        ScenarioTacticResultResponse response)
    {
        if (request.Summary == null)
            throw new InvalidOperationException("Scenario_TacticResult carried no battle summary");

        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.GetConcentrateCampaign(db, account, request.Summary.StageId);
        if (stageSave == null)
            throw new InvalidOperationException($"Story strategy stage save not found for stage {request.Summary.StageId}");

        stageSave.TacticClearTimeMscSum += (long)Math.Floor(request.Summary.EndFrame / 30f) * 1000;
        stageSave.EchelonInfos = ConcentrateCampaignManager.ChangeConcentratedEchelon(stageSave.EchelonInfos, request.Summary, request.Hand);

        // read before the win branch clears them - the enemy-phase continuation below resumes off the engaged id
        var engagedEnemyId = stageSave.EngagedEnemyEntityId;
        var wasEnemyPhase = stageSave.CampaignState == CampaignState.EnemyPhase;

        var isStageClear = false;
        var endBattleType = CampaignEndBattle.Win;
        var tacticWon = !request.Summary.IsAbort && request.Summary.EndType == BattleEndType.Clear;

        if (!tacticWon)
        {
            if (stageSave.EchelonInfos != null)
                stageSave.EchelonInfos.Remove(request.Summary.Group01Summary.TeamId);
        }
        else
        {
            if (stageSave.EnemyInfos != null &&
                stageSave.EnemyInfos.Remove(stageSave.EngagedEnemyEntityId))
            {
                stageSave.EnemyClearCount++;
            }

            stageSave.EngagedEnemyEntityId = 0;

            if (ConcentrateCampaignManager.CalcTacticRank(request.Summary) >= ConcentrateCampaignManager.TacticRankS)
                stageSave.TacticRankSCount++;

            var hexaData = await _concentrateCampaignManager.LoadStageMap(stageSave);
            var endBattle = HexaMapService.FindSatisfiedEndBattle(
                hexaData, stageSave.EnemyInfos, stageSave.ActivatedHexaEventsAndConditions);

            if (endBattle is { } fired)
            {
                stageSave.ActivatedHexaEventsAndConditions ??= new Dictionary<long, List<long>>();
                stageSave.ActivatedHexaEventsAndConditions[fired.Event.EventId] = fired.ConditionIds;

                endBattleType = fired.Command.EndBattleType;
                isStageClear = true;
            }
            else if (stageSave.EnemyInfos is { Count: 0 })
            {
                isStageClear = true;
            }

            // ending the run stops ContentSave_Get offering the save as resumable; scenario progress itself is Scenario_Clear's job, driven by the script that resumes after this battle
            if (isStageClear)
                stageSave.IsOpen = false;
        }

        if (wasEnemyPhase && !isStageClear)
        {
            ConcentrateCampaignManager.DecideEnemyMoves(
                await _concentrateCampaignManager.LoadStageMap(stageSave), stageSave, engagedEnemyId,
                _excelService.GetTable<Schale.FlatData.CampaignUnitExcelT>(), _excelService.GetTable<Schale.FlatData.CampaignStrategyObjectExcelT>());
        }
        else
        {
            stageSave.DisplayInfos = new List<HexaDisplayInfo>();
        }

        db.CampaignMainStageSaves.Update(stageSave);
        await db.SaveChangesAsync();

        // the EndBattle entry is still what tells the client the map is done, it just carries nothing: no reward table exists for these stages
        var clearReward = isStageClear
            ? new StrategyClearRewardInfo
            {
                FirstClearReward = new List<ParcelInfo>(),
                ThreeStarReward = new List<ParcelInfo>(),
                StrategyObjectRewards = new Dictionary<long, List<ParcelInfo>>(),
                ParcelResultDB = new ParcelResultDB()
            }
            : null;

        response.SaveDataDB = (StoryStrategyStageSaveDB)ConcentrateCampaignManager.AttachStageClearForWire(
            Wire(stageSave), clearReward, endBattleType);
        response.IsPlayerWin = tacticWon;

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_EndTurn)]
    public async Task<ScenarioEndTurnResponse> EndTurn(
        SchaleDataContext db,
        ScenarioEndTurnRequest request,
        ScenarioEndTurnResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.EndTurn(db, account, new CampaignEndTurnRequest
        {
            StageUniqueId = request.StageUniqueId
        });

        response.SaveDataDB = Wire(stageSave);
        response.AccountCurrencyDB = db.Currencies.Where(x => x.AccountServerId == account.ServerId).FirstOrDefault()?.ToMap(_mapper) ?? new();

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_Portal)]
    public async Task<ScenarioPortalResponse> Portal(
        SchaleDataContext db,
        ScenarioPortalRequest request,
        ScenarioPortalResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _eventContentCampaignManager.Portal(db, account, new EventContentPortalRequest
        {
            StageUniqueId = request.StageUniqueId,
            EchelonEntityId = request.EchelonEntityId
        });

        response.StoryStrategyStageSaveDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_WithdrawEchelon)]
    public async Task<ScenarioWithdrawEchelonResponse> WithdrawEchelon(
        SchaleDataContext db,
        ScenarioWithdrawEchelonRequest request,
        ScenarioWithdrawEchelonResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (stageSave, echelons) = await _eventContentCampaignManager.WithdrawEchelon(db, account, new EventContentWithdrawEchelonRequest
        {
            StageUniqueId = request.StageUniqueId,
            WithdrawEchelonEntityId = request.WithdrawEchelonEntityId
        });

        response.SaveDataDB = Wire(stageSave);
        response.WithdrawEchelonDBs = echelons;

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_Retreat)]
    public async Task<ScenarioRetreatResponse> Retreat(
        SchaleDataContext db,
        ScenarioRetreatRequest request,
        ScenarioRetreatResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageSave = await _concentrateCampaignManager.GetConcentrateCampaign(db, account, request.StageUniqueId);
        response.ReleasedEchelonNumbers = stageSave?.EchelonInfos?.Keys.ToList() ?? new List<long>();

        await _concentrateCampaignManager.CloseConcentrateCampaigns(db, account, request.StageUniqueId);

        // nothing was paid to enter, so unlike Campaign_Retreat there is no 90% refund to carry
        response.ParcelResultDB = new()
        {
            AccountCurrencyDB = db.Currencies.Where(x => x.AccountServerId == account.ServerId).FirstOrDefault()?.ToMap(_mapper) ?? new()
        };

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_RestartMainStage)]
    public async Task<ScenarioRestartMainStageResponse> RestartMainStage(
        SchaleDataContext db,
        ScenarioRestartMainStageRequest request,
        ScenarioRestartMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // a restart is just a fresh save - the creation path already closes whatever run was open, and it comes back BeforeStart for the ConfirmMainStage that follows
        var stageSave = await CreateStoryStrategySave(db, account, request.StageUniqueId);

        response.ParcelResultDB = new()
        {
            AccountCurrencyDB = db.Currencies.Where(x => x.AccountServerId == account.ServerId).FirstOrDefault()?.ToMap(_mapper) ?? new()
        };
        response.SaveDataDB = Wire(stageSave);

        return response;
    }

    [ProtocolHandler(Protocol.Scenario_SkipMainStage)]
    public async Task<ScenarioSkipMainStageResponse> SkipMainStage(
        SchaleDataContext db,
        ScenarioSkipMainStageRequest request,
        ScenarioSkipMainStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // skipping steps over the strategy phase entirely - the script carries on client-side, the server just has to stop offering the map back as an open run
        await _concentrateCampaignManager.CloseConcentrateCampaigns(db, account, request.StageUniqueId);

        return response;
    }
}
