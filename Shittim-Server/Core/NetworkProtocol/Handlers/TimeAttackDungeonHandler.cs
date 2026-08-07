using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;
using Shittim.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class TimeAttackDungeonHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly TimeAttackDungeonManager _timeAttackDungeonManager;
    private readonly IMapper _mapper;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;

    public TimeAttackDungeonHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        TimeAttackDungeonManager timeAttackDungeonManager,
        IMapper mapper,
        ExcelTableService excelService,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _timeAttackDungeonManager = timeAttackDungeonManager;
        _mapper = mapper;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.TimeAttackDungeon_Sweep)]
    public async Task<TimeAttackDungeonSweepResponse> Sweep(
        SchaleDataContext db,
        TimeAttackDungeonSweepRequest request,
        TimeAttackDungeonSweepResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (request.SweepCount < 1)
            throw new WebAPIException(WebAPIErrorCode.TimeAttackDungeonInvalidRequest, "SweepCount must be positive");

        var info = account.ContentInfo.TimeAttackDungeonDataInfo;
        var season = _excelService.GetTable<TimeAttackDungeonSeasonManageExcelT>()
                .FirstOrDefault(x => x.Id == info.SeasonId)
            ?? throw new WebAPIException(WebAPIErrorCode.TimeAttackDungeonNotOpen, $"Season {info.SeasonId} not found");

        // A sweep replays the best clear; with nothing cleared there is nothing to replay.
        if (info.SeasonBestRecord <= 0)
            throw new WebAPIException(WebAPIErrorCode.TimeAttackDungeonInvalidRequest, "No cleared run to sweep");

        var reward = _excelService.GetTable<TimeAttackDungeonRewardExcelT>()
                .FirstOrDefault(x => x.Id == season.TimeAttackDungeonRewardId)
            ?? throw new WebAPIException(WebAPIErrorCode.TimeAttackDungeonInvalidData, $"Reward {season.TimeAttackDungeonRewardId} not found");

        var currency = db.Currencies.FirstOrDefault(x => x.AccountServerId == account.ServerId);
        if (currency == null
            || !currency.CurrencyDict.TryGetValue(CurrencyTypes.TimeAttackDungeonTicket, out var tickets)
            || tickets < request.SweepCount)
        {
            throw new WebAPIException(WebAPIErrorCode.TimeAttackDungeonInvalidRequest, "Not enough tickets");
        }

        await _parcelHandler.BuildParcel(db, account,
            new ParcelResult(ParcelType.Currency, (long)CurrencyTypes.TimeAttackDungeonTicket, request.SweepCount),
            isConsume: true);

        var columns = ShopHandler.AlignedColumnCount(
            reward.RewardParcelType?.Count, reward.RewardParcelId?.Count, reward.RewardParcelDefaultAmount?.Count);
        // The point ratio scales each parcel between its default and max amounts; the official curve is unknown,
        // bounded here by the excel's own Default..Max so it cannot run away.
        var ratio = reward.RewardMaxPoint > 0
            ? System.Math.Clamp(info.SeasonBestRecord / (double)reward.RewardMaxPoint, 0d, 1d)
            : 0d;

        var perSweep = new List<ParcelInfo>();
        for (int i = 0; i < columns; i++)
        {
            if (reward.RewardMinPoint != null && i < reward.RewardMinPoint.Count
                && info.SeasonBestRecord < reward.RewardMinPoint[i])
            {
                continue;
            }

            var min = reward.RewardParcelDefaultAmount![i];
            var max = (reward.RewardParcelMaxAmount != null && i < reward.RewardParcelMaxAmount.Count)
                ? reward.RewardParcelMaxAmount[i]
                : min;
            var amount = min + (long)((max - min) * ratio);
            if (amount <= 0)
                continue;

            perSweep.Add(new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = reward.RewardParcelType![i], Id = reward.RewardParcelId![i] },
                Amount = amount
            });
        }

        var all = new List<ParcelResult>();
        response.Rewards = [];
        for (int n = 0; n < request.SweepCount; n++)
        {
            response.Rewards.Add(perSweep);
            all.AddRange(perSweep.Select(x => new ParcelResult(x.Key!.Type, x.Key.Id, x.Amount)));
        }

        var resolver = await _parcelHandler.BuildParcel(db, account, all);
        response.ParcelResultDB = resolver.ParcelResult;
        response.RoomDB = _timeAttackDungeonManager.GetRoom(db, account)?.ToMap(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.TimeAttackDungeon_Login)]
    public async Task<TimeAttackDungeonLoginResponse> Login(
        SchaleDataContext db,
        TimeAttackDungeonLoginRequest request,
        TimeAttackDungeonLoginResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ServerTimeTicks = _timeAttackDungeonManager.GetTADTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.TimeAttackDungeon_Lobby)]
    public async Task<TimeAttackDungeonLobbyResponse> Lobby(
        SchaleDataContext db,
        TimeAttackDungeonLobbyRequest request,
        TimeAttackDungeonLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var currentRoom = _timeAttackDungeonManager.GetLobby(db, account);
        var previousRoom = await _timeAttackDungeonManager.GetPreviousRoom(db, account);
        
        response.ServerTimeTicks = _timeAttackDungeonManager.GetTADTimeTicks(account).Ticks;

        if (previousRoom != null)
        {
            response.PreviousRoomDB = previousRoom.ToMap(_mapper);
        }
        
        if (currentRoom != null && currentRoom.Count > 0)
        {
            response.RoomDBs = currentRoom;
        }

        return response;
    }

    [ProtocolHandler(Protocol.TimeAttackDungeon_CreateBattle)]
    public async Task<TimeAttackDungeonCreateBattleResponse> CreateBattle(
        SchaleDataContext db,
        TimeAttackDungeonCreateBattleRequest request,
        TimeAttackDungeonCreateBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var room = await _timeAttackDungeonManager.CreateBattle(db, account, request.IsPractice);

        response.RoomDB = room.ToMap(_mapper);
        response.ServerTimeTicks = _timeAttackDungeonManager.GetTADTimeTicks(account).Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.TimeAttackDungeon_EnterBattle)]
    public async Task<TimeAttackDungeonEnterBattleResponse> EnterBattle(
        SchaleDataContext db,
        TimeAttackDungeonEnterBattleRequest request,
        TimeAttackDungeonEnterBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ServerTimeTicks = _timeAttackDungeonManager.GetTADTimeTicks(account).Ticks;

        if (request.AssistUseInfo != null)
        {
            response.AssistCharacterDB = new AssistCharacterDB
            {
                AccountId = account.ServerId,
                ServerId = request.AssistUseInfo.CharacterDBId,
                UniqueId = 10000,
                SlotNumber = 1,
                Level = 1,
                StarGrade = 3,
                CombatStyleIndex = request.AssistUseInfo.CombatStyleIndex,
                IsMulligan = request.AssistUseInfo.IsMulligan,
                IsTSAInteraction = request.AssistUseInfo.IsTSAInteraction
            };
        }

        return response;
    }

    [ProtocolHandler(Protocol.TimeAttackDungeon_EndBattle)]
    public async Task<TimeAttackDungeonEndBattleResponse> EndBattle(
        SchaleDataContext db,
        TimeAttackDungeonEndBattleRequest request,
        TimeAttackDungeonEndBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ServerTimeTicks = _timeAttackDungeonManager.GetTADTimeTicks(account).Ticks;

        if (request.Summary.EndType != BattleEndType.Clear)
        {
            var room = _timeAttackDungeonManager.GetRoom(db, account);
            if (room != null)
            {
                response.RoomDB = room.ToMap(_mapper);
            }
            return response;
        }

        var targetGeas = _timeAttackDungeonManager.GetTADGeas(request.Summary.StageId);
        var dungeonResult = await _timeAttackDungeonManager.BattleResult(db, account, request.Summary);

        if (targetGeas != null)
        {
            var timePoint = MathService.CalculateTADScore(request.Summary.EndFrame / 30f, targetGeas);
            var totalPoint = targetGeas.ClearDefaultPoint + timePoint;

            response.TotalPoint = totalPoint;
            response.DefaultPoint = targetGeas.ClearDefaultPoint;
            response.TimePoint = timePoint;
        }

        if (dungeonResult != null)
        {
            response.RoomDB = dungeonResult.ToMap(_mapper);
        }

        return response;
    }

    [ProtocolHandler(Protocol.TimeAttackDungeon_GiveUp)]
    public async Task<TimeAttackDungeonGiveUpResponse> GiveUp(
        SchaleDataContext db,
        TimeAttackDungeonGiveUpRequest request,
        TimeAttackDungeonGiveUpResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var room = await _timeAttackDungeonManager.GiveUp(db, account);
        
        if (room != null)
        {
            response.RoomDB = room.ToMap(_mapper);
        }
        
        response.SeasonBestRecord = account.ContentInfo.TimeAttackDungeonDataInfo.SeasonBestRecord;
        response.ServerTimeTicks = _timeAttackDungeonManager.GetTADTimeTicks(account).Ticks;

        return response;
    }
}
