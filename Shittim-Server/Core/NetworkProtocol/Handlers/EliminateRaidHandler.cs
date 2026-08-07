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

    [ProtocolHandler(Protocol.EliminateRaid_GetBestTeam)]
    public async Task<EliminateRaidGetBestTeamResponse> GetBestTeam(
        SchaleDataContext db,
        EliminateRaidGetBestTeamRequest request,
        EliminateRaidGetBestTeamResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var lobby = await _raidManager.GetUpdatedLobby(db, account);
        var stageExcels = _excelService.GetTable<EliminateRaidStageExcelT>();

        var teamsByBossGroup = new Dictionary<string, List<RaidTeamSettingDB>>();
        var summariesByGroup = account.RaidSummaries
            .Where(x => x.ContentType == ContentTypeSummary.EliminateRaid &&
                        !x.IsMock &&
                        x.SeasonId == account.ContentInfo.EliminateRaidDataInfo.SeasonId)
            .GroupBy(x => stageExcels.FirstOrDefault(s => s.Id == x.RaidStageId)?.RaidBossGroup)
            .Where(g => g.Key != null);

        foreach (var group in summariesByGroup)
        {
            var best = group.OrderByDescending(x => x.Score).First();
            var groupIndex = Math.Max(0, lobby.OpenedBossGroups.IndexOf(group.Key!));
            teamsByBossGroup[group.Key!] = RaidService.BuildBestTeams(
                db, best, account.ServerId, EchelonType.EliminateRaid01 + groupIndex);
        }

        response.RaidTeamSettingDBsDict = teamsByBossGroup;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_SeasonReward)]
    public async Task<EliminateRaidSeasonRewardResponse> SeasonReward(
        SchaleDataContext db,
        EliminateRaidSeasonRewardRequest request,
        EliminateRaidSeasonRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var lobby = await _raidManager.GetUpdatedLobby(db, account);
        var season = _excelService.GetTable<EliminateRaidSeasonManageExcelT>()
            .FirstOrDefault(x => x.SeasonId == account.ContentInfo.EliminateRaidDataInfo.SeasonId);
        if (season == null)
            throw new WebAPIException(WebAPIErrorCode.RaidExcelDataNotFound, $"Eliminate raid season {account.ContentInfo.EliminateRaidDataInfo.SeasonId} not found");

        var gauge = Math.Min(account.ContentInfo.EliminateRaidDataInfo.TotalRankingPoint, season.MaxSeasonRewardGauage);
        var claimable = RaidService.ClaimableSeasonRewardIds(
            season.SeasonRewardId, season.StackedSeasonRewardGauge, gauge, lobby.ReceiveRewardIds);

        if (claimable.Count == 0)
        {
            response.ReceiveRewardIds = lobby.ReceiveRewardIds;
            return response;
        }

        var rewards = _excelService.GetTable<EliminateRaidStageSeasonRewardExcelT>()
            .Where(x => claimable.Contains(x.SeasonRewardId))
            .SelectMany(x => RaidService.ZipParcelColumns(x.SeasonRewardParcelType, x.SeasonRewardParcelUniqueId, x.SeasonRewardAmount))
            .ToList();

        lobby.ReceiveRewardIds.AddRange(claimable);
        db.EliminateRaidLobbyInfos.Update(lobby);

        var parcelResult = await _parcelHandler.BuildParcel(db, account, rewards);
        await db.SaveChangesAsync();

        response.ReceiveRewardIds = lobby.ReceiveRewardIds;
        response.ParcelResultDB = parcelResult.ParcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.EliminateRaid_RankingReward)]
    public async Task<EliminateRaidRankingRewardResponse> RankingReward(
        SchaleDataContext db,
        EliminateRaidRankingRewardRequest request,
        EliminateRaidRankingRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var lobby = await _raidManager.GetUpdatedLobby(db, account);
        if (lobby.ReceivedRankingRewardId != 0)
            throw new WebAPIException(WebAPIErrorCode.RaidSeasonAlreadyReceiveReward, "Ranking reward already received this season");

        var season = _excelService.GetTable<EliminateRaidSeasonManageExcelT>()
            .FirstOrDefault(x => x.SeasonId == account.ContentInfo.EliminateRaidDataInfo.SeasonId);
        if (season == null)
            throw new WebAPIException(WebAPIErrorCode.RaidExcelDataNotFound, $"Eliminate raid season {account.ContentInfo.EliminateRaidDataInfo.SeasonId} not found");

        var rows = _excelService.GetTable<EliminateRaidRankingRewardExcelT>()
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
        db.EliminateRaidLobbyInfos.Update(lobby);

        var parcelResult = await _parcelHandler.BuildParcel(db, account, parcels);
        await db.SaveChangesAsync();

        response.ReceivedRankingRewardId = row.Id;
        response.ParcelResultDB = parcelResult.ParcelResult;

        return response;
    }

}
