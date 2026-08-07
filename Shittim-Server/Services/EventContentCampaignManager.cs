using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.MX.Core.Math;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.NetworkProtocol;
using Shittim.Services;

namespace Shittim_Server.Services
{
    public class EventContentCampaignManager
    {
        private readonly ExcelTableService _excelService;
        private readonly HexaMapService _hexaMapService;
        private readonly ConcentrateCampaignManager _concentrateCampaignManager;
        private readonly EventContentCollectionService _collectionService;
        private readonly ParcelHandler _parcelHandler;
        private readonly IMapper _mapper;

        public EventContentCampaignManager(
            ExcelTableService excelService,
            HexaMapService hexaMapService,
            ConcentrateCampaignManager concentrateCampaignManager,
            EventContentCollectionService collectionService,
            ParcelHandler parcelHandler,
            IMapper mapper)
        {
            _excelService = excelService;
            _hexaMapService = hexaMapService;
            _concentrateCampaignManager = concentrateCampaignManager;
            _collectionService = collectionService;
            _parcelHandler = parcelHandler;
            _mapper = mapper;
        }

        public async Task<(List<ParcelInfo>, ParcelResultDB)> EventContentEnterStage(
            SchaleDataContext context,
            AccountDBServer account,
            long stageUniqueId)
        {
            var stageExcels = _excelService.GetTable<EventContentStageExcelT>();
            var campaignStageExcel = stageExcels.GetEventContentStageId(stageUniqueId);

            var parcelInfo = new ParcelResult(
                campaignStageExcel.StageEnterCostType,
                campaignStageExcel.StageEnterCostId,
                campaignStageExcel.StageEnterCostAmount);

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfo, isConsume: true);

            return (parcelResolver.ParcelInfos, parcelResolver.ParcelResult);
        }

        public async Task<(CampaignStageHistoryDBServer, ParcelResultDB, Dictionary<long, List<MissionProgressDB>>)> 
            EventContentStoryStageResult(
                SchaleDataContext context,
                AccountDBServer account,
                EventContentStoryStageResultRequest req)
        {
            var dateTime = account.GameSettings.ServerDateTime();

            var stageExcels = _excelService.GetTable<EventContentStageExcelT>();
            var campaignStageExcel = stageExcels.GetEventContentStageId(req.StageUniqueId);

            var historyDb = new CampaignStageHistoryDBServer
            {
                AccountServerId = account.ServerId,
                StageUniqueId = req.StageUniqueId,
                ClearTurnRecord = 1,
                IsClearedEver = true,
                TodayPlayCount = 1,
                LastPlay = dateTime,
                FirstClearRewardReceive = dateTime,
                StarRewardReceive = dateTime
            };

            var existingHistory = await context.CampaignStageHistories
                .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.StageUniqueId == req.StageUniqueId);

            // read before the merge, which keeps the existing row and does not carry the receive stamps over from this run's history
            var grantFirstClear = existingHistory == null || existingHistory.FirstClearRewardReceive == null;
            var grantThreeStar = existingHistory == null || existingHistory.StarRewardReceive == null;

            if (existingHistory == null)
            {
                context.CampaignStageHistories.Add(historyDb);
            }
            else
            {
                MergeExistHistoryWithNew(context, existingHistory, historyDb, dateTime);
                historyDb = existingHistory;
            }

            if (grantFirstClear) historyDb.FirstClearRewardReceive = dateTime;
            if (grantThreeStar) historyDb.StarRewardReceive = dateTime;

            var eventMissionProgressDBDict = await ProgressEventMissions(
                context, account, req.EventContentId, req.StageUniqueId);

            var (firstClear, threeStar, drops) = StageRewards(
                campaignStageExcel.EventContentStageRewardId, grantFirstClear, grantThreeStar);

            var parcelInfos = firstClear.Concat(drops).Concat(threeStar).ToList();

