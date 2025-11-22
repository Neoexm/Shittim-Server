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

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class EliminateRaidHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly EliminateRaidManager _raidManager;

    public EliminateRaidHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        EliminateRaidManager raidManager) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _raidManager = raidManager;
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
            var assistCharacter = ShittimService.GetAssistCharacter(request.AssistUseInfo.EchelonType)
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
            var assistCharacter = ShittimService.GetAssistCharacter(request.AssistUseInfo.EchelonType)
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
}
