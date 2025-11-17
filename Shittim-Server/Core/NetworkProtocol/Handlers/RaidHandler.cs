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

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class RaidHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly RaidManager _raidManager;

    public RaidHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        RaidManager raidManager) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _raidManager = raidManager;
    }

    [ProtocolHandler(Protocol.Raid_Login)]
    public async Task<RaidLoginResponse> Login(
        SchaleDataContext db,
        RaidLoginRequest request,
        RaidLoginResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

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
                ShittimService.GetAssistCharacter(request.AssistUseInfo.EchelonType)
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
                ShittimService.GetAssistCharacter(request.AssistUseInfo.EchelonType)
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
}
