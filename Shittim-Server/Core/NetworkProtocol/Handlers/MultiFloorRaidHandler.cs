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

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MultiFloorRaidHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;

    public static DateTime MultiFloorRaidDateTime;

    public MultiFloorRaidHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
    }

    private DateTime GetMultiFloorRaidDateTime(AccountDBServer account)
    {
        var seasonExcels = _excelService.GetTable<MultiFloorRaidSeasonManageExcelT>();
        var seasonData = seasonExcels.FirstOrDefault(x => x.SeasonId == account.ContentInfo.MultiFloorRaidDataInfo.SeasonId);
        
        if (seasonData != null && !string.IsNullOrEmpty(seasonData.SeasonStartDate))
        {
            return DateTime.Parse(seasonData.SeasonStartDate).AddDays(3);
        }
        
        return account.GameSettings.ServerDateTime();
    }

    [ProtocolHandler(Protocol.MultiFloorRaid_Sync)]
    public async Task<MultiFloorRaidSyncResponse> Sync(
        SchaleDataContext db,
        MultiFloorRaidSyncRequest request,
        MultiFloorRaidSyncResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        MultiFloorRaidDateTime = GetMultiFloorRaidDateTime(account);
        var multiFloorRaidDateTime = MultiFloorRaidDateTime;

        var existingRaid = await db.GetAccountMultiFloorRaids(account.ServerId)
            .FirstOrDefaultAsync(x => x.SeasonId == account.ContentInfo.MultiFloorRaidDataInfo.SeasonId);

        if (existingRaid == null)
        {
            existingRaid = new MultiFloorRaidDBServer
            {
                AccountServerId = account.ServerId,
                SeasonId = account.ContentInfo.MultiFloorRaidDataInfo.SeasonId,
                ClearedDifficulty = 124,
                RewardDifficulty = 124,
                LastClearDate = multiFloorRaidDateTime,
                LastRewardDate = multiFloorRaidDateTime,
                TotalReceivedRewards = new List<ParcelInfo>(),
                TotalReceivableRewards = new List<ParcelInfo>()
            };
            db.MultiFloorRaids.Add(existingRaid);
            await db.SaveChangesAsync();
        }

        response.MultiFloorRaidDBs = [existingRaid.ToMap(_mapper)];
        response.ServerTimeTicks = multiFloorRaidDateTime.Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.MultiFloorRaid_EnterBattle)]
    public async Task<MultiFloorRaidEnterBattleResponse> EnterBattle(
        SchaleDataContext db,
        MultiFloorRaidEnterBattleRequest request,
        MultiFloorRaidEnterBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ServerTimeTicks = GetMultiFloorRaidDateTime(account).Ticks;

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

    [ProtocolHandler(Protocol.MultiFloorRaid_EndBattle)]
    public async Task<MultiFloorRaidEndBattleResponse> EndBattle(
        SchaleDataContext db,
        MultiFloorRaidEndBattleRequest request,
        MultiFloorRaidEndBattleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var multiFloorRaidData = account.ContentInfo.MultiFloorRaidDataInfo;
        var multiFloorRaidDateTime = GetMultiFloorRaidDateTime(account);

        var existingRaid = await db.GetAccountMultiFloorRaids(account.ServerId)
            .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId);

        if (existingRaid == null)
        {
            existingRaid = new MultiFloorRaidDBServer
            {
                AccountServerId = account.ServerId,
                SeasonId = multiFloorRaidData.SeasonId
            };
            db.MultiFloorRaids.Add(existingRaid);
        }

        if (!request.Summary.IsAbort && request.Summary.EndType == BattleEndType.Clear)
        {
            existingRaid.SeasonId = existingRaid.SeasonId != multiFloorRaidData.SeasonId 
                ? multiFloorRaidData.SeasonId 
                : existingRaid.SeasonId;
            existingRaid.ClearedDifficulty = 124;
            existingRaid.LastClearDate = account.GameSettings.ServerDateTime();
            existingRaid.RewardDifficulty = 124;
            existingRaid.LastRewardDate = account.GameSettings.ServerDateTime();
            existingRaid.ClearBattleFrame = request.Summary.EndFrame;
            existingRaid.AllCleared = false;
            existingRaid.HasReceivableRewards = false;
            existingRaid.TotalReceivedRewards = new List<ParcelInfo>();
            existingRaid.TotalReceivableRewards = new List<ParcelInfo>();

            await db.SaveChangesAsync();
        }

        response.MultiFloorRaidDB = existingRaid.ToMap(_mapper);
        response.ParcelResultDB = new();
        response.ServerTimeTicks = multiFloorRaidDateTime.Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.MultiFloorRaid_ReceiveReward)]
    public async Task<MultiFloorRaidReceiveRewardResponse> ReceiveReward(
        SchaleDataContext db,
        MultiFloorRaidReceiveRewardRequest request,
        MultiFloorRaidReceiveRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var multiFloorRaidDateTime = GetMultiFloorRaidDateTime(account);

        var existingRaid = await db.GetAccountMultiFloorRaids(account.ServerId).FirstOrDefaultAsync();

        if (existingRaid != null)
        {
            response.MultiFloorRaidDB = existingRaid.ToMap(_mapper);
        }
        else
        {
            response.MultiFloorRaidDB = new MultiFloorRaidDB
            {
                SeasonId = account.ContentInfo.MultiFloorRaidDataInfo.SeasonId
            };
        }

        response.ParcelResultDB = new();
        response.ServerTimeTicks = multiFloorRaidDateTime.Ticks;

        return response;
    }
}
