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

    public TimeAttackDungeonHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        TimeAttackDungeonManager timeAttackDungeonManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _timeAttackDungeonManager = timeAttackDungeonManager;
        _mapper = mapper;
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
