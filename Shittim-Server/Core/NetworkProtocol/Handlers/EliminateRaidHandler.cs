using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.Core.Math;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;
using Shittim.Services;
using Shittim_Server.GameClient;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class EliminateRaidHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly EliminateRaidManager _raidManager;
    private readonly ParcelHandler _parcelHandler;

    public EliminateRaidHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        EliminateRaidManager raidManager,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _raidManager = raidManager;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.EliminateRaid_Login)]
    public async Task<EliminateRaidLoginResponse> Login(
        SchaleDataContext db,
        EliminateRaidLoginRequest request,
        EliminateRaidLoginResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_Lobby)]
    public async Task<EliminateRaidLobbyResponse> Lobby(
        SchaleDataContext db,
        EliminateRaidLobbyRequest request,
        EliminateRaidLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raidLobby = await _raidManager.GetUpdatedLobby(db, account);

        response.SeasonType = RaidSeasonType.Open;
        response.RaidLobbyInfoDB = raidLobby.ToMap(_mapper);
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_CreateBattle)]
    public async Task<EliminateRaidCreateBattleResponse> CreateBattle(
        SchaleDataContext db,
        EliminateRaidCreateBattleRequest request,
        EliminateRaidCreateBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        account.ContentInfo.EliminateRaidDataInfo.CurrentRaidUniqueId = request.RaidUniqueId;
        account.ContentInfo.EliminateRaidDataInfo.CurrentDifficulty = _raidManager.GetDifficulty(request.RaidUniqueId);

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
            var assistCharacter = SchaleService.GetAssistCharacter(request.AssistUseInfo.EchelonType)
                .FirstOrDefault(x => x.AssistCharacterServerId == request.AssistUseInfo.CharacterDBId);
            response.AssistCharacterDB = RaidService.FinishingAssistCharacterInfo(assistCharacter, request.AssistUseInfo);
        }

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_EnterBattle)]
    public async Task<EliminateRaidEnterBattleResponse> EnterBattle(
        SchaleDataContext db,
        EliminateRaidEnterBattleRequest request,
        EliminateRaidEnterBattleResponse response)
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
            var assistCharacter = SchaleService.GetAssistCharacter(request.AssistUseInfo.EchelonType)
                .FirstOrDefault(x => x.AssistCharacterServerId == request.AssistUseInfo.CharacterDBId);
            response.AssistCharacterDB = RaidService.FinishingAssistCharacterInfo(assistCharacter, request.AssistUseInfo);
        }

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_EndBattle)]
    public async Task<EliminateRaidEndBattleResponse> EndBattle(
        SchaleDataContext db,
        EliminateRaidEndBattleRequest request,
        EliminateRaidEndBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        bool isCleared = await _raidManager.SaveBattle(db, account, request.Summary, request.IsPractice);

        if (!isCleared)
        {
            account.ContentInfo.EliminateRaidDataInfo.TimeBonus += request.Summary.EndFrame;
            db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await db.SaveChangesAsync();
            response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;
            return response;
        }

        var targetStage = _raidManager.GetRaidStage(account);

        var totalTime = (request.Summary.EndFrame + account.ContentInfo.EliminateRaidDataInfo.TimeBonus) / 30f;
        var timeScore = MathService.CalculateTimeScore(totalTime, targetStage.PerSecondMinusScore);
        var hpPercentScorePoint = targetStage.HPPercentScore;
        var defaultClearPoint = targetStage.DefaultClearScore;

        var rankingPoint = timeScore + hpPercentScorePoint + defaultClearPoint;

        if (!request.IsPractice)
        {
            account.ContentInfo.EliminateRaidDataInfo.BestRankingPoint = rankingPoint > account.ContentInfo.EliminateRaidDataInfo.BestRankingPoint ?
                rankingPoint : account.ContentInfo.EliminateRaidDataInfo.BestRankingPoint;
            account.ContentInfo.EliminateRaidDataInfo.TotalRankingPoint += rankingPoint;
        }
        account.ContentInfo.EliminateRaidDataInfo.TimeBonus = 0;
        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        await _raidManager.ClearBossData(db, account, RaidStatus.Clear, rankingPoint);

        response.RankingPoint = rankingPoint;
        response.BestRankingPoint = account.ContentInfo.EliminateRaidDataInfo.BestRankingPoint;
        response.ClearTimePoint = timeScore;
        response.HPPercentScorePoint = hpPercentScorePoint;
        response.DefaultClearPoint = defaultClearPoint;
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_GiveUp)]
    public async Task<EliminateRaidGiveUpResponse> GiveUp(
        SchaleDataContext db,
        EliminateRaidGiveUpRequest request,
        EliminateRaidGiveUpResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var giveUpRaid = new RaidGiveUpDB
        {
            Ranking = 1,
            RankingPoint = account.ContentInfo.EliminateRaidDataInfo.TotalRankingPoint,
            BestRankingPoint = account.ContentInfo.EliminateRaidDataInfo.BestRankingPoint
        };

        await _raidManager.ClearBossData(db, account, RaidStatus.Close);

        response.RaidGiveUpDB = giveUpRaid;
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_OpponentList)]
    public async Task<EliminateRaidOpponentListResponse> OpponentList(
        SchaleDataContext db,
        EliminateRaidOpponentListRequest request,
        EliminateRaidOpponentListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.OpponentUserDBs = [];
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_RankingIndex)]
    public async Task<EliminateRaidRankingIndexResponse> RankingIndex(
        SchaleDataContext db,
        EliminateRaidRankingIndexRequest request,
        EliminateRaidRankingIndexResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.RankBrackets = [];
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_RankingReward)]
    public async Task<EliminateRaidRankingRewardResponse> RankingReward(
        SchaleDataContext db,
        EliminateRaidRankingRewardRequest request,
        EliminateRaidRankingRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raidLobby = await _raidManager.GetUpdatedLobby(db, account);
        var targetSeason = _excelService.GetTable<EliminateRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == account.ContentInfo.EliminateRaidDataInfo.SeasonId);
        var bracket = _excelService.GetTable<EliminateRaidRankingRewardExcelT>()
            .FirstOrDefault(x => x.RankingRewardGroupId == targetSeason.RankingRewardGroupId && x.RankStart <= raidLobby.Ranking && raidLobby.Ranking <= x.RankEnd);

        response.ReceivedRankingRewardId = raidLobby.ReceivedRankingRewardId;
        if (bracket == null || raidLobby.ReceivedRankingRewardId == bracket.Id)
            return response;

        var parcels = bracket.RewardParcelType
            .Select((type, i) => new ParcelResult(type, bracket.RewardParcelUniqueId[i], bracket.RewardParcelAmount[i]))
            .ToList();
        var resolver = await _parcelHandler.BuildParcel(db, account, parcels);

        raidLobby.ReceivedRankingRewardId = bracket.Id;
        db.EliminateRaidLobbyInfos.Update(raidLobby);
        await db.SaveChangesAsync();

        response.ReceivedRankingRewardId = bracket.Id;
        response.ParcelResultDB = resolver.ParcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_SeasonReward)]
    public async Task<EliminateRaidSeasonRewardResponse> SeasonReward(
        SchaleDataContext db,
        EliminateRaidSeasonRewardRequest request,
        EliminateRaidSeasonRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raidLobby = await _raidManager.GetUpdatedLobby(db, account);
        var targetSeason = _excelService.GetTable<EliminateRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == account.ContentInfo.EliminateRaidDataInfo.SeasonId);

        // the lobby is seeded with every SeasonRewardId already marked received, so this normally settles to an empty payout; the difference set keeps the claim honest if that seeding ever changes
        var unclaimed = targetSeason.SeasonRewardId.Except(raidLobby.ReceiveRewardIds).ToList();
        var seasonRewardExcels = _excelService.GetTable<EliminateRaidStageSeasonRewardExcelT>();

        var parcels = new List<ParcelResult>();
        foreach (var id in unclaimed)
        {
            var row = seasonRewardExcels.FirstOrDefault(x => x.SeasonRewardId == id);
            if (row == null) continue;
            parcels.AddRange(row.SeasonRewardParcelType.Select((type, i) => new ParcelResult(type, row.SeasonRewardParcelUniqueId[i], row.SeasonRewardAmount[i])));
        }

        var resolver = await _parcelHandler.BuildParcel(db, account, parcels);
        raidLobby.ReceiveRewardIds.AddRange(unclaimed);
        db.EliminateRaidLobbyInfos.Update(raidLobby);
        await db.SaveChangesAsync();

        response.ReceiveRewardIds = raidLobby.ReceiveRewardIds;
        response.ParcelResultDB = resolver.ParcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_LimitedReward)]
    public async Task<EliminateRaidLimitedRewardResponse> LimitedReward(
        SchaleDataContext db,
        EliminateRaidLimitedRewardRequest request,
        EliminateRaidLimitedRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raidLobby = await _raidManager.GetUpdatedLobby(db, account);
        var targetSeason = _excelService.GetTable<EliminateRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == account.ContentInfo.EliminateRaidDataInfo.SeasonId);

        var limitedIds = new Dictionary<Difficulty, long>
        {
            { Difficulty.Normal, targetSeason.LimitedRewardIdNormal },
            { Difficulty.Hard, targetSeason.LimitedRewardIdHard },
            { Difficulty.VeryHard, targetSeason.LimitedRewardIdVeryhard },
            { Difficulty.Hardcore, targetSeason.LimitedRewardIdHardcore },
            { Difficulty.Extreme, targetSeason.LimitedRewardIdExtreme },
            { Difficulty.Insane, targetSeason.LimitedRewardIdInsane },
            { Difficulty.Torment, targetSeason.LimitedRewardIdTorment },
        };

        // one claim per difficulty per season; the win summaries are the only record of which difficulties actually got a real clear
        var seasonId = account.ContentInfo.EliminateRaidDataInfo.SeasonId;
        var earned = account.RaidSummaries
            .Where(x => x.ContentType == ContentTypeSummary.EliminateRaid && x.SeasonId == seasonId && x.BattleStatus == BattleStatus.Win && !x.IsMock)
            .Select(x => x.Difficulty).Distinct()
            .Where(limitedIds.ContainsKey).Select(d => limitedIds[d])
            .Where(id => id != 0 && !raidLobby.ReceiveLimitedRewardIds.Contains(id))
            .ToList();

        var limitedRewardExcels = _excelService.GetTable<EliminateRaidStageLimitedRewardExcelT>();
        var parcels = new List<ParcelResult>();
        foreach (var id in earned)
        {
            var row = limitedRewardExcels.FirstOrDefault(x => x.LimitedRewardId == id);
            if (row == null) continue;
            parcels.AddRange(row.LimitedRewardParcelType.Select((type, i) => new ParcelResult(type, row.LimitedRewardParcelUniqueId[i], row.LimitedRewardAmount[i])));
        }

        var resolver = await _parcelHandler.BuildParcel(db, account, parcels);
        raidLobby.ReceiveLimitedRewardIds.AddRange(earned);
        db.EliminateRaidLobbyInfos.Update(raidLobby);
        await db.SaveChangesAsync();

        response.ReceiveRewardIds = raidLobby.ReceiveLimitedRewardIds;
        response.ParcelResultDB = resolver.ParcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_Sweep)]
    public async Task<EliminateRaidSweepResponse> Sweep(
        SchaleDataContext db,
        EliminateRaidSweepRequest request,
        EliminateRaidSweepResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var targetStage = _excelService.GetTable<EliminateRaidStageExcelT>().FirstOrDefault(x => x.Id == request.UniqueId);
        var rewardExcels = _excelService.GetTable<EliminateRaidStageRewardExcelT>();

        var rewards = new List<List<ParcelInfo>>();
        var allDrops = new List<ParcelResult>();
        for (var i = 0; i < request.SweepCount; i++)
        {
            var run = rewardExcels
                .Where(x => x.GroupId == targetStage.RaidRewardGroupId && MathService.GenerateProbability(x.ClearStageRewardProb))
                .Select(x => new ParcelResult(x.ClearStageRewardParcelType, x.ClearStageRewardParcelUniqueID, x.ClearStageRewardAmount))
                .ToList();
            rewards.Add(run.Select(r => new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = r.Type, Id = r.Id },
                Amount = r.Amount,
                Multiplier = BasisPoint.One,
                Probability = BasisPoint.One
            }).ToList());
            allDrops.AddRange(run);
        }

        var resolver = await _parcelHandler.BuildParcel(db, account, allDrops);
        await db.SaveChangesAsync();

        response.TotalSeasonPoint = account.ContentInfo.EliminateRaidDataInfo.TotalRankingPoint;
        response.Rewards = rewards;
        response.ParcelResultDB = resolver.ParcelResult;
        response.ServerTimeTicks = _raidManager.GetRaidTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_GetBestTeam)]
    public async Task<EliminateRaidGetBestTeamResponse> GetBestTeam(
        SchaleDataContext db,
        EliminateRaidGetBestTeamRequest request,
        EliminateRaidGetBestTeamResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.RaidTeamSettingDBsDict = new();

        return response;
    }
}
