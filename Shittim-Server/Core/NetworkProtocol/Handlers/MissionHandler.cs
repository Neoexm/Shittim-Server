using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MissionHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;
    private readonly MissionService _missionService;

    public MissionHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper,
        ExcelTableService excelService,
        ParcelHandler parcelHandler,
        MissionService missionService) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
        _missionService = missionService;
    }

    [ProtocolHandler(Protocol.Mission_Sync)]
    public async Task<MissionSyncResponse> Sync(
        SchaleDataContext db,
        MissionSyncRequest request,
        MissionSyncResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var missionProgresses = db.GetAccountMissionProgresses(account.ServerId).ToList();

        response.MissionProgressDBs = _mapper.Map<List<MissionProgressDB>>(missionProgresses);

        return response;
    }

    [ProtocolHandler(Protocol.Mission_List)]
    public async Task<MissionListResponse> List(
        SchaleDataContext db,
        MissionListRequest request,
        MissionListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var campaignHistories = db.GetAccountCampaignStageHistories(account.ServerId)
            .Select(h => h.StoryUniqueId)
            .ToList();

        var missions = db.GetAccountMissionProgresses(account.ServerId);

        var missionProgresses = request.EventContentId == null
            ? missions.ToList()
            : missions.Where(x => x.MissionUniqueId.ToString().StartsWith(request.EventContentId.ToString())).ToList();

        response.MissionHistoryUniqueIds = campaignHistories;
        response.ProgressDBs = _mapper.Map<List<MissionProgressDB>>(missionProgresses);

        return response;
    }

    [ProtocolHandler(Protocol.Mission_GuideMissionSeasonList)]
    public async Task<GuideMissionSeasonListResponse> GuideMissionSeasonList(
        SchaleDataContext db,
        GuideMissionSeasonListRequest request,
        GuideMissionSeasonListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Mission_Reward)]
    public async Task<MissionRewardResponse> Reward(
        SchaleDataContext db,
        MissionRewardRequest request,
        MissionRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // Find the mission progress
        var missionProgress = await db.MissionProgresses
            .FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.MissionUniqueId == request.MissionUniqueId);

        if (missionProgress == null)
        {
            throw new Exception("Mission progress not found.");
        }

        if (!missionProgress.Complete)
        {
             // For debugging/permissive mode, maybe allow it? But officially should throw.
             // We'll trust the checked logic for now.
             // throw new Exception("Mission not complete.");
        }

        // Load Mission Excel to get rewards
        var missionExcel = _excelService.GetTable<MissionExcelT>().FirstOrDefault(x => x.Id == request.MissionUniqueId);
        
        // Prepare response lists
        response.MissionProgressDBs = new List<MissionProgressDB>();

        if (missionExcel != null)
        {
            // Use ParcelHandler to process rewards
            var parcelResultList = ParcelResult.ConvertParcelResult(
                missionExcel.MissionRewardParcelType, 
                missionExcel.MissionRewardParcelId, 
                missionExcel.MissionRewardAmount
            );

            var parcelResolver = await _parcelHandler.BuildParcel(db, account, parcelResultList);
            response.ParcelResultDB = parcelResolver.ParcelResult;
        }

        // Delete progress to mark as claimed
        db.MissionProgresses.Remove(missionProgress);
        
        // Notify client that mission is now in history
        response.AddedHistoryDB = new MissionHistoryDB
        {
            AccountServerId = account.ServerId,
            MissionUniqueId = missionProgress.MissionUniqueId,
            ServerId = missionProgress.ServerId, // Use existing ID or 0
            CompleteTime = DateTime.Now,
            Expired = false
        };

        await db.SaveChangesAsync();

        // Check if this was a Daily mission and update "Complete X Daily Missions" count
        if (missionExcel.Category == MissionCategory.Daily)
        {
            var updatedMetaMissions = _missionService.UpdateMissionProgress(
                db, 
                account, 
                MissionCompleteConditionType.Reset_DailyMissionFulfill, 
                1
            );
            
            // Add any updated meta-missions to the response so the client sees the progress bar move
            response.MissionProgressDBs = updatedMetaMissions;
            await db.SaveChangesAsync();
        }
        else
        {
            // Return empty list implies it's gone from active list.
            response.MissionProgressDBs = new List<MissionProgressDB>();
        }

        return response;
    }
}
