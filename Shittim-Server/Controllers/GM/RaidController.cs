using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shittim.GameMasters;
using Shittim.Models.GM;
using Shittim.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using BlueArchiveAPI.Services;

namespace Shittim.Controllers.GM
{
    [ApiController]
    [Route("/dev/api")]
    public class RaidController : ControllerBase
    {
        private readonly IDbContextFactory<SchaleDataContext> contextFactory;
        private readonly ExcelTableService excelTableService;

        public RaidController(IDbContextFactory<SchaleDataContext> _contextFactory, ExcelTableService _excelTableService)
        {
            contextFactory = _contextFactory;
            excelTableService = _excelTableService;
        }

        [HttpGet]
        [Route("get_raid")]
        public IResult GetRaid()
        {
            var raidData = excelTableService.GetTable<RaidSeasonManageExcelT>();
            var timeAttackDungeonData = excelTableService.GetTable<TimeAttackDungeonSeasonManageExcelT>();
            var TADExcel = excelTableService.GetTable<TimeAttackDungeonExcelT>();
            var eliminateRaidData = excelTableService.GetTable<EliminateRaidSeasonManageExcelT>();
            var multiFloorRaidData = excelTableService.GetTable<MultiFloorRaidSeasonManageExcelT>();

            var processedRaids = raidData.Select(raid => new
            {
                raid.SeasonId,
                BossDetail = raid.OpenRaidBossGroup.FirstOrDefault() ?? "N/A",
                Date = raid.SeasonStartData
            });

            var processedTimeAttackDungeons = timeAttackDungeonData.Select(dungeon => new
            {
                dungeon.Id,
                JfdType = TADExcel.FirstOrDefault(x => x.Id == dungeon.DungeonId)?.TimeAttackDungeonType,
                Date = dungeon.StartDate
            });

            var processedEliminateRaids = eliminateRaidData.Select(eliminate =>
            {
                var groupedBosses = new List<string>
                {
            eliminate.OpenRaidBossGroup01,
            eliminate.OpenRaidBossGroup02,
            eliminate.OpenRaidBossGroup03
                }
                .Where(boss => !string.IsNullOrEmpty(boss))
                .Select(boss => boss.Split('_'))
                .GroupBy(parts => parts[0])
                .Select(group => $"{group.Key} {group.Select(parts => parts[1]).First()} ({string.Join(", ", group.Select(parts => parts[2]))})")
                .ToList();

                return new
                {
                    eliminate.SeasonId,
                    BossDetail = string.Join(", ", groupedBosses),
                    Date = eliminate.SeasonStartData
                };
            });

            var processedMultiFloorRaids = multiFloorRaidData.Select(multiFloorRaid => new
            {
                multiFloorRaid.SeasonId,
                BossDetail = multiFloorRaid.OpenRaidBossGroupId ?? "N/A",
                Date = multiFloorRaid.SeasonStartDate
            });

            var responseData = new
            {
                Raids = processedRaids,
                TimeAttackDungeons = processedTimeAttackDungeons,
                EliminateRaids = processedEliminateRaids,
                MultiFloorRaids = processedMultiFloorRaids
            };

            return BaseAPIResponse.Create(ResponseStatus.Success, responseData);
        }

        [HttpGet]
        [Route("get_raidinfo")]
        public IResult GetRaidInfo()
        {
            var totalAssault = ContentGM.GenTotalAssaultData(excelTableService);
            var grandAssault = ContentGM.GenGrandAssaultData(excelTableService);
            var timeAttackDungeon = ContentGM.GenTADData(excelTableService);
            var multiFloor = ContentGM.GenMultiFloorData(excelTableService);

            var payload = new GetRaidResponse
            {
                TotalAssault = totalAssault,
                GrandAssault = grandAssault,
                TimeAttackDungeon = timeAttackDungeon,
                MultiFloor = multiFloor
            };

            return BaseAPIResponse.Create(ResponseStatus.Success, payload);
        }

        [HttpPost]
        [Route("set_raid")]
        public async Task<IResult> SetRaid(SetRaidRequest request)
        {
            var context = contextFactory.CreateDbContext();
            var account = context.GetAccount(request.UserID);

            return await ContentGM.SetContentSeasonWeb(excelTableService, account, context, request.RaidType, request.SeasonId);
        }

