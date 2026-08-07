using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.MX.NetworkProtocol;
using Shittim.Services;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

// Field story season state lives in the ContentInfo JSON column as one FieldSnapshot per season - the wire model already carries everything the client syncs back, so it doubles as the stored shape.
public class FieldHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ParcelHandler _parcelHandler;

    public FieldHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.Field_Sync)]
    public async Task<FieldSyncResponse> Sync(
        SchaleDataContext db,
        FieldSyncRequest request,
        FieldSyncResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var snapshot = GetSeason(account, request.FieldSeasonId);
        snapshot.ServerTime = account.GameSettings.ServerDateTime();

        var dates = _excelService.GetTable<FieldDateExcelT>()
            .Where(x => x.SeasonId == request.FieldSeasonId)
            .OrderBy(x => x.OpenDate)
            .ToList();
        var endedDates = (snapshot.DateHistoryDBs ?? []).Select(x => x.DateId).ToHashSet();
        response.PlayableDateId = dates.FirstOrDefault(x => !endedDates.Contains(x.UniqueId))?.UniqueId ?? dates.LastOrDefault()?.UniqueId ?? 0;

        var stageIds = _excelService.GetTable<FieldContentStageExcelT>()
            .Where(x => x.SeasonId == request.FieldSeasonId)
            .Select(x => x.Id)
            .ToList();
        response.StageHistoryDBs = db.GetAccountCampaignStageHistories(account.ServerId)
            .Where(x => stageIds.Contains(x.StageUniqueId))
            .ToMapList(_mapper);

        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        response.FieldSnapshot = snapshot;

        return response;
    }

    [ProtocolHandler(Protocol.Field_Interaction)]
    public async Task<FieldInteractionResponse> Interaction(
        SchaleDataContext db,
        FieldInteractionRequest request,
        FieldInteractionResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var now = account.GameSettings.ServerDateTime();

        var excel = _excelService.GetTable<FieldInteractionExcelT>()
            .FirstOrDefault(x => x.FieldSeasonId == request.FieldSeasonId && x.UniqueId == request.UniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"FieldInteraction {request.UniqueId} not in season {request.FieldSeasonId}");

        var snapshot = GetSeason(account, request.FieldSeasonId);
        snapshot.Interactions ??= [];
        var entry = snapshot.Interactions.FirstOrDefault(x => x.UniqueId == request.UniqueId);
        if (entry == null)
        {
            entry = new FieldInteractionDB { SeasonId = request.FieldSeasonId, UniqueId = request.UniqueId };
            snapshot.Interactions.Add(entry);
        }
        entry.UpdateDate = now;

        // Reward-typed steps carry a FieldRewardExcel group in InteractionId; the other step types (scenario, dialog, scene change) run purely client-side
        var parcels = new List<ParcelResult>();
        for (var i = 0; i < excel.InteractionType.Count && i < excel.InteractionId.Count; i++)
        {
            if (excel.InteractionType[i] != FieldInteractionType.Reward)
                continue;
            parcels.AddRange(_excelService.GetTable<FieldRewardExcelT>()
                .Where(x => x.GroupId == excel.InteractionId[i] && MathService.GenerateProbability(x.RewardProb))
                .Select(x => new ParcelResult(x.RewardParcelType, x.RewardId, x.RewardAmount)));
        }
        if (parcels.Count > 0)
        {
            var resolver = await _parcelHandler.BuildParcel(db, account, parcels);
            response.ParcelResultDB = resolver.ParcelResult;
        }

        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        response.InteractionDB = entry;
        response.CharacterDB = snapshot.Character;
        response.MasteryDB = snapshot.Mastery;

        return response;
    }

    [ProtocolHandler(Protocol.Field_QuestClear)]
    public async Task<FieldQuestClearResponse> QuestClear(
        SchaleDataContext db,
        FieldQuestClearRequest request,
        FieldQuestClearResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var now = account.GameSettings.ServerDateTime();

        var excel = _excelService.GetTable<FieldQuestExcelT>()
            .FirstOrDefault(x => x.FieldSeasonId == request.FieldSeasonId && x.UniqueId == request.UniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"FieldQuest {request.UniqueId} not in season {request.FieldSeasonId}");

        var snapshot = GetSeason(account, request.FieldSeasonId);
        var quests = request.IsDaily ? snapshot.DailyQuests ??= [] : snapshot.MainQuests ??= [];
        var quest = quests.FirstOrDefault(x => x.UniqueId == request.UniqueId);
        if (quest == null)
        {
            quest = new FieldQuestDB { SeasonId = request.FieldSeasonId, UniqueId = request.UniqueId, IsDaily = request.IsDaily };
            quests.Add(quest);
        }

        if (!quest.IsComplete)
        {
            quest.IsComplete = true;
            quest.UpdateDate = now;

            var parcels = _excelService.GetTable<FieldRewardExcelT>()
                .Where(x => x.GroupId == excel.RewardId && MathService.GenerateProbability(x.RewardProb))
                .Select(x => new ParcelResult(x.RewardParcelType, x.RewardId, x.RewardAmount))
                .ToList();
            var resolver = await _parcelHandler.BuildParcel(db, account, parcels);
            response.ParcelResultDB = resolver.ParcelResult;
        }

        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        response.Quest = quest;

        return response;
    }

    [ProtocolHandler(Protocol.Field_SceneChanged)]
    public async Task<FieldSceneChangedResponse> SceneChanged(
        SchaleDataContext db,
        FieldSceneChangedRequest request,
        FieldSceneChangedResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var snapshot = GetSeason(account, request.FieldSeasonId);
        snapshot.Character ??= new FieldCharacterDB();
        snapshot.Character.PreviousSceneId = snapshot.Character.CurrentSceneId;
        snapshot.Character.CurrentSceneId = request.SceneId;
        snapshot.Character.WasSceneChanged = true;

        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        response.CharacterDB = snapshot.Character;

        return response;
    }

    [ProtocolHandler(Protocol.Field_EndDate)]
    public async Task<FieldEndDateResponse> EndDate(
        SchaleDataContext db,
        FieldEndDateRequest request,
        FieldEndDateResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var snapshot = GetSeason(account, request.FieldSeasonId);
        snapshot.DateHistoryDBs ??= [];
        var history = snapshot.DateHistoryDBs.FirstOrDefault(x => x.DateId == request.DateId);
        if (history == null)
        {
            history = new FieldDateHistoryDB { DateId = request.DateId, ClearDate = account.GameSettings.ServerDateTime() };
            snapshot.DateHistoryDBs.Add(history);
        }

        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        response.DateHistoryDB = history;

        return response;
    }

    [ProtocolHandler(Protocol.Field_EnterStage)]
    public async Task<FieldEnterStageResponse> EnterStage(
        SchaleDataContext db,
        FieldEnterStageRequest request,
        FieldEnterStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var now = account.GameSettings.ServerDateTime();

        var stage = _excelService.GetTable<FieldContentStageExcelT>()
            .FirstOrDefault(x => x.Id == request.StageUniqueId && x.SeasonId == request.FieldSeasonId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"FieldContentStage {request.StageUniqueId} not in season {request.FieldSeasonId}");

        var save = new FieldStageSaveDB
        {
            AccountServerId = account.ServerId,
            CreateTime = now,
            StageUniqueId = request.StageUniqueId,
            LastEnterStageEchelonNumber = request.LastEnterStageEchelonNumber
        };

        if (stage.StageEnterCostAmount > 0)
        {
            var resolver = await _parcelHandler.BuildParcel(db, account,
                new ParcelResult(stage.StageEnterCostType, stage.StageEnterCostId, stage.StageEnterCostAmount), isConsume: true);
            await db.SaveChangesAsync();
            response.ParcelResultDB = resolver.ParcelResult;
            save.StageEntranceFee = [new ParcelInfo { Key = new ParcelKeyPair { Type = stage.StageEnterCostType, Id = stage.StageEnterCostId }, Amount = stage.StageEnterCostAmount }];
        }

        response.SaveDataDB = save;

        return response;
    }

    [ProtocolHandler(Protocol.Field_StageResult)]
    public async Task<FieldStageResultResponse> StageResult(
        SchaleDataContext db,
        FieldStageResultRequest request,
        FieldStageResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var now = account.GameSettings.ServerDateTime();

        var stage = _excelService.GetTable<FieldContentStageExcelT>()
            .FirstOrDefault(x => x.Id == request.Summary.StageId)
            ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"FieldContentStage {request.Summary.StageId} not found");

        var cleared = !request.Summary.IsAbort && request.Summary.EndType == BattleEndType.Clear;

        var history = db.GetAccountCampaignStageHistories(account.ServerId)
            .FirstOrDefault(x => x.StageUniqueId == stage.Id);
        var firstClear = cleared && (history == null || !history.IsClearedEver);
        if (history == null)
        {
            history = new CampaignStageHistoryDBServer
            {
                AccountServerId = account.ServerId,
                StageUniqueId = stage.Id
            };
            db.CampaignStageHistories.Add(history);
        }
        history.LastPlay = now;
        history.TodayPlayCount += 1;
        if (cleared)
        {
            history.IsClearedEver = true;
            history.Star1Flag = true;
            history.Star2Flag = true;
            history.Star3Flag = true;
            if (history.ClearTurnRecord == 0)
                history.ClearTurnRecord = 1;
        }

        // reward groups key on the stage id; nothing in FieldContentStageExcel names the group explicitly
        var rewardRows = _excelService.GetTable<FieldContentStageRewardExcelT>().Where(x => x.GroupId == stage.Id).ToList();
        if (rewardRows.Count == 0)
            rewardRows = _excelService.GetTable<FieldContentStageRewardExcelT>().Where(x => x.GroupId == stage.GroupId).ToList();

        var parcels = new List<ParcelResult>();
        if (cleared)
        {
            foreach (var row in rewardRows.Where(x => x.RewardTag == RewardTag.Default))
            {
                if (row.RewardProb == 0 || Random.Shared.Next(10000) < row.RewardProb)
                    parcels.Add(new ParcelResult(row.RewardParcelType, row.RewardId, row.RewardAmount));
            }
        }

        if (firstClear)
        {
            var firstClearRows = rewardRows.Where(x => x.RewardTag == RewardTag.FirstClear).ToList();
            parcels.AddRange(firstClearRows.Select(x => new ParcelResult(x.RewardParcelType, x.RewardId, x.RewardAmount)));
            response.FirstClearReward = firstClearRows
                .Select(x => new ParcelInfo { Key = new ParcelKeyPair { Type = x.RewardParcelType, Id = x.RewardId }, Amount = x.RewardAmount })
                .ToList();

            var threeStarRows = rewardRows.Where(x => x.RewardTag == RewardTag.ThreeStar).ToList();
            parcels.AddRange(threeStarRows.Select(x => new ParcelResult(x.RewardParcelType, x.RewardId, x.RewardAmount)));
            response.ThreeStarReward = threeStarRows
                .Select(x => new ParcelInfo { Key = new ParcelKeyPair { Type = x.RewardParcelType, Id = x.RewardId }, Amount = x.RewardAmount })
                .ToList();
        }

        var resolver = await _parcelHandler.BuildParcel(db, account, parcels);
        await db.SaveChangesAsync();

        response.CampaignStageHistoryDB = history.ToMap(_mapper);
        response.LevelUpCharacterDBs = [];
        response.ParcelResultDB = resolver.ParcelResult;

        return response;
    }

    private FieldSnapshot GetSeason(AccountDBServer account, long seasonId)
    {
        var seasons = account.ContentInfo.FieldSeasons;
        var snapshot = seasons.FirstOrDefault(x => x.FieldSeasonId == seasonId);
        if (snapshot == null)
        {
            // spawn the character at the season's entry date scene so the first Sync lands somewhere valid
            var entryScene = 0L;
            var season = _excelService.GetTable<FieldSeasonExcelT>().FirstOrDefault(x => x.UniqueId == seasonId);
            if (season != null)
                entryScene = _excelService.GetTable<FieldDateExcelT>().FirstOrDefault(x => x.SeasonId == seasonId && x.UniqueId == season.EntryDateId)?.EntrySceneId ?? 0;

            snapshot = new FieldSnapshot
            {
                FieldSeasonId = seasonId,
                AccountId = account.ServerId,
                Character = new FieldCharacterDB { CurrentSceneId = entryScene },
                Mastery = new FieldMasteryDB { Level = 1 },
                DateHistoryDBs = [],
                Interactions = [],
                MainQuests = [],
                DailyQuests = []
            };
            seasons.Add(snapshot);
        }
        return snapshot;
    }
}
