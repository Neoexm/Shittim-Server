using Microsoft.EntityFrameworkCore;
using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Core.Math;
using Schale.FlatData;
using Schale.Excel;
using Shittim_Server.Core;
using Shittim_Server.Services;
using Shittim_Server.Managers;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ContentSweepHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;
    private readonly CampaignManager _campaignManager;
    private readonly IMapper _mapper;

    public ContentSweepHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        ParcelHandler parcelHandler,
        CampaignManager campaignManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
        _campaignManager = campaignManager;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.ContentSweep_Request)]
    public async Task<ContentSweepResponse> Request(
        SchaleDataContext db,
        ContentSweepRequest request,
        ContentSweepResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var campaignStageExcels = _excelService.GetTable<CampaignStageExcelT>();
        var campaignStageRewardExcels = _excelService.GetTable<CampaignStageRewardExcelT>();
        var campaignChapterExcels = _excelService.GetTable<CampaignChapterExcelT>();

        var stageExcel = campaignStageExcels.GetCampaignStageId(request.StageId);
        
        var apCost = stageExcel.StageEnterCostAmount * request.Count;
        var apCostParcel = new ParcelResult(stageExcel.StageEnterCostType, stageExcel.StageEnterCostId, apCost);
        await _parcelHandler.BuildParcel(db, account, apCostParcel, isConsume: true);

        var clearParcels = new List<List<ParcelInfo>>();
        var allRewardParcels = new List<ParcelResult>();
        
        for (int i = 0; i < request.Count; i++)
        {
            var rewardDatas = campaignStageRewardExcels.GetAllRewardsByGroupId(stageExcel.CampaignStageRewardId);
            var sweepRewards = GetCalcProbability(rewardDatas);
            
            var parcelInfoList = sweepRewards.Select(r => new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = r.Type, Id = r.Id },
                Amount = r.Amount,
                Multiplier = BasisPoint.One,
                Probability = BasisPoint.One
            }).ToList();
            
            clearParcels.Add(parcelInfoList);
            allRewardParcels.AddRange(sweepRewards);
        }

        var bonusParcels = new List<ParcelInfo>();
        
        var expAmount = stageExcel.StageEnterCostAmount * request.Count;
        allRewardParcels.Add(new ParcelResult(ParcelType.AccountExp, 0, expAmount));

        var parcelResult = await _parcelHandler.BuildParcel(db, account, allRewardParcels);

        var dateTime = account.GameSettings.ServerDateTime();
        var chapterId = campaignChapterExcels.GetChapterIdFromStageId(request.StageId);
        var historyDb = db.CampaignStageHistories.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.StageUniqueId == request.StageId);

        if (historyDb == null)
        {
            historyDb = new CampaignStageHistoryDBServer(account.ServerId, request.StageId, chapterId, dateTime)
            {
                TodayPlayCount = (int)request.Count,
                LastPlay = dateTime
            };
            db.CampaignStageHistories.Add(historyDb);
        }
        else
        {
            historyDb.TodayPlayCount += (int)request.Count;
            historyDb.LastPlay = dateTime;
            db.CampaignStageHistories.Update(historyDb);
        }

        await db.SaveChangesAsync();

        response.ClearParcels = clearParcels;
        response.BonusParcels = bonusParcels;
        response.ParcelResult = parcelResult.ParcelResult;
        response.CampaignStageHistoryDB = historyDb.ToMap(_mapper);

        return response;
    }

    private static List<ParcelResult> GetCalcProbability(IEnumerable<CampaignStageRewardExcelT> rewardExcels)
    {
        var result = new List<ParcelResult>();
        foreach (var rewardExcel in rewardExcels)
        {
            if (!GenerateProbability(rewardExcel.StageRewardProb)) continue;
            var parcelInfos = new ParcelResult(
                rewardExcel.StageRewardParcelType,
                rewardExcel.StageRewardId,
                rewardExcel.StageRewardAmount);
            result.Add(parcelInfos);
        }
        return result;
    }

    private static bool GenerateProbability(long probability)
    {
        if (probability == 0) return true;
        return Random.Shared.Next(10000) < probability;
    }
}
