using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;

namespace Shittim_Server.Services;

// MissionService only ever scans MissionExcel and BattlePassMissionExcel, and its category allowance excludes MiniGameScore/MiniGameEvent, so none of the 429 MiniGameMissionExcel rows can move through it.
public class MiniGameMissionService
{
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;

    public MiniGameMissionService(ExcelTableService excelService, IMapper mapper)
    {
        _excelService = excelService;
        _mapper = mapper;
    }

    public List<MissionProgressDB> UpdateMissionProgress(
        SchaleDataContext context,
        AccountDBServer account,
        long eventContentId,
        MissionCompleteConditionType conditionType,
        long amount = 1,
        long? parameter = null,
        long? stageId = null)
    {
        var updated = new List<MissionProgressDB>();

        var missions = _excelService.GetTable<MiniGameMissionExcelT>()
            .Where(m => m.EventContentId == eventContentId && m.CompleteConditionType == conditionType)
            .ToList();

        var bestOf = IsBestOf(conditionType);
        var justCompleted = new List<long>();

        foreach (var mission in missions)
        {
            // CompleteConditionParameter on this table leads with the event id and then names the subject, so [804] is the whole-event row and [804, 8040102] is the one stage. A row that names a stage must not move for a different stage.
            var declared = mission.CompleteConditionParameter ?? [];

            // the shooting section rows are three long - [event, stage, section] - and every section number appears once for the normal stage and once for the hard one, so the section on its own matches both
            if (declared.Count > 2 && stageId.HasValue && declared[1] != stageId.Value)
                continue;

            long key;
            if (declared.Count > 1 && declared[0] != eventContentId)
            {
                // no leading event id means the list is the subject set itself rather than a scope plus a subject, and the count asks how many of them you hold - so one shared bucket, one tick per member
                if (!parameter.HasValue || !declared.Contains(parameter.Value))
                    continue;
                key = 0L;
            }
            else if (parameter.HasValue && declared.Contains(parameter.Value))
                key = parameter.Value;
            else if (declared.Count <= 1)
                key = declared.Count == 1 ? declared[0] : 0L;
            else
                continue;

            // a handler makes several of these calls before it saves, and the recursion below re-enters, so the row wanted may only exist in the change tracker - a query runs in SQL and would add a second one
            var progress = context.MissionProgresses.Local.FirstOrDefault(m => m.AccountServerId == account.ServerId && m.MissionUniqueId == mission.Id)
                ?? context.MissionProgresses.FirstOrDefault(m => m.AccountServerId == account.ServerId && m.MissionUniqueId == mission.Id);

            if (progress != null && progress.Complete)
                continue;

            if (progress == null)
            {
                progress = new MissionProgressDBServer
                {
                    AccountServerId = account.ServerId,
                    MissionUniqueId = mission.Id,
                    StartTime = account.GameSettings.ServerDateTime(),
                    ProgressParameters = new Dictionary<long, long>(),
                    Complete = false
                };
                context.MissionProgresses.Add(progress);
            }

            progress.ProgressParameters ??= new Dictionary<long, long>();
            var current = progress.ProgressParameters.GetValueOrDefault(key);

            progress.ProgressParameters[key] = bestOf ? Math.Max(current, amount) : current + amount;
            progress.Complete = progress.ProgressParameters[key] >= mission.CompleteConditionCount;

            if (progress.Complete)
                justCompleted.Add(mission.Id);

            updated.Add(progress.ToMap(_mapper));
        }

        foreach (var completedId in justCompleted)
            updated.AddRange(UpdateMissionProgress(context, account, eventContentId, MissionCompleteConditionType.Reset_CompleteMission, 1, completedId));

        return updated;
    }

    // A personal best, not a tally - replaying a worse run must not walk the number backwards, and accumulating would complete a "score 3,000,000 in one run" mission over three bad runs.
    private static bool IsBestOf(MissionCompleteConditionType conditionType)
        => conditionType is MissionCompleteConditionType.Reset_GetBestScoreRhythm
            or MissionCompleteConditionType.Reset_GetSpecificScoreRhythm
            or MissionCompleteConditionType.Reset_GetComboCountRhythm
            or MissionCompleteConditionType.Reset_FieldMasteryLevel
            or MissionCompleteConditionType.Reset_DreamGetSpecificParameter;
}
