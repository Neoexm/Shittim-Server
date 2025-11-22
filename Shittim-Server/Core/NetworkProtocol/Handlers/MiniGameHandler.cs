using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MiniGameHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;

    public MiniGameHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
    }

    [ProtocolHandler(Protocol.MiniGame_StageList)]
    public async Task<MiniGameStageListResponse> StageList(
        SchaleDataContext db,
        MiniGameStageListRequest request,
        MiniGameStageListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var stageExcels = _excelService.GetTable<EventContentStageExcelT>();
        var stageData = stageExcels
            .Where(x => x.EventContentId == request.EventContentId)
            .Select(x => new MiniGameHistoryDB
            {
                EventContentId = x.EventContentId,
                UniqueId = x.Id,
                AccumulatedScore = 0,
                IsFullCombo = false,
                ClearDate = account.GameSettings.ServerDateTime().AddDays(-1),
                HighScore = 0
            }).ToList();

        response.MiniGameHistoryDBs = new List<MiniGameHistoryDB>();

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_MissionList)]
    public async Task<MiniGameMissionListResponse> MissionList(
        SchaleDataContext db,
        MiniGameMissionListRequest request,
        MiniGameMissionListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var missionExcels = _excelService.GetTable<EventContentMissionExcelT>();
        var missionIds = missionExcels
            .Where(x => x.EventContentId == request.EventContentId)
            .Select(x => x.Id)
            .ToList();

        var missionProgress = missionExcels
            .Where(x => x.EventContentId == request.EventContentId)
            .Select(x => new MissionProgressDB
            {
                MissionUniqueId = x.Id,
                StartTime = account.GameSettings.ServerDateTime().AddHours(-1),
                ProgressParameters = new Dictionary<long, long>
                {
                    { request.EventContentId, 1 }
                }
            }).ToList();

        response.MissionHistoryUniqueIds = missionIds;
        response.ProgressDBs = missionProgress;

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_EnterStage)]
    public async Task<MiniGameEnterStageResponse> EnterStage(
        SchaleDataContext db,
        MiniGameEnterStageRequest request,
        MiniGameEnterStageResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var progressDict = new MissionProgressDB
        {
            MissionUniqueId = request.UniqueId,
            StartTime = DateTime.UtcNow,
            ProgressParameters = new Dictionary<long, long> { { request.EventContentId, 1 } }
        };

        var progressDB = new MissionProgressDB
        {
            MissionUniqueId = request.EventContentId,
            StartTime = DateTime.UtcNow,
            ProgressParameters = new Dictionary<long, long> { { 0, Random.Shared.NextInt64() } }
        };

        response.EventMissionProgressDBDict = new Dictionary<long, List<MissionProgressDB>>
        {
            { request.EventContentId, [progressDict] }
        };
        response.MissionProgressDBs = [progressDB];

        return response;
    }

    [ProtocolHandler(Protocol.MiniGame_CCGLobby)]
    public async Task<MiniGameCCGLobbyResponse> CCGLobby(
        SchaleDataContext db,
        MiniGameCCGLobbyRequest request,
        MiniGameCCGLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.Perks = [];
        response.RewardPoint = 0;
        response.CanSweep = false;

        return response;
    }
}