            parcelInfos.Add(new ParcelResult(
                ParcelType.AccountExp,
                campaignStageExcel.StageEnterCostId,
                campaignStageExcel.StageEnterCostAmount));

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfos);
            await context.SaveChangesAsync();

            return (historyDb, parcelResolver.ParcelResult, eventMissionProgressDBDict);
        }

        public async Task<(CampaignStageHistoryDBServer, ParcelResultDB, Dictionary<long, List<MissionProgressDB>>)>
            EventContentMainGroundStageResult(
                SchaleDataContext context,
                AccountDBServer account,
                EventContentMainGroundStageResultRequest req)
        {
            var dateTime = account.GameSettings.ServerDateTime();

            var stageExcels = _excelService.GetTable<EventContentStageExcelT>();
            var campaignStageExcel = stageExcels.GetEventContentStageId(req.Summary.StageId);

            CampaignStageHistoryDBServer historyDb = new();
            bool grantFirstClear;
            bool grantThreeStar;

            if (CheckIfCleared(req.Summary))
            {
                historyDb = new CampaignStageHistoryDBServer
                {
                    AccountServerId = account.ServerId,
                    StageUniqueId = req.Summary.StageId,
                    IsClearedEver = true,
                    TodayPlayCount = 1,
                    LastPlay = dateTime,
                    FirstClearRewardReceive = dateTime,
                    StarRewardReceive = dateTime
                };

                CalcStrategySkipStarGoals(historyDb, req.Summary);

                var existingHistory = await context.CampaignStageHistories
                    .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.StageUniqueId == req.Summary.StageId);

                grantFirstClear = existingHistory == null || existingHistory.FirstClearRewardReceive == null;
                grantThreeStar = existingHistory == null || existingHistory.StarRewardReceive == null;

                if (existingHistory != null)
                {
                    MergeExistHistoryWithNew(context, existingHistory, historyDb, dateTime);
                    historyDb = existingHistory;
                }
                else
                {
                    context.CampaignStageHistories.Add(historyDb);
                }

                if (grantFirstClear) historyDb.FirstClearRewardReceive = dateTime;
                if (grantThreeStar) historyDb.StarRewardReceive = dateTime;
            }
            else
            {
                var amount = (long)(campaignStageExcel.StageEnterCostAmount * 0.9);
                var parcelInfo = new ParcelResult(campaignStageExcel.StageEnterCostType, campaignStageExcel.StageEnterCostId, amount);
                var parcelRetreat = await _parcelHandler.BuildParcel(context, account, parcelInfo);
                return (historyDb, parcelRetreat.ParcelResult, new Dictionary<long, List<MissionProgressDB>>());
            }

            var eventMissionProgressDBDict = await ProgressEventMissions(
                context, account, req.EventContentId, req.Summary.StageId);

            var (firstClear, threeStar, drops) = StageRewards(
                campaignStageExcel.EventContentStageRewardId, grantFirstClear, grantThreeStar);

            var parcelInfos = firstClear.Concat(drops).Concat(threeStar).ToList();

            parcelInfos.Add(new ParcelResult(ParcelType.AccountExp, 0, campaignStageExcel.StageEnterCostAmount));

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfos);
            await context.SaveChangesAsync();

            return (historyDb, parcelResolver.ParcelResult, eventMissionProgressDBDict);
        }

        // The event twin of ConcentrateCampaignManager.CreateConcentrateCampaign. Only three things differ: the map is named by the stage row rather than derived from the stage id, the ContentType the client keys the save by is EventContentMainStage, and the buff dictionary the stage's BuffContentId feeds exists.
        public async Task<CampaignMainStageSaveDBServer> CreateEventMainStage(
            SchaleDataContext context,
            AccountDBServer account,
            long stageUniqueId)
        {
            var stageExcel = _excelService.GetTable<EventContentStageExcelT>().GetEventContentStageId(stageUniqueId);
            var hexaData = await _hexaMapService.LoadState(stageExcel.StrategyMap);

            await _concentrateCampaignManager.CloseConcentrateCampaigns(context, account);

            var entranceFee = ParcelInfo.CreateParcelInfo(
                stageExcel.StageEnterCostType, stageExcel.StageEnterCostId, stageExcel.StageEnterCostAmount);

            var stageSave = new CampaignMainStageSaveDBServer
            {
                ContentType = Schale.FlatData.ContentType.EventContentMainStage,
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
                SelectedBuffDict = new Dictionary<long, long>(),
                StrategyMap = stageExcel.StrategyMap,
                CreateTime = account.GameSettings.ServerDateTime(),
                StageUniqueId = stageUniqueId,
                StageEntranceFee = entranceFee,
                EnemyKillCountByUniqueId = new(),
                IsOpen = true
            };
            stageSave.AccountServerId = account.ServerId;

            context.CampaignMainStageSaves.Add(stageSave);
            await context.SaveChangesAsync();

            return stageSave;
        }

        public async Task<(CampaignMainStageSaveDBServer Save, CampaignStageHistoryDB History, long TacticRank,
            StrategyClearRewardInfo? ClearReward, CampaignEndBattle EndBattleType, ParcelResultDB ParcelResult,
            List<ParcelInfo> BonusReward, List<EventContentCollectionDB> Collections)> EventTacticResult(
            SchaleDataContext context,
            AccountDBServer account,
            EventContentTacticResultRequest req)
        {
            if (req.Summary == null)
                throw new InvalidOperationException("EventContent_TacticResult carried no battle summary");

            var dateTime = account.GameSettings.ServerDateTime();
            var stageSaveData = await _concentrateCampaignManager.GetConcentrateCampaign(context, account, req.Summary.StageId);

            if (stageSaveData == null)
                throw new InvalidOperationException($"Event stage save not found for stage {req.Summary.StageId}");

            var stageExcel = _excelService.GetTable<EventContentStageExcelT>()
                .FirstOrDefault(x => x.Id == req.Summary.StageId);
            var tacticRank = ConcentrateCampaignManager.CalcTacticRank(req.Summary);

            // no ChapterUniqueId: event stages are grouped by EventContentId and difficulty, and nothing in CampaignChapterExcel covers them
            var historyDb = new CampaignStageHistoryDBServer
            {
                AccountServerId = account.ServerId,
                StageUniqueId = req.Summary.StageId,
                LastPlay = dateTime,
                TodayPlayCount = 0,
                ClearTurnRecord = 0,
                Star1Flag = false,
                Star2Flag = false,
                Star3Flag = false
            };

            stageSaveData.TacticClearTimeMscSum += (long)Math.Floor(req.Summary.EndFrame / 30f) * 1000;
            stageSaveData.EchelonInfos = ConcentrateCampaignManager.ChangeConcentratedEchelon(stageSaveData.EchelonInfos, req.Summary, req.Hand);

            // read before the win branch clears them - the enemy-phase continuation below resumes off the engaged id
            var engagedEnemyId = stageSaveData.EngagedEnemyEntityId;
            var wasEnemyPhase = stageSaveData.CampaignState == CampaignState.EnemyPhase;

            var isStageClear = false;
            var endBattleType = CampaignEndBattle.Win;
            var tacticWon = CheckIfCleared(req.Summary);

            if (!tacticWon)
            {
                if (stageSaveData.EchelonInfos != null)
                    stageSaveData.EchelonInfos.Remove(req.Summary.Group01Summary.TeamId);
            }
            else
            {
                if (stageSaveData.EnemyInfos != null &&
                    stageSaveData.EnemyInfos.Remove(stageSaveData.EngagedEnemyEntityId))
                {
                    stageSaveData.EnemyClearCount++;
                }

                stageSaveData.EngagedEnemyEntityId = 0;

                if (tacticRank >= ConcentrateCampaignManager.TacticRankS)
                    stageSaveData.TacticRankSCount++;

                var hexaData = await _concentrateCampaignManager.LoadStageMap(stageSaveData);
                var endBattle = HexaMapService.FindSatisfiedEndBattle(
                    hexaData, stageSaveData.EnemyInfos, stageSaveData.ActivatedHexaEventsAndConditions);

                if (endBattle is { } fired)
                {
                    stageSaveData.ActivatedHexaEventsAndConditions ??= new Dictionary<long, List<long>>();
                    stageSaveData.ActivatedHexaEventsAndConditions[fired.Event.EventId] = fired.ConditionIds;

                    endBattleType = fired.Command.EndBattleType;
                    isStageClear = true;
                }
                else if (stageSaveData.EnemyInfos is { Count: 0 })
                {
                    isStageClear = true;
                }

                if (isStageClear)
                    stageSaveData.IsOpen = false;
            }

            if (isStageClear)
            {
                historyDb.Star1Flag = true;
                historyDb.Star2Flag = stageExcel != null && stageSaveData.CurrentTurn <= stageExcel.StarConditionTurnCount;
                historyDb.Star3Flag = stageExcel != null && stageSaveData.TacticRankSCount >= stageExcel.StarConditionTacticRankSCount;
                historyDb.ClearTurnRecord = stageSaveData.CurrentTurn;
                historyDb.TacticClearCountWithRankSRecord = stageSaveData.TacticRankSCount;
                historyDb.IsClearedEver = true;
            }

            var existHistory = await context.CampaignStageHistories
                .FirstOrDefaultAsync(x =>
                    x.AccountServerId == account.ServerId &&
                    x.StageUniqueId == req.Summary.StageId);

            // Both rewards are once-per-account, and the gates read the merged history, so a replayed stage still ends but advertises nothing - official does the same on a re-clear.
            var grantFirstClear = isStageClear && (existHistory == null || existHistory.FirstClearRewardReceive == null);
            var grantThreeStar = isStageClear
                && historyDb.Star1Flag && historyDb.Star2Flag && historyDb.Star3Flag
                && (existHistory == null || existHistory.StarRewardReceive == null);

            if (existHistory != null)
            {
                MergeExistHistoryWithNew(context, existHistory, historyDb, dateTime);
                historyDb = existHistory;
            }
            else
            {
                context.CampaignStageHistories.Add(historyDb);
            }

            if (grantFirstClear) historyDb.FirstClearRewardReceive = dateTime;
            if (grantThreeStar) historyDb.StarRewardReceive = dateTime;

            if (wasEnemyPhase && !isStageClear)
            {
                ConcentrateCampaignManager.DecideEnemyMoves(
                    await _concentrateCampaignManager.LoadStageMap(stageSaveData), stageSaveData, engagedEnemyId,
                    _excelService.GetTable<CampaignUnitExcelT>(), _excelService.GetTable<CampaignStrategyObjectExcelT>());
            }
            else
            {
                stageSaveData.DisplayInfos = new List<HexaDisplayInfo>();
            }

            // Only while the run is still going: EventContent_SelectBuff answers against the account's one open save, so a popup raised by the tactic that cleared the stage could never be closed.
            if (tacticWon && !isStageClear)
                RollBuffGroup(stageSaveData, stageExcel, req.EventContentId);

            if (isStageClear)
                await ProgressEventMissions(context, account, req.EventContentId, req.Summary.StageId);

            context.CampaignMainStageSaves.Update(stageSaveData);
            await context.SaveChangesAsync();

            var historyMap = historyDb.ToMap(_mapper);

            var (parcelResult, clearReward, bonusReward) = await BuildEventTacticReward(
                context, account, stageExcel, req.EventContentId, stageSaveData, isStageClear, grantFirstClear, grantThreeStar);

            if (clearReward != null)
                clearReward.CampaignStageHistoryDB = historyMap;

            var collections = new List<EventContentCollectionDB>();
            if (isStageClear)
            {
                collections = _collectionService.Unlock(context, account, req.EventContentId, CollectionUnlockType.ClearSpecificEventStage);
                await context.SaveChangesAsync();
            }

            return (stageSaveData, historyMap, tacticRank, clearReward, endBattleType, parcelResult, bonusReward, collections);
        }

        public async Task<(CampaignStageHistoryDBServer History, ParcelResultDB ParcelResult, long TacticRank,
            List<ParcelInfo> FirstClearReward, List<EventContentCollectionDB> Collections)> EventSubStageResult(
                SchaleDataContext context,
                AccountDBServer account,
                EventContentSubStageResultRequest req)
        {
            var dateTime = account.GameSettings.ServerDateTime();

            var stageExcels = _excelService.GetTable<EventContentStageExcelT>();
            var campaignStageExcel = stageExcels.GetEventContentStageId(req.Summary.StageId);
            var tacticRank = ConcentrateCampaignManager.CalcTacticRank(req.Summary);

            if (!CheckIfCleared(req.Summary))
            {
                var amount = (long)(campaignStageExcel.StageEnterCostAmount * 0.9);
                var refund = new ParcelResult(campaignStageExcel.StageEnterCostType, campaignStageExcel.StageEnterCostId, amount);
                var refundResolver = await _parcelHandler.BuildParcel(context, account, refund);

                return (new CampaignStageHistoryDBServer(), refundResolver.ParcelResult, tacticRank, new List<ParcelInfo>(), new List<EventContentCollectionDB>());
            }

            var historyDb = new CampaignStageHistoryDBServer
            {
                AccountServerId = account.ServerId,
                StageUniqueId = req.Summary.StageId,
                IsClearedEver = true,
                TodayPlayCount = 1,
                LastPlay = dateTime,
                FirstClearRewardReceive = dateTime,
                StarRewardReceive = dateTime
            };

            CalcStrategySkipStarGoals(historyDb, req.Summary);

            var existingHistory = await context.CampaignStageHistories
                .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.StageUniqueId == req.Summary.StageId);

            var grantFirstClear = existingHistory == null || existingHistory.FirstClearRewardReceive == null;
            var grantThreeStar = existingHistory == null || existingHistory.StarRewardReceive == null;

            if (existingHistory != null)
            {
                MergeExistHistoryWithNew(context, existingHistory, historyDb, dateTime);
                historyDb = existingHistory;
            }
            else
            {
                context.CampaignStageHistories.Add(historyDb);
            }

            if (grantFirstClear) historyDb.FirstClearRewardReceive = dateTime;
            if (grantThreeStar) historyDb.StarRewardReceive = dateTime;

            await ProgressEventMissions(context, account, req.EventContentId, req.Summary.StageId);

            var (firstClear, threeStar, drops) = StageRewards(
                campaignStageExcel.EventContentStageRewardId, grantFirstClear, grantThreeStar);

            var parcelInfos = firstClear.Concat(drops).Concat(threeStar).ToList();
            parcelInfos.Add(new ParcelResult(ParcelType.AccountExp, 0, campaignStageExcel.StageEnterCostAmount));

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfos);

            var collections = _collectionService.Unlock(context, account, req.EventContentId, CollectionUnlockType.ClearSpecificEventStage);

            await context.SaveChangesAsync();

            return (historyDb, parcelResolver.ParcelResult, tacticRank, ToParcelInfos(firstClear), collections);
        }

        public async Task<(ParcelResultDB ParcelResult, List<long> ReleasedEchelonNumbers)> EventRetreat(
            SchaleDataContext context,
            AccountDBServer account,
            long stageUniqueId)
        {
            var stageSaveData = await _concentrateCampaignManager.GetConcentrateCampaign(context, account, stageUniqueId);
            var released = stageSaveData?.EchelonInfos?.Keys.ToList() ?? new List<long>();

            var stageExcel = _excelService.GetTable<EventContentStageExcelT>().GetEventContentStageId(stageUniqueId);
            // no open run means the entrance fee was never taken for it, so there is nothing to hand back
            var amount = stageSaveData == null ? 0 : (long)(stageExcel.StageEnterCostAmount * 0.9);
            var parcelInfos = new ParcelResult(stageExcel.StageEnterCostType, stageExcel.StageEnterCostId, amount);

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelInfos);

            await _concentrateCampaignManager.CloseConcentrateCampaigns(context, account, stageUniqueId);

            return (parcelResolver.ParcelResult, released);
        }

        public async Task<(CampaignMainStageSaveDBServer Save, List<EchelonDB> Echelons)> WithdrawEchelon(
            SchaleDataContext context,
            AccountDBServer account,
            EventContentWithdrawEchelonRequest req)
        {
            var stageSaveData = await _concentrateCampaignManager.GetConcentrateCampaign(context, account, req.StageUniqueId);
            if (stageSaveData == null)
                throw new InvalidOperationException($"Event stage save not found for stage {req.StageUniqueId}");

            var withdrawn = req.WithdrawEchelonEntityId ?? new List<long>();
            var echelons = new List<EchelonDB>();

            foreach (var entityId in withdrawn)
            {
                stageSaveData.EchelonInfos?.Remove(entityId);
                stageSaveData.DeployedEchelonInfos?.RemoveAll(x => x.EntityId == entityId);

                // on a strategy map the entity id IS the echelon number - it is what DeployConcentratedEchelon looked the echelon up by
                var echelonData = await EchelonService.GetConcentratedCampaignEchelon(context, account.ServerId, entityId);
                var mapped = echelonData?.ToMap(_mapper);

                if (mapped != null)
                    echelons.Add(mapped);
            }

            if (withdrawn.Count > 0)
            {
                // keyed by the turn the withdrawal happened on, which is what the client's map replays them against
                stageSaveData.WithdrawInfos ??= new Dictionary<long, List<long>>();
                stageSaveData.WithdrawInfos.TryGetValue(stageSaveData.CurrentTurn, out var thisTurn);
                stageSaveData.WithdrawInfos[stageSaveData.CurrentTurn] = (thisTurn ?? new List<long>()).Concat(withdrawn).ToList();
            }

            context.CampaignMainStageSaves.Update(stageSaveData);
            await context.SaveChangesAsync();

            return (stageSaveData, echelons);
        }

        // Mirrors the client's CampaignService.FindPortalExit/FindCounterPartPortalObject/MoveHexaUnitByPortal: the tile the echelon stands on has to carry a Portal or a one-way entrance, the counterpart is the object sharing its PortalId under a different EntityId (an entrance pairs with an exit, a two-way portal with another two-way portal), and a unit of the same side already standing on the far tile refuses the warp.
        // Strategy rows in the strategymap dumps carry no CampaignStrategyExcel of their own, so the object type has to come from CampaignStrategyObjectExcel keyed by Strategy.Id.
        public async Task<CampaignMainStageSaveDBServer> Portal(
            SchaleDataContext context,
            AccountDBServer account,
            EventContentPortalRequest req)
        {
            var stageSaveData = await _concentrateCampaignManager.GetConcentrateCampaign(context, account, req.StageUniqueId);
            if (stageSaveData == null)
                throw new InvalidOperationException($"Event stage save not found for stage {req.StageUniqueId}");

            if (stageSaveData.EchelonInfos == null
                || !stageSaveData.EchelonInfos.TryGetValue(req.EchelonEntityId, out var echelon))
            {
                throw new WebAPIException(WebAPIErrorCode.CampaignStageEchelonNotFound, $"Echelon {req.EchelonEntityId} is not on the map");
            }

            var objectExcels = _excelService.GetTable<CampaignStrategyObjectExcelT>();
            var strategies = stageSaveData.StrategyObjects?.Values.ToList() ?? new List<Strategy>();

            var entrance = strategies.FirstOrDefault(x => SameTile(x.Location, echelon.Location));
            var entranceExcel = entrance == null ? null : objectExcels.FirstOrDefault(x => x.Id == entrance.Id);

            if (entranceExcel == null
                || (entranceExcel.StrategyObjectType != StrategyObjectType.Portal
                    && entranceExcel.StrategyObjectType != StrategyObjectType.PortalOneWayEnterance))
            {
                throw new WebAPIException(WebAPIErrorCode.CampaignPortalExitNotFound, $"Echelon {req.EchelonEntityId} is not standing on a portal");
            }

            var exitType = entranceExcel.StrategyObjectType == StrategyObjectType.PortalOneWayEnterance
                ? StrategyObjectType.PortalOneWayExit
                : StrategyObjectType.Portal;

            var exit = strategies.FirstOrDefault(x =>
            {
                if (x.EntityId == entrance!.EntityId)
                    return false;

                var excel = objectExcels.FirstOrDefault(e => e.Id == x.Id);
                return excel != null && excel.StrategyObjectType == exitType && excel.PortalId == entranceExcel.PortalId;
            });

            if (exit == null || exit.Location == null)
                throw new WebAPIException(WebAPIErrorCode.CampaignPortalExitNotFound, $"Portal {entranceExcel.PortalId} has no counterpart on this map");

            var occupied = stageSaveData.EchelonInfos.Values.Any(x => x.EntityId != echelon.EntityId && SameTile(x.Location, exit.Location));
            if (occupied)
                throw new WebAPIException(WebAPIErrorCode.CampaignCannotReachDestination, "The portal exit is occupied");

            // fresh HexLocation2D rather than sharing the exit object's - both sit in the same save row and a later write through one would drag the other with it
            echelon.Location = new HexLocation2D { x = exit.Location.x, y = exit.Location.y, z = exit.Location.z };

            // the warp costs neither an action nor a slot in the movement queue; this one entry is the whole of what the client plays
            stageSaveData.DisplayInfos = new List<HexaDisplayInfo>
            {
                new()
                {
                    Type = HexaDisplayType.WarpUnit,
                    EntityId = req.EchelonEntityId,
                    Location = new HexLocation { x = exit.Location.x, y = exit.Location.y, z = exit.Location.z },
                },
            };

            context.CampaignMainStageSaves.Update(stageSaveData);
            await context.SaveChangesAsync();

            return stageSaveData;
        }

        public async Task<CampaignMainStageSaveDBServer> SelectBuff(
            SchaleDataContext context,
            AccountDBServer account,
            long selectedBuffId)
        {
            // the request names the buff and nothing else, so the run it answers is reached through the group that offers it - two events can be open at once and each can have a run sitting on a popup, and then the newest is not the one being answered
            var groups = _excelService.GetTable<EventContentBuffGroupExcelT>()
                .Where(x => x.EventContentBuffId1 == selectedBuffId || x.EventContentBuffId2 == selectedBuffId || x.EventContentDebuffId == selectedBuffId)
                .Select(x => x.BuffGroupId)
                .ToHashSet();

            var waiting = await context.CampaignMainStageSaves
                .Where(x => x.AccountServerId == account.ServerId && x.IsOpen && x.IsBuffSelectPopupOpen)
                .OrderByDescending(x => x.ServerId)
                .ToListAsync();

            var stageSaveData = waiting.FirstOrDefault(x => groups.Contains(x.CurrentAppearedBuffGroupId));

            if (stageSaveData == null)
                throw new WebAPIException(WebAPIErrorCode.EventContentCannotSelectBuff, "No event stage run is waiting on a buff selection");

            stageSaveData.SelectedBuffDict ??= new Dictionary<long, long>();
            stageSaveData.SelectedBuffDict[stageSaveData.CurrentAppearedBuffGroupId] = selectedBuffId;
            stageSaveData.IsBuffSelectPopupOpen = false;
            stageSaveData.CurrentAppearedBuffGroupId = 0;

            context.CampaignMainStageSaves.Update(stageSaveData);
            await context.SaveChangesAsync();

            return stageSaveData;
        }

        // The client's IsAvailableStageToday reads sweepCount + TodayPlayCount <= TodayPurchasePlayCountHardStage + ConstCommonExcel.HardStageCount, and only on Hard stages, so a bought play raises the purchase counter and nothing else - taking one off TodayPlayCount as well would hand out two plays per purchase.
        public async Task<(AccountCurrencyDBServer Currency, CampaignStageHistoryDBServer History)> PurchasePlayCountHardStage(
            SchaleDataContext context,
            AccountDBServer account,
            long stageUniqueId)
        {
            var stageExcel = _excelService.GetTable<EventContentStageExcelT>().GetEventContentStageId(stageUniqueId);
            if (stageExcel.StageDifficulty != StageDifficulty.Hard)
                throw new WebAPIException(WebAPIErrorCode.CampaignStagePlayLimit, $"Stage {stageUniqueId} has no purchasable plays");

            var historyDb = await context.CampaignStageHistories
                .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.StageUniqueId == stageUniqueId);

            if (historyDb == null)
            {
                historyDb = new CampaignStageHistoryDBServer
                {
                    AccountServerId = account.ServerId,
                    StageUniqueId = stageUniqueId,
                    LastPlay = account.GameSettings.ServerDateTime()
                };
                context.CampaignStageHistories.Add(historyDb);
            }

            var dailyLimit = _excelService.GetTable<ConstCommonExcelT>().First().HardAdventurePlayCountRecoverDailyNumber;
            if (historyDb.TodayPurchasePlayCountHardStage >= dailyLimit)
                throw new WebAPIException(WebAPIErrorCode.CampaignStagePlayLimit, $"Stage {stageUniqueId} has no purchases left today");

            // the price is not a constant anywhere - ServiceActionExcel names the goods row and the goods row is what carries the currency and the amount
            var serviceAction = _excelService.GetTable<ServiceActionExcelT>()
                .FirstOrDefault(x => x.ServiceActionType == ServiceActionType.HardAdventurePlayCountRecover);
            var goods = serviceAction == null
                ? null
                : _excelService.GetTable<GoodsExcelT>().FirstOrDefault(x => x.Id == serviceAction.GoodsId);

            if (goods != null)
            {
                var cost = ParcelResult.ConvertParcelResult(goods.ConsumeParcelType, goods.ConsumeParcelId, goods.ConsumeParcelAmount);
                await _parcelHandler.BuildParcel(context, account, cost, isConsume: true);
            }

            historyDb.TodayPurchasePlayCountHardStage++;

            await context.SaveChangesAsync();

            var currency = await context.Currencies.FirstAsync(x => x.AccountServerId == account.ServerId);

            return (currency, historyDb);
        }

        // Event stages have no per-tactic character exp - EventContentStageExcel carries no TacticRewardExp - so unlike the campaign twin there is nothing to pay until the tactic that clears the stage.
        private async Task<(ParcelResultDB ParcelResult, StrategyClearRewardInfo? ClearReward, List<ParcelInfo> BonusReward)> BuildEventTacticReward(
            SchaleDataContext context,
            AccountDBServer account,
            EventContentStageExcelT? stageExcel,
            long eventContentId,
            CampaignMainStageSaveDBServer save,
            bool isStageClear,
            bool grantFirstClear,
            bool grantThreeStar)
        {
            var firstClear = new List<ParcelResult>();
            var threeStar = new List<ParcelResult>();
            var drops = new List<ParcelResult>();

            if (stageExcel != null && isStageClear)
            {
                (firstClear, threeStar, drops) = StageRewards(
                    stageExcel.EventContentStageRewardId, grantFirstClear, grantThreeStar);
            }

            var granted = firstClear.Concat(drops).Concat(threeStar).ToList();

            // the debuff bonus is a percentage of the event currency this clear just paid, so it has to be measured against the rolled list and granted alongside it in the one BuildParcel call
            var bonus = stageExcel == null
                ? new List<ParcelResult>()
                : DebuffBonus(save, stageExcel, eventContentId, granted);

            var resolver = await _parcelHandler.BuildParcel(context, account, granted.Concat(bonus).ToList());

            if (!isStageClear)
                return (resolver.ParcelResult, null, new List<ParcelInfo>());

            var bonusReward = ToParcelInfos(bonus);

            return (resolver.ParcelResult, new StrategyClearRewardInfo
            {
                FirstClearReward = ToParcelInfos(firstClear),
                ThreeStarReward = ToParcelInfos(threeStar),
                StrategyObjectRewards = new Dictionary<long, List<ParcelInfo>>(),
                EventContentBonusReward = bonusReward,
                ParcelResultDB = resolver.ParcelResult,
            }, bonusReward);
        }

        // One reward group holds every row for the stage and the tag is what separates them: FirstClear and the event's permanent-reward row pay once per account, ThreeStar once when all three stars are lit, and everything else is the per-clear drop table rolled against RewardProb.
        // Rolling the whole group instead pays the once-per-account rows again on every single clear.
        private (List<ParcelResult> FirstClear, List<ParcelResult> ThreeStar, List<ParcelResult> Drops) StageRewards(
            long rewardGroupId,
            bool grantFirstClear,
            bool grantThreeStar)
        {
            var rewards = _excelService.GetTable<EventContentStageRewardExcelT>()
                .GetAllRewardsByGroupId(rewardGroupId)
                .ToList();

            var firstClear = grantFirstClear
                ? rewards.Where(x => x.RewardTag == RewardTag.FirstClear || x.RewardTag == RewardTag.EventPermanentReward).Select(ToParcel).ToList()
                : new List<ParcelResult>();

            var threeStar = grantThreeStar
                ? rewards.Where(x => x.RewardTag == RewardTag.ThreeStar).Select(ToParcel).ToList()
                : new List<ParcelResult>();

            var drops = rewards
                .Where(x => x.RewardTag == RewardTag.Default || x.RewardTag == RewardTag.Rare || x.RewardTag == RewardTag.Event)
                .Where(x => MathService.GenerateProbability(x.RewardProb))
                .Select(ToParcel)
                .ToList();

            return (firstClear, threeStar, drops);
        }

        private static ParcelResult ToParcel(EventContentStageRewardExcelT reward)
            => new(reward.RewardParcelType, reward.RewardId, reward.RewardAmount);

        // One buff group appears per won tactic on the stages whose EventContentStageExcel names a BuffContentId, and it stays up until EventContent_SelectBuff answers it. BuffGroupProb is a uniform 10 on every row in this data version, so the draw is flat across the groups the run has not seen yet.
        private void RollBuffGroup(CampaignMainStageSaveDBServer save, EventContentStageExcelT? stageExcel, long eventContentId)
        {
            if (stageExcel == null || stageExcel.BuffContentId == 0 || save.IsBuffSelectPopupOpen)
                return;

            save.SelectedBuffDict ??= new Dictionary<long, long>();

            var available = _excelService.GetTable<EventContentBuffGroupExcelT>()
                .Where(x => x.EventContentId == eventContentId && x.BuffContentId == stageExcel.BuffContentId)
                .Select(x => x.BuffGroupId)
                .Distinct()
                .Where(x => !save.SelectedBuffDict.ContainsKey(x))
                .ToList();

            if (available.Count == 0)
                return;

            save.CurrentAppearedBuffGroupId = available[Random.Shared.Next(available.Count)];
            save.IsBuffSelectPopupOpen = true;
        }

        // Taking the debuff instead of one of the two buffs pays a percentage of the stage's event currency back on the clear. The percentage row names an EventContentItemType and EventContentCurrencyItemExcel is what turns that into the item id the drops were paid in.
        private List<ParcelResult> DebuffBonus(
            CampaignMainStageSaveDBServer save,
            EventContentStageExcelT stageExcel,
            long eventContentId,
            List<ParcelResult> granted)
        {
            if (save.SelectedBuffDict == null || save.SelectedBuffDict.Count == 0)
                return new List<ParcelResult>();

            var groups = _excelService.GetTable<EventContentBuffGroupExcelT>()
                .Where(x => x.EventContentId == eventContentId && x.BuffContentId == stageExcel.BuffContentId)
                .ToList();

            var tookDebuff = groups.Any(g => save.SelectedBuffDict.TryGetValue(g.BuffGroupId, out var picked) && picked == g.EventContentDebuffId);
            if (!tookDebuff)
                return new List<ParcelResult>();

            var currencyItems = _excelService.GetTable<EventContentCurrencyItemExcelT>()
                .Where(x => x.EventContentId == eventContentId)
                .ToList();

            var bonus = new List<ParcelResult>();

            foreach (var row in _excelService.GetTable<EventContentDebuffRewardExcelT>()
                .Where(x => x.EventContentId == eventContentId && x.EventStageId == stageExcel.Id))
            {
                var itemId = currencyItems.FirstOrDefault(x => x.EventContentItemType == row.EventContentItemType)?.ItemUniqueId ?? 0;
                if (itemId == 0)
                    continue;

                var earned = granted.Where(x => x.Type == ParcelType.Item && x.Id == itemId).Sum(x => x.Amount);
                var amount = earned * row.RewardPercentage / 10000;

                if (amount > 0)
                    bonus.Add(new ParcelResult(ParcelType.Item, itemId, amount));
            }

            return bonus;
        }

        private static bool SameTile(HexLocation2D? a, HexLocation2D? b)
            => a != null && b != null && a.x == b.x && a.y == b.y && a.z == b.z;

        private static List<ParcelInfo> ToParcelInfos(IEnumerable<ParcelResult> parcels)
            => parcels.Select(r => new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = r.Type, Id = r.Id },
                Amount = r.Amount,
                Multiplier = BasisPoint.One,
                Probability = BasisPoint.One
            }).ToList();

        // Advances EVERY event mission whose condition parameters contain the cleared stage (not just the post-event extension-time variant), and marks them Complete when the condition count is met.
        // Without Complete the client never unlocks the next quest in the chain.
        private async Task<Dictionary<long, List<MissionProgressDB>>> ProgressEventMissions(
            SchaleDataContext context,
            AccountDBServer account,
            long eventContentId,
            long stageUniqueId)
        {
            var matchingExcels = _excelService.GetTable<EventContentMissionExcelT>()
                .GetMissionExcelByEventContentId(eventContentId)
                .GetMissionExcelFromConditionParameter(stageUniqueId);

            if (matchingExcels.Count == 0)
                return new Dictionary<long, List<MissionProgressDB>>();

            var progressDBs = new List<MissionProgressDB>();

            foreach (var missionExcel in matchingExcels)
            {
                var existingMission = await context.MissionProgresses
                    .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.MissionUniqueId == missionExcel.Id);

                if (existingMission == null)
                {
                    existingMission = new MissionProgressDBServer
                    {
                        AccountServerId = account.ServerId,
                        MissionUniqueId = missionExcel.Id,
                        StartTime = account.GameSettings.ServerDateTime(),
                        ProgressParameters = new Dictionary<long, long>()
                    };
                    context.MissionProgresses.Add(existingMission);
                }

                existingMission.ProgressParameters ??= new Dictionary<long, long>();
                existingMission.ProgressParameters[stageUniqueId] = 1;

                var conditionCount = Math.Max(1, missionExcel.CompleteConditionCount);
                if (existingMission.ProgressParameters.Values.Sum() >= conditionCount)
                    existingMission.Complete = true;

                progressDBs.Add(existingMission.ToMap(_mapper));
            }

            return new Dictionary<long, List<MissionProgressDB>>
            {
                { eventContentId, progressDBs }
            };
        }

        private void MergeExistHistoryWithNew(
            SchaleDataContext context,
            CampaignStageHistoryDBServer existHistoryDb,
            CampaignStageHistoryDBServer newHistoryDb,
            DateTime dateTime)
        {
            existHistoryDb.Star1Flag = existHistoryDb.Star1Flag || newHistoryDb.Star1Flag;
            existHistoryDb.Star2Flag = existHistoryDb.Star2Flag || newHistoryDb.Star2Flag;
            existHistoryDb.Star3Flag = existHistoryDb.Star3Flag || newHistoryDb.Star3Flag;
            existHistoryDb.IsClearedEver = existHistoryDb.IsClearedEver || newHistoryDb.IsClearedEver;

            // Both are best-ever records and only a run that actually cleared reports one: a defeat leaves them at 0 and must not overwrite what an earlier clear achieved. Fewer turns is better, more S-rank tactics is better.
            if (newHistoryDb.ClearTurnRecord > 0 &&
                (existHistoryDb.ClearTurnRecord == 0 || newHistoryDb.ClearTurnRecord < existHistoryDb.ClearTurnRecord))
            {
                existHistoryDb.ClearTurnRecord = newHistoryDb.ClearTurnRecord;
            }

            if (newHistoryDb.TacticClearCountWithRankSRecord > existHistoryDb.TacticClearCountWithRankSRecord)
                existHistoryDb.TacticClearCountWithRankSRecord = newHistoryDb.TacticClearCountWithRankSRecord;

            existHistoryDb.TodayPlayCount += 1;
            existHistoryDb.LastPlay = dateTime;

            context.CampaignStageHistories.Update(existHistoryDb);
        }

        private bool CheckIfCleared(BattleSummary summary)
        {
            return !summary.IsAbort && summary.EndType == BattleEndType.Clear;
        }

        private void CalcStrategySkipStarGoals(CampaignStageHistoryDBServer historyDB, BattleSummary summary)
        {
            historyDB.Star1Flag = CalcAllEnemiesDefeated(summary);
            historyDB.Star2Flag = CalcAllEnemiesDefeatedInTime(summary);
            historyDB.Star3Flag = CalcAllAlive(summary);
            historyDB.ClearTurnRecord = 1;
        }

        private bool CalcAllEnemiesDefeated(BattleSummary battleSummary)
        {
            if (battleSummary.Group02Summary == null) return false;

            foreach (var enemy in battleSummary.Group02Summary.Heroes)
            {
                if (enemy.DeadFrame == -1) return false;
            }

            return true;
        }

        private bool CalcAllEnemiesDefeatedInTime(BattleSummary battleSummary)
        {
            if (battleSummary.Group02Summary == null) return false;

            var lastEnemy = battleSummary.Group02Summary.Heroes.LastOrDefault();
            if (lastEnemy == null) return false;

            return lastEnemy.DeadFrame <= 120 * 30;
        }

        private bool CalcAllAlive(BattleSummary battleSummary)
        {
            if (battleSummary.Group01Summary == null) return false;

            foreach (var hero in battleSummary.Group01Summary.Heroes)
            {
                if (hero.DeadFrame != -1) return false;
            }

            return true;
        }
    }
}
