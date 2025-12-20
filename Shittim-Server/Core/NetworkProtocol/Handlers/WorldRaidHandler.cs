using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class WorldRaidHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly WorldRaidManager _worldRaidManager;
    private readonly IMapper _mapper;

    public WorldRaidHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        WorldRaidManager worldRaidManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _worldRaidManager = worldRaidManager;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.WorldRaid_Lobby)]
    public async Task<WorldRaidLobbyResponse> Lobby(
        SchaleDataContext db,
        WorldRaidLobbyRequest request,
        WorldRaidLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var localBoss = await _worldRaidManager.WorldRaidLobby(db, account, request);

        response.ClearHistoryDBs = db.GetAccountWorldRaidClearHistories(account.ServerId)
            .Where(x => x.SeasonId == request.SeasonId)
            .ToMapList(_mapper);
        response.LocalBossDBs = localBoss.ToMapList(_mapper);
        response.BossGroups = [];

        return response;
    }

    [ProtocolHandler(Protocol.WorldRaid_BossList)]
    public async Task<WorldRaidBossListResponse> BossList(
        SchaleDataContext db,
        WorldRaidBossListRequest request,
        WorldRaidBossListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var bossList = await _worldRaidManager.GetBossList(db, account, request);

        response.BossListInfoDBs = bossList.ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.WorldRaid_EnterBattle)]
    public async Task<WorldRaidEnterBattleResponse> EnterBattle(
        SchaleDataContext db,
        WorldRaidEnterBattleRequest request,
        WorldRaidEnterBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var raidBattle = await _worldRaidManager.EnterBattle(db, account, request);

        response.RaidBattleDB = raidBattle.ToMap(_mapper);

        if (request.AssistUseInfos != null && request.AssistUseInfos.Count > 0)
        {
            var assistInfo = request.AssistUseInfos[0];
            response.AssistCharacterDBs = [new AssistCharacterDB
            {
                AccountId = account.ServerId,
                ServerId = assistInfo.CharacterDBId,
                UniqueId = 10000,
                SlotNumber = 1,
                Level = 1,
                StarGrade = 3,
                CombatStyleIndex = assistInfo.CombatStyleIndex,
                IsMulligan = assistInfo.IsMulligan,
                IsTSAInteraction = assistInfo.IsTSAInteraction
            }];
        }

        return response;
    }

    [ProtocolHandler(Protocol.WorldRaid_BattleResult)]
    public async Task<WorldRaidBattleResultResponse> BattleResult(
        SchaleDataContext db,
        WorldRaidBattleResultRequest request,
        WorldRaidBattleResultResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var parcelResultDB = await _worldRaidManager.BattleResult(db, account, request);

        response.ParcelResultDB = parcelResultDB ?? new();

        return response;
    }

    [ProtocolHandler(Protocol.WorldRaid_ReceiveReward)]
    public async Task<WorldRaidReceiveRewardResponse> ReceiveReward(
        SchaleDataContext db,
        WorldRaidReceiveRewardRequest request,
        WorldRaidReceiveRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }
}
