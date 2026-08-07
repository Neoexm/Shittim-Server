using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.GameLogic.DBModel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;
using Shittim.Services;
using Shittim_Server.GameClient;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class RaidHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly RaidManager _raidManager;
    private readonly ParcelHandler _parcelHandler;

    public RaidHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        RaidManager raidManager,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _raidManager = raidManager;
        _parcelHandler = parcelHandler;
    }

    // No excel column caps a sweep; the ceiling only has to stop a crafted count from looping unboundedly.
    private const long MaxSweepPerRequest = 100;

    [ProtocolHandler(Protocol.Raid_Login)]
    public async Task<RaidLoginResponse> Login(
        SchaleDataContext db,
        RaidLoginRequest request,
        RaidLoginResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Raid_CompleteList)]
    public async Task<RaidCompleteListResponse> CompleteList(
        SchaleDataContext db,
        RaidCompleteListRequest request,
        RaidCompleteListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.RaidDBs = db.GetAccountRaids(account.ServerId).ToMapList(_mapper);
        response.StackedDamage = 0;
        response.ReceiveRewardId = [];
        response.CurSeasonUniqueId = account.ContentInfo.RaidDataInfo.SeasonId;

        return response;
    }

    [ProtocolHandler(Protocol.Raid_Lobby)]
    public async Task<RaidLobbyResponse> Lobby(
        SchaleDataContext db,
        RaidLobbyRequest request,
        RaidLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raidLobby = await _raidManager.GetUpdatedLobby(db, account);

        response.SeasonType = RaidSeasonType.Open;
        response.RaidLobbyInfoDB = raidLobby.ToMap(_mapper);
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.Raid_CreateBattle)]
    public async Task<RaidCreateBattleResponse> CreateBattle(
        SchaleDataContext db,
        RaidCreateBattleRequest request,
        RaidCreateBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        account.ContentInfo.RaidDataInfo.CurrentRaidUniqueId = request.RaidUniqueId;
        account.ContentInfo.RaidDataInfo.CurrentDifficulty = request.Difficulty;

        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        var raidDB = await _raidManager.CreateOrUpdateRaid(db, account, request.IsPractice, request.RaidUniqueId);
        var raidBattleDB = await _raidManager.CreateOrUpdateBattle(db, account, request.RaidUniqueId);

        response.RaidDB = raidDB.ToMap(_mapper);
        response.RaidBattleDB = raidBattleDB.ToMap(_mapper);
        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper);
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;
        response.MissionProgressDBs = [];

        if (request.AssistUseInfo != null)
        {
            response.AssistCharacterDB = RaidService.FinishingAssistCharacterInfo(
                SchaleService.GetAssistCharacter(request.AssistUseInfo.EchelonType)
                    .FirstOrDefault(x => x.AssistCharacterServerId == request.AssistUseInfo.CharacterDBId),
                request.AssistUseInfo);
        }

        return response;
    }

    [ProtocolHandler(Protocol.Raid_EnterBattle)]
    public async Task<RaidEnterBattleResponse> EnterBattle(
        SchaleDataContext db,
        RaidEnterBattleRequest request,
        RaidEnterBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raidDB = _raidManager.GetRaidData(db, account);
        var raidBattleDB = _raidManager.GetRaidBattleData(db, account);

        response.RaidDB = raidDB.ToMap(_mapper);
        response.RaidBattleDB = raidBattleDB.ToMap(_mapper);
        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper);
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;
        response.MissionProgressDBs = [];

        if (request.AssistUseInfo != null)
        {
            response.AssistCharacterDB = RaidService.FinishingAssistCharacterInfo(
                SchaleService.GetAssistCharacter(request.AssistUseInfo.EchelonType)
                    .FirstOrDefault(x => x.AssistCharacterServerId == request.AssistUseInfo.CharacterDBId),
                request.AssistUseInfo);
        }

        return response;
    }

    [ProtocolHandler(Protocol.Raid_EndBattle)]
    public async Task<RaidEndBattleResponse> EndBattle(
        SchaleDataContext db,
        RaidEndBattleRequest request,
        RaidEndBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        bool isCleared = await _raidManager.SaveBattle(db, account, request.Summary, request.IsPractice);

        if (!isCleared)
        {
            account.ContentInfo.RaidDataInfo.TimeBonus += request.Summary.EndFrame;
            db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await db.SaveChangesAsync();
            response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;
            return response;
        }

        var targetStage = _raidManager.GetRaidStage(account);

        var totalTime = (request.Summary.EndFrame + account.ContentInfo.RaidDataInfo.TimeBonus) / 30f;
        var timeScore = MathService.CalculateTimeScore(totalTime, targetStage.PerSecondMinusScore);
        var hpPercentScorePoint = targetStage.HPPercentScore;
        var defaultClearPoint = targetStage.DefaultClearScore;

        var rankingPoint = timeScore + hpPercentScorePoint + defaultClearPoint;

        if (!request.IsPractice)
        {
            account.ContentInfo.RaidDataInfo.BestRankingPoint = rankingPoint > account.ContentInfo.RaidDataInfo.BestRankingPoint ?
                rankingPoint : account.ContentInfo.RaidDataInfo.BestRankingPoint;
            account.ContentInfo.RaidDataInfo.TotalRankingPoint += rankingPoint;
        }
        account.ContentInfo.RaidDataInfo.TimeBonus = 0;
        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        await _raidManager.EndBossBattle(db, account, RaidStatus.Clear, rankingPoint);

        response.RankingPoint = rankingPoint;
        response.BestRankingPoint = account.ContentInfo.RaidDataInfo.BestRankingPoint;
        response.ClearTimePoint = timeScore;
        response.HPPercentScorePoint = hpPercentScorePoint;
        response.DefaultClearPoint = defaultClearPoint;
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.Raid_GiveUp)]
    public async Task<RaidGiveUpResponse> GiveUp(
        SchaleDataContext db,
        RaidGiveUpRequest request,
        RaidGiveUpResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var giveUpRaid = new RaidGiveUpDB
        {
            Ranking = 1,
            RankingPoint = account.ContentInfo.RaidDataInfo.TotalRankingPoint,
            BestRankingPoint = account.ContentInfo.RaidDataInfo.BestRankingPoint
        };

        await _raidManager.EndBossBattle(db, account, RaidStatus.Close);

        response.RaidGiveUpDB = giveUpRaid;
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.Raid_OpponentList)]
    public async Task<RaidOpponentListResponse> OpponentList(
        SchaleDataContext db,
        RaidOpponentListRequest request,
        RaidOpponentListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.OpponentUserDBs = [];
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.Raid_RankingIndex)]
    public async Task<RaidRankingIndexResponse> RankingIndex(
        SchaleDataContext db,
        RaidRankingIndexRequest request,
        RaidRankingIndexResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.RankBrackets = [];
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.Raid_List)]
    public async Task<RaidListResponse> List(
        SchaleDataContext db,
        RaidListRequest request,
        RaidListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raid = _raidManager.GetRaidData(db, account);

        response.CreateRaidDBs = [];
        response.EnterRaidDBs = raid != null ? [raid.ToMap(_mapper)] : [];
        response.ListRaidDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Raid_Search)]
    public async Task<RaidSearchResponse> Search(
        SchaleDataContext db,
        RaidSearchRequest request,
        RaidSearchResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raid = _raidManager.GetRaidData(db, account);
        if (raid == null)
            throw new WebAPIException(WebAPIErrorCode.RaidSearchNotFound, "No raid in progress to search");

        response.RaidDBs = [raid.ToMap(_mapper)];

        return response;
    }

    [ProtocolHandler(Protocol.Raid_Share)]
    public async Task<RaidShareResponse> Share(
        SchaleDataContext db,
        RaidShareRequest request,
        RaidShareResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raid = db.Raids.FirstOrDefault(x =>
            x.ServerId == request.RaidServerId &&
            x.AccountServerId == account.ServerId);
        if (raid == null)
            throw new WebAPIException(WebAPIErrorCode.RaidShareNotFound, $"Raid {request.RaidServerId} not found");

        response.RaidDB = raid.ToMap(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Raid_Detail)]
    public async Task<RaidDetailResponse> Detail(
        SchaleDataContext db,
        RaidDetailRequest request,
        RaidDetailResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raid = db.Raids.FirstOrDefault(x =>
            x.ServerId == request.RaidServerId &&
            x.AccountServerId == account.ServerId);
        if (raid == null)
            throw new WebAPIException(WebAPIErrorCode.RaidDBDataNotFound, $"Raid {request.RaidServerId} not found");

        var contentType = raid.ContentType;
        var battle = db.RaidBattles.FirstOrDefault(x =>
            x.AccountServerId == account.ServerId &&
            x.ContentType == contentType &&
            x.RaidUniqueId == raid.UniqueId);
        var damage = battle?.RaidMembers.FirstOrDefault()?.DamageCollection?.Sum(x => x.GivenDamage) ?? 0;

        response.RaidDetailDB = new RaidDetailDB
        {
            RaidUniqueId = raid.UniqueId,
            EndDate = raid.End,
            DamageTable =
            [
                new RaidPlayerInfoDB
                {
                    RaidServerId = raid.ServerId,
                    AccountId = account.ServerId,
                    JoinDate = raid.Begin,
                    DamageAmount = damage,
                    RaidPlayCount = 1,
                    Nickname = account.Nickname,
                    CharacterId = account.RepresentCharacterServerId,
                    AccountLevel = account.Level
                }
            ]
        };
        response.ParticipateCharacterServerIds =
            raid.ParticipateCharacterServerIds.TryGetValue(account.ServerId, out var characterIds) ? characterIds : [];

        return response;
    }

    [ProtocolHandler(Protocol.Raid_BattleUpdate)]
    public async Task<RaidBattleUpdateResponse> BattleUpdate(
        SchaleDataContext db,
        RaidBattleUpdateRequest request,
        RaidBattleUpdateResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raid = _raidManager.GetRaidData(db, account);
        var battle = _raidManager.GetRaidBattleData(db, account);
        if (raid == null || battle == null)
            throw new WebAPIException(WebAPIErrorCode.RaidBattleNotFound, "No raid battle in progress");
        if (request.RaidBossIndex < 0 || request.RaidBossIndex >= raid.RaidBossDBs.Count)
            throw new WebAPIException(WebAPIErrorCode.RaidBattleUpdateFail, $"Boss index {request.RaidBossIndex} out of range");

        // Snapshot only; EndBattle's SaveBattle stays authoritative for raid.RaidBossDBs.
        battle.RaidBossIndex = request.RaidBossIndex;
        battle.CurrentBossHP = Math.Max(0, raid.RaidBossDBs[request.RaidBossIndex].BossCurrentHP - request.CumulativeDamage);
        battle.CurrentBossGroggy = request.CumulativeGroggyPoint;
        db.RaidBattles.Update(battle);
        await db.SaveChangesAsync();

        response.RaidBattleDB = battle.ToMap(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Raid_Reward)]
    public async Task<RaidRewardResponse> Reward(
        SchaleDataContext db,
        RaidRewardRequest request,
        RaidRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raid = db.Raids.FirstOrDefault(x =>
            x.ServerId == request.RaidServerId &&
            x.AccountServerId == account.ServerId &&
            x.ContentType == ContentType.Raid);
        if (raid == null || raid.IsPractice || request.IsPractice)
            throw new WebAPIException(WebAPIErrorCode.RaidRewardDataNotFound, $"No rewardable raid {request.RaidServerId}");
        if (raid.IsRewardReceived)
            throw new WebAPIException(WebAPIErrorCode.RaidEndRewardFlagError, $"Raid {request.RaidServerId} reward already received");
        // The dead-boss check rather than RaidState: a raid row exists from the moment a battle is created,
        // and give-ups leave it rewardable if only the state is trusted.
        if (!RaidService.IsCleared(raid))
            throw new WebAPIException(WebAPIErrorCode.RaidRewardDataNotFound, $"Raid {request.RaidServerId} was not cleared");

        var stage = _excelService.GetTable<RaidStageExcelT>().FirstOrDefault(x => x.Id == raid.UniqueId);
        if (stage == null)
            throw new WebAPIException(WebAPIErrorCode.RaidExcelDataNotFound, $"Raid stage {raid.UniqueId} not found");

        var rewards = RaidService.RollStageRewards(
            _excelService.GetTable<RaidStageRewardExcelT>().Where(x => x.GroupId == stage.RaidRewardGroupId));

        raid.IsRewardReceived = true;
        db.Raids.Update(raid);

        var parcelResult = await _parcelHandler.BuildParcel(db, account, rewards);
        await db.SaveChangesAsync();

        response.RankingPoint = account.ContentInfo.RaidDataInfo.TotalRankingPoint;
        response.BestRankingPoint = account.ContentInfo.RaidDataInfo.BestRankingPoint;
        response.ParcelResultDB = parcelResult.ParcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.Raid_RewardAll)]
    public async Task<RaidRewardAllResponse> RewardAll(
        SchaleDataContext db,
        RaidRewardAllRequest request,
        RaidRewardAllResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raids = db.Raids.Where(x =>
            x.AccountServerId == account.ServerId &&
            x.ContentType == ContentType.Raid &&
            !x.IsPractice &&
            !x.IsRewardReceived &&
            (x.RaidState == RaidStatus.Clear || x.RaidState == RaidStatus.Close)).ToList()
            .Where(RaidService.IsCleared).ToList();

        var stageExcels = _excelService.GetTable<RaidStageExcelT>();
        var rewardExcels = _excelService.GetTable<RaidStageRewardExcelT>();

        var allRewards = new List<ParcelResult>();
        foreach (var raid in raids)
        {
            var stage = stageExcels.FirstOrDefault(x => x.Id == raid.UniqueId);
            if (stage == null) continue;

            allRewards.AddRange(RaidService.RollStageRewards(
                rewardExcels.Where(x => x.GroupId == stage.RaidRewardGroupId)));
            raid.IsRewardReceived = true;
            db.Raids.Update(raid);
        }

        var parcelResult = await _parcelHandler.BuildParcel(db, account, allRewards);
        await db.SaveChangesAsync();

        response.ParcelResultDB = parcelResult.ParcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.Raid_SeasonReward)]
    public async Task<RaidSeasonRewardResponse> SeasonReward(
        SchaleDataContext db,
        RaidSeasonRewardRequest request,
        RaidSeasonRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var lobby = await _raidManager.GetUpdatedLobby(db, account);
        var season = _excelService.GetTable<RaidSeasonManageExcelT>()
            .FirstOrDefault(x => x.SeasonId == account.ContentInfo.RaidDataInfo.SeasonId);
        if (season == null)
            throw new WebAPIException(WebAPIErrorCode.RaidExcelDataNotFound, $"Raid season {account.ContentInfo.RaidDataInfo.SeasonId} not found");

        var gauge = Math.Min(account.ContentInfo.RaidDataInfo.TotalRankingPoint, season.MaxSeasonRewardGauage);
        var claimable = RaidService.ClaimableSeasonRewardIds(
            season.SeasonRewardId, season.StackedSeasonRewardGauge, gauge, lobby.ReceiveRewardIds);

        if (claimable.Count == 0)
        {
            response.ReceiveRewardIds = lobby.ReceiveRewardIds;
            return response;
        }

        var rewards = _excelService.GetTable<RaidStageSeasonRewardExcelT>()
            .Where(x => claimable.Contains(x.SeasonRewardId))
            .SelectMany(x => RaidService.ZipParcelColumns(x.SeasonRewardParcelType, x.SeasonRewardParcelUniqueId, x.SeasonRewardAmount))
            .ToList();

        lobby.ReceiveRewardIds.AddRange(claimable);
        db.SingleRaidLobbyInfos.Update(lobby);

        var parcelResult = await _parcelHandler.BuildParcel(db, account, rewards);
        await db.SaveChangesAsync();

        response.ReceiveRewardIds = lobby.ReceiveRewardIds;
        response.ParcelResultDB = parcelResult.ParcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.Raid_RankingReward)]
    public async Task<RaidRankingRewardResponse> RankingReward(
        SchaleDataContext db,
        RaidRankingRewardRequest request,
        RaidRankingRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var lobby = await _raidManager.GetUpdatedLobby(db, account);
        if (lobby.ReceivedRankingRewardId != 0)
            throw new WebAPIException(WebAPIErrorCode.RaidSeasonAlreadyReceiveReward, "Ranking reward already received this season");

        var season = _excelService.GetTable<RaidSeasonManageExcelT>()
            .FirstOrDefault(x => x.SeasonId == account.ContentInfo.RaidDataInfo.SeasonId);
        if (season == null)
            throw new WebAPIException(WebAPIErrorCode.RaidExcelDataNotFound, $"Raid season {account.ContentInfo.RaidDataInfo.SeasonId} not found");

        var rows = _excelService.GetTable<RaidRankingRewardExcelT>()
            .Where(x => x.RankingRewardGroupId == season.RankingRewardGroupId)
            .ToList();
        // Solo server: everyone is rank 1. Regional data may leave the base pair 0, hence the Global fallback.
        var row = rows.FirstOrDefault(x => x.RankStart <= 1 && 1 <= x.RankEnd)
               ?? rows.FirstOrDefault(x => x.RankStartGlobal <= 1 && 1 <= x.RankEndGlobal);
        if (row == null)
            throw new WebAPIException(WebAPIErrorCode.RaidRankingNotFound, $"No rank-1 reward row in group {season.RankingRewardGroupId}");

        var parcels = RaidService.ZipParcelColumns(row.RewardParcelType, row.RewardParcelUniqueId, row.RewardParcelAmount);
        lobby.ReceivedRankingRewardId = row.Id;
        lobby.CanReceiveRankingReward = false;
        db.SingleRaidLobbyInfos.Update(lobby);

        var parcelResult = await _parcelHandler.BuildParcel(db, account, parcels);
        await db.SaveChangesAsync();

        response.ReceivedRankingRewardId = row.Id;
        response.ParcelResultDB = parcelResult.ParcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.Raid_Sweep)]
    public async Task<RaidSweepResponse> Sweep(
        SchaleDataContext db,
        RaidSweepRequest request,
        RaidSweepResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (request.SweepCount < 1)
            throw new WebAPIException(WebAPIErrorCode.RaidRewardDataNotFound, "SweepCount must be positive");

        var sweepCount = Math.Min(request.SweepCount, MaxSweepPerRequest);
        var stage = _excelService.GetTable<RaidStageExcelT>().FirstOrDefault(x => x.Id == request.UniqueId);
        if (stage == null)
            throw new WebAPIException(WebAPIErrorCode.RaidExcelDataNotFound, $"Raid stage {request.UniqueId} not found");

        // A sweep replays a stage the account has already beaten; without this it mints the drop table of any
        // stage, at any difficulty, for free.
        var cleared = db.Raids
            .Where(x => x.AccountServerId == account.ServerId
                && x.UniqueId == request.UniqueId
                && x.ContentType == ContentType.Raid
                && !x.IsPractice)
            .ToList()
            .Any(RaidService.IsCleared);
        if (!cleared)
            throw new WebAPIException(WebAPIErrorCode.RaidRewardDataNotFound, $"Raid stage {request.UniqueId} was never cleared");

        var rewardRows = _excelService.GetTable<RaidStageRewardExcelT>()
            .Where(x => x.GroupId == stage.RaidRewardGroupId)
            .ToList();

        var rewards = new List<List<ParcelInfo>>();
        var allParcels = new List<ParcelResult>();
        for (long i = 0; i < sweepCount; i++)
        {
            var rolled = RaidService.RollStageRewards(rewardRows);
            rewards.Add(RaidService.ToParcelInfos(rolled));
            allParcels.AddRange(rolled);
        }

        var parcelResult = await _parcelHandler.BuildParcel(db, account, allParcels);

        var lobby = await _raidManager.GetUpdatedLobby(db, account);
        lobby.SweepPointByRaidUniqueId.TryGetValue(request.UniqueId, out var swept);
        lobby.SweepPointByRaidUniqueId[request.UniqueId] = swept + sweepCount;
        db.SingleRaidLobbyInfos.Update(lobby);
        await db.SaveChangesAsync();

        response.TotalSeasonPoint = account.ContentInfo.RaidDataInfo.TotalRankingPoint;
        response.Rewards = rewards;
        response.ParcelResultDB = parcelResult.ParcelResult;

        return response;
    }

}
