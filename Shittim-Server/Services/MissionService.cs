using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;

namespace Shittim_Server.Services;

public class MissionService
{
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;

    public MissionService(ExcelTableService excelService, IMapper mapper)
    {
        _excelService = excelService;
        _mapper = mapper;
    }

    public List<MissionProgressDB> UpdateMissionProgress(
        SchaleDataContext context,
        AccountDBServer account,
        MissionCompleteConditionType conditionType,
        long amount = 1,
        long? parameter = null)
    {
        var missionExcels = _excelService.GetTable<MissionExcelT>();
        var battlePassMissionExcels = _excelService.GetTable<BattlePassMissionExcelT>();
        var updatedMissions = new List<MissionProgressDB>();

        var relevantMissions = missionExcels
            .Where(m => m.CompleteConditionType == conditionType)
            .Where(m => m.Category == MissionCategory.Daily || 
                       m.Category == MissionCategory.Weekly || 
                       m.Category == MissionCategory.Achievement)
            .Select(m => new { m.Id, m.CompleteConditionParameter, m.CompleteConditionCount })
            .ToList();

        // Add BattlePass missions to relevant list
        var relevantBpMissions = battlePassMissionExcels
            .Where(m => m.CompleteConditionType == conditionType)
            .Select(m => new { m.Id, m.CompleteConditionParameter, m.CompleteConditionCount })
            .ToList();
            
        relevantMissions.AddRange(relevantBpMissions);

        foreach (var mission in relevantMissions)
        {
            if (parameter.HasValue && mission.CompleteConditionParameter != null && 
                mission.CompleteConditionParameter.Count > 0)
            {
                if (!mission.CompleteConditionParameter.Contains(parameter.Value))
                    continue;
            }

            var existingMission = context.MissionProgresses
                .FirstOrDefault(m => m.AccountServerId == account.ServerId && 
                                   m.MissionUniqueId == mission.Id);

            if (existingMission != null && existingMission.Complete)
                continue;

            if (existingMission == null)
            {
                existingMission = new MissionProgressDBServer
                {
                    AccountServerId = account.ServerId,
                    MissionUniqueId = mission.Id,
                    StartTime = account.GameSettings.ServerDateTime(),
                    ProgressParameters = new Dictionary<long, long> { { 0, amount } },
                    Complete = false
                };
                context.MissionProgresses.Add(existingMission);
            }
            else
            {
                if (existingMission.ProgressParameters == null)
                    existingMission.ProgressParameters = new Dictionary<long, long>();

                if (!existingMission.ProgressParameters.ContainsKey(0))
                    existingMission.ProgressParameters[0] = 0;

                existingMission.ProgressParameters[0] += amount;
            }

            if (existingMission.ProgressParameters[0] >= mission.CompleteConditionCount)
            {
                existingMission.Complete = true;
            }

            updatedMissions.Add(existingMission.ToMap(_mapper));
        }

        return updatedMissions;
    }
}