        [HttpPost]
        [Route("get_raid_records_by_season")]
        public async Task<IResult> GetTotalAssaultRecords(GetRaidRecordsRequest raidReq)
        {

            var context = contextFactory.CreateDbContext();
            var account = context.GetAccount(raidReq.UserID);
            var battles = account.RaidSummaries
                .Where(rs => rs.SeasonId == raidReq.SeasonId && rs.ContentType == ContentTypeSummary.Raid)
                .ToList();
            var raidStageExcel = excelTableService.GetTable<RaidStageExcelT>();

            var rsp = new RaidRecordsResponse();

            foreach (var raidSummary in battles)
            {
                var summaryIds = raidSummary.BattleSummaryIds;

                var battleSummaries = context.BattleSummaries
                    .Where(bs => summaryIds.Contains(bs.BattleId.ToString()))
                    .ToList();
                long totalEndFrame = battleSummaries.Sum(bs => bs.EndFrame);
                float totalSeconds = totalEndFrame / 30f;
                var targetStage = raidStageExcel.FirstOrDefault(stage =>
                    stage.Id == raidSummary.RaidStageId);

                var timeScore = MathService.CalculateTimeScore(totalSeconds, targetStage.PerSecondMinusScore);
                var hpPercentScorePoint = targetStage.HPPercentScore;
                var defaultClearPoint = targetStage.DefaultClearScore;
                var finalScore = (int)(timeScore + hpPercentScorePoint + defaultClearPoint);

                var teams = new Dictionary<int, List<RaidTeamMember>>();
                for (int i = 0; i < battleSummaries.Count; i++)
                {
                    var summary = battleSummaries[i];
                    var members = summary.Characters.Select(ch => new RaidTeamMember
                    {
                        Id = ch.UniqueId,
                        Level = ch.Level,
                        StarGrade = ch.StarGrade,
                        WeaponStarGrade = ch.WeaponDatas.StarGrade
                    }).ToList();

                    teams[i + 1] = members;
                }

                rsp.records.Add(new TotalAssaultBattleRecord
                {
                    BattleId = raidSummary.ServerId,
                    Score = finalScore,
                    Difficulty = (int)targetStage.Difficulty,
                    Teams = teams,
                });
            }
            return BaseAPIResponse.Create(ResponseStatus.Success, rsp);
        }

        [HttpPost]
        [Route("get_grand_records_by_season")]
        public async Task<IResult> GetGrandAssaultRecords(GetRaidRecordsRequest raidReq)
        {
            var context = contextFactory.CreateDbContext();
            var account = context.GetAccount(raidReq.UserID);

            var battles = account.RaidSummaries
                .Where(rs => rs.SeasonId == raidReq.SeasonId && rs.ContentType == ContentTypeSummary.EliminateRaid)
                .ToList();

            var raidStageExcel = excelTableService.GetTable<EliminateRaidStageExcelT>();

            var rsp = new GrandAssaultRecordsResponse();

            foreach (var battle in battles)
            {
                var targetStage = raidStageExcel.FirstOrDefault(stage => stage.Id == battle.RaidStageId);
                var armorType = targetStage.RaidBossGroup.Split('_').Last();

                var summaryIds = battle.BattleSummaryIds;
                var battleSummaries = context.BattleSummaries
                    .Where(bs => summaryIds.Contains(bs.BattleId.ToString()))
                    .ToList();


                long totalEndFrame = battleSummaries.Sum(bs => bs.EndFrame);
                float totalSeconds = totalEndFrame / 30f;
                var timeScore = MathService.CalculateTimeScore(totalSeconds, targetStage.PerSecondMinusScore);
                var hpPercentScorePoint = targetStage.HPPercentScore;
                var defaultClearPoint = targetStage.DefaultClearScore;
                var finalScore = (int)(timeScore + hpPercentScorePoint + defaultClearPoint);

                var teams = new Dictionary<int, List<RaidTeamMember>>();
                for (int i = 0; i < battleSummaries.Count; i++)
                {
                    var summary = battleSummaries[i];
                    var members = summary.Characters.Select(ch => new RaidTeamMember
                    {
                        Id = ch.UniqueId,
                        Level = ch.Level,
                        StarGrade = ch.StarGrade,
                        WeaponStarGrade = ch.WeaponDatas.StarGrade
                    }).ToList();

                    teams[i + 1] = members;
                }

                rsp.records.Add(new GrandAssaultBattleRecord
                {
                    BattleId = battle.ServerId,
                    Score = finalScore,
                    Difficulty = (int)targetStage.Difficulty,
                    Armor = armorType,
                    Teams = teams
                });
            }

            return BaseAPIResponse.Create(ResponseStatus.Success, rsp);
        }
    }
}
