using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.Data.GameModel;
using Shittim_Server.Core;
using Shittim_Server.Services;
using Schale.FlatData;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class BattlePassHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelTableService;
    private readonly ParcelHandler _parcelHandler;

    public BattlePassHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelTableService,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _excelTableService = excelTableService;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.BattlePass_Check)]
    public async Task<BattlePassCheckResponse> Check(
        SchaleDataContext db,
        BattlePassCheckRequest request,
        BattlePassCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.BattlePass_GetInfo)]
    public async Task<BattlePassGetInfoResponse> GetInfo(
        SchaleDataContext db,
        BattlePassGetInfoRequest request,
        BattlePassGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var battlePass = await db.BattlePasses
            .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.BattlePassId == request.BattlePassId);

        if (battlePass == null)
        {
            battlePass = new BattlePassDBServer
            {
                AccountServerId = account.ServerId,
                BattlePassId = request.BattlePassId,
                PassLevel = 1,
                PassExp = 0,
                PurchaseGroupId = 0,
                ReceiveRewardLevel = 0,
                ReceivePurchaseRewardLevel = 0,
                WeeklyPassExp = 0,
                LastWeeklyPassExpLimitRefreshDate = DateTime.Now
            };
            
            db.BattlePasses.Add(battlePass);
            await db.SaveChangesAsync();
        }

        response.BattlePassInfo = battlePass;

        return response;
    }

    [ProtocolHandler(Protocol.BattlePass_ReceiveReward)]
    public async Task<BattlePassReceiveRewardResponse> ReceiveReward(
        SchaleDataContext db,
        BattlePassReceiveRewardRequest request,
        BattlePassReceiveRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var battlePass = await db.BattlePasses
            .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.BattlePassId == request.BattlePassId);

        if (battlePass == null)
            throw new Exception("Battle Pass not found");

        var battlePassInfoExcel = _excelTableService.GetTable<BattlePassInfoExcelT>()
            .FirstOrDefault(x => x.Id == request.BattlePassId);

        if (battlePassInfoExcel == null) 
            throw new Exception("Battle Pass Info not found");

        var rewardExcels = _excelTableService.GetTable<BattlePassRewardExcelT>();
        var parcels = new List<ParcelInfo>();

        // Collect Normal Rewards
        if (battlePass.ReceiveRewardLevel < battlePass.PassLevel)
        {
            var newRewards = rewardExcels.Where(x => 
                x.RewardGroupId == battlePassInfoExcel.FreeRewardGroupID && 
                x.Level > battlePass.ReceiveRewardLevel && 
                x.Level <= battlePass.PassLevel);

            foreach (var reward in newRewards)
            {
                parcels.Add(new ParcelInfo
                {
                    Key = new ParcelKeyPair { Type = reward.RewardParcelType, Id = reward.RewardParcelUniqueId },
                    Amount = reward.RewardParcelAmount
                });
            }
            battlePass.ReceiveRewardLevel = battlePass.PassLevel;
        }

        // Collect Paid Rewards
        if (battlePass.PurchaseGroupId != 0 && battlePass.ReceivePurchaseRewardLevel < battlePass.PassLevel)
        {
            var newPaidRewards = rewardExcels.Where(x => 
                x.RewardGroupId == battlePassInfoExcel.PurchaseRewardGroupID && 
                x.Level > battlePass.ReceivePurchaseRewardLevel && 
                x.Level <= battlePass.PassLevel);

            foreach (var reward in newPaidRewards)
            {
                parcels.Add(new ParcelInfo
                {
                    Key = new ParcelKeyPair { Type = reward.RewardParcelType, Id = reward.RewardParcelUniqueId },
                    Amount = reward.RewardParcelAmount
                });
            }
            battlePass.ReceivePurchaseRewardLevel = battlePass.PassLevel;
        }

        var parcelResultDB = new ParcelResultDB();
        if (parcels.Any())
        {
            await _parcelHandler.BuildParcel(db, account, parcels, parcelResultDB);
        }

        await db.SaveChangesAsync();

        response.BattlePassInfo = battlePass;
        response.ParcelResult = parcelResultDB;

        return response;
    }

    [ProtocolHandler(Protocol.BattlePass_BuyLevel)]
    public async Task<BattlePassBuyLevelResponse> BuyLevel(
        SchaleDataContext db,
        BattlePassBuyLevelRequest request,
        BattlePassBuyLevelResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var battlePass = await db.BattlePasses
            .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.BattlePassId == request.BattlePassId);

        if (battlePass == null)
            throw new Exception("Battle Pass not found");

        // Logic for buying levels
        // Simplifying: Increment level, assume checking currency is done or simplified (deduct if needed using ParcelHandler)
        // Ignoring cost for now as per "unblock user" priority, or we can look up cost in Excel if critical.
        // Usually PassLvUpGoodsID is the currency (e.g. Pyroxene) and amount is needed per level.
        
        battlePass.PassLevel += request.BattlePassBuyLevelCount;
        // Cap level if needed (check BattlePassLevelExcel max level)
        
        await db.SaveChangesAsync();

        response.BattlePassInfo = battlePass;
        
        // Populate AccountCurrencyDB if changed (it's not changed here, but good practice to return current state)
        var currency = await db.Currencies.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId);
        if (currency != null)
        {
            response.AccountCurrencyDB = new AccountCurrencyDB
            {
                AccountLevel = currency.AccountLevel,
                AcademyLocationRankSum = currency.AcademyLocationRankSum,
                CurrencyDict = currency.CurrencyDict,
                UpdateTimeDict = currency.UpdateTimeDict
            };
        }

        return response;
    }

    [ProtocolHandler(Protocol.BattlePass_MissionList)]
    public async Task<BattlePassMissionListResponse> MissionList(
        SchaleDataContext db,
        BattlePassMissionListRequest request,
        BattlePassMissionListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var missionTable = _excelTableService.GetTable<BattlePassMissionExcelT>();
        var bpMissions = missionTable.Where(x => x.BattlePassId == request.BattlePassId).Select(x => x.Id).ToList();

        var missionProgresses = await db.MissionProgresses
            .Where(x => x.AccountServerId == account.ServerId && bpMissions.Contains(x.MissionUniqueId))
            .ToListAsync();

        response.ProgressDBs = missionProgresses.Select(x => new MissionProgressDB
        {
            MissionUniqueId = x.MissionUniqueId,
            ProgressParameters = x.ProgressParameters,
            StartTime = x.StartTime,
            Complete = x.Complete,
            ServerId = x.ServerId,
            AccountServerId = x.AccountServerId
        }).ToList();

        response.MissionHistoryUniqueIds = missionProgresses
            .Where(x => x.Complete)
            .Select(x => x.MissionUniqueId)
            .ToList();

        return response;
    }
}
