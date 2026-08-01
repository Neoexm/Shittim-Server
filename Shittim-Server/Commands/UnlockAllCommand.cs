using Schale.FlatData;
using Shittim.Services.Client;
using Schale.Data.GameModel;
using Microsoft.EntityFrameworkCore;

namespace Shittim.Commands
{
    [CommandHandler("unlockall", "Command to unlock all of its contents", "!unlockall [target content]")]
    internal class UnlockAllCommand : Command
    {
        public UnlockAllCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^campaign$|^story$|^weekdungeon$|^schooldungeon$|^battlepass$|^mission$|^help$", "Target content name (campaign, story, weekdungeon, schooldungeon, battlepass, mission)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string target { get; set; } = string.Empty;

        public override async Task Execute()
        {
            if (string.IsNullOrEmpty(target) || target.ToLower() == "help")
            {
                await ShowHelp();
                return;
            }

            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            switch (target.ToLower())
            {
                case "campaign":
                    var campaignChapterExcel = connection.ExcelTableService.GetTable<CampaignChapterExcelT>();
                    var existingStages = context.CampaignStageHistories
                        .Where(x => x.AccountServerId == account.ServerId)
                        .ToDictionary(x => x.StageUniqueId);
                    var newStageCount = 0;

                    foreach (var excel in campaignChapterExcel)
                    {
                        foreach (var stageId in excel.NormalCampaignStageId.Concat(excel.HardCampaignStageId).Concat(excel.NormalExtraStageId).Concat(excel.VeryHardCampaignStageId))
                        {
                            if (existingStages.TryGetValue(stageId, out var existing))
                            {
                                existing.Star1Flag = true;
                                existing.Star2Flag = true;
                                existing.Star3Flag = true;
                                existing.BestStarRecord = 3;
                                existing.IsClearedEver = true;
                                if (existing.ClearTurnRecord == 0)
                                    existing.ClearTurnRecord = 1;
                                existing.FirstClearRewardReceive ??= account.GameSettings.ServerDateTime();
                                existing.StarRewardReceive ??= account.GameSettings.ServerDateTime();
                                continue;
                            }

                            var row = new CampaignStageHistoryDBServer
                            {
                                AccountServerId = account.ServerId,
                                StageUniqueId = stageId,
                                ChapterUniqueId = excel.Id,
                                ClearTurnRecord = 1,
                                TacticClearCountWithRankSRecord = 1,
                                BestStarRecord = 3,
                                Star1Flag = true,
                                Star2Flag = true,
                                Star3Flag = true,
                                IsClearedEver = true,
                                LastPlay = account.GameSettings.ServerDateTime(),
                                TodayPlayCount = 1,
                                FirstClearRewardReceive = account.GameSettings.ServerDateTime(),
                                StarRewardReceive = account.GameSettings.ServerDateTime(),
                            };
                            context.CampaignStageHistories.Add(row);
                            existingStages[stageId] = row;
                            newStageCount++;
                        }
                    }

                    await context.SaveChangesAsync();
                    await connection.SendChatMessage($"Unlocked all campaign stages ({newStageCount} new, rest updated in place)!");
                    break;

                case "story":
                    // Story (scenario) progression is what the client keys campaign/story unlocks on: an episode is cleared iff its ModeId is in ScenarioHistoryDBs (Scenario_List / login sync), and ScenarioModeExcel.ClearedModeId chains episodes together.
                    // Stage histories alone leave everything visually locked.
                    var scenarioModeExcel = connection.ExcelTableService.GetTable<ScenarioModeExcelT>();
                    var existingScenarioIds = context.ScenarioHistories
                        .Where(x => x.AccountServerId == account.ServerId)
                        .Select(x => x.ScenarioUniqueId)
                        .ToHashSet();
                    var newScenarioCount = 0;

                    foreach (var mode in scenarioModeExcel)
                    {
                        // Event stories are granted through the event-content protocols; forcing them here would put ids in the account the client can only resolve mid-event.
                        if (mode.EventContentId != 0)
                            continue;

                        // SpecialOperation is the "Ex." tab (Decagrammaton etc.), Mini the short stories - both permanent content, same clear mechanism.
                        if (mode.ModeType != ScenarioModeTypes.Main &&
                            mode.ModeType != ScenarioModeTypes.Sub &&
                            mode.ModeType != ScenarioModeTypes.Prologue &&
                            mode.ModeType != ScenarioModeTypes.SpecialOperation &&
                            mode.ModeType != ScenarioModeTypes.Mini)
                            continue;

                        if (!existingScenarioIds.Add(mode.ModeId))
                            continue;

                        context.ScenarioHistories.Add(new()
                        {
                            AccountServerId = account.ServerId,
                            ScenarioUniqueId = mode.ModeId,
                            ClearDateTime = account.GameSettings.ServerDateTime(),
                        });
                        newScenarioCount++;
                    }

                    await context.SaveChangesAsync();
                    await connection.SendChatMessage($"Cleared {newScenarioCount} story episodes (main/sub/prologue/ex/mini)!");
                    break;

                case "weekdungeon":
                    var weekdungeonExcel = connection.ExcelTableService.GetTable<WeekDungeonExcelT>();

                    foreach (var excel in weekdungeonExcel)
                    {
                        var starGoalRecord = new Dictionary<StarGoalType, long>();
                        
                        if(excel.StarGoal[0] == StarGoalType.GetBoxes)
                        {
                            starGoalRecord.Add(StarGoalType.GetBoxes, excel.StarGoalAmount.Last());
                        } else {
                            foreach(var goalType in excel.StarGoal)
                            {
                                starGoalRecord.Add(goalType, 1);
                            }
                        }

                        context.WeekDungeonStageHistories.Add(new() {
                            AccountServerId = account.ServerId,
                            StageUniqueId = excel.StageId,
                            StarGoalRecord = starGoalRecord
                        });
                    }

                    await context.SaveChangesAsync();
                    await connection.SendChatMessage("Unlocked all of stages of week dungeon!");
                    break;

                case "schooldungeon":
                    var schooldungeonExcel = connection.ExcelTableService.GetTable<SchoolDungeonStageExcelT>();

                    foreach (var excel in schooldungeonExcel)
                    {
                        var starFlags = new bool[excel.StarGoal.Count];
                        for(int i = 0; i < excel.StarGoal.Count; i++)
                        {
                            starFlags[i] = true;
                        }

                        context.SchoolDungeonStageHistories.Add(new() {
                            AccountServerId = account.ServerId,
                            StageUniqueId = excel.StageId,
                            StarFlags = starFlags
                        });
                    }

                    await context.SaveChangesAsync();
                    await connection.SendChatMessage("Unlocked all of stages of school dungeon!");
                    break;

                case "battlepass":
                    var battlePassExcel = connection.ExcelTableService.GetTable<BattlePassInfoExcelT>();
                    // Every season in the excel, not just the active one.
                    foreach (var season in battlePassExcel)
                    {
                        var bp = await context.BattlePasses.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.BattlePassId == season.Id);
                        if (bp == null)
                        {
                            bp = new BattlePassDBServer
                            {
                                AccountServerId = account.ServerId,
                                BattlePassId = season.Id,
                                LastWeeklyPassExpLimitRefreshDate = DateTime.Now
                            };
                            context.BattlePasses.Add(bp);
                        }

                        bp.PassLevel = 50;
                        bp.PassExp = 0;
                        bp.PurchaseGroupId = 1;
                        bp.ReceiveRewardLevel = 50;
                        bp.ReceivePurchaseRewardLevel = 50;
                    }
                    
                    await context.SaveChangesAsync();
                    await connection.SendChatMessage("Unlocked Battle Pass (Level 50 + Paid)!");
                    break;

                case "mission":
                    var missionExcel = connection.ExcelTableService.GetTable<MissionExcelT>();
                    var newMissionsCount = 0;

                    foreach(var mission in missionExcel)
                    {
                        var progress = await context.MissionProgresses.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.MissionUniqueId == mission.Id);
                        if(progress == null)
                        {
                            progress = new MissionProgressDBServer
                            {
                                AccountServerId = account.ServerId,
                                MissionUniqueId = mission.Id,
                                Complete = true,
                                StartTime = DateTime.Now,
                                ProgressParameters = new Dictionary<long,long>()
                            };
                            context.MissionProgresses.Add(progress);
                            newMissionsCount++;
                        }
                        else
                        {
                            progress.Complete = true; // Force complete
                        }
                    }

                    await context.SaveChangesAsync();
                    await connection.SendChatMessage($"Unlocked {newMissionsCount} new missions (and set rest to Complete)!");
                    break;

                default:
                    throw new ArgumentException("Invalid target!");
            }
        }

        private async Task ShowHelp()
        {
            await connection.SendChatMessage("!unlockall - Command to unlock all of its contents");
            await connection.SendChatMessage("Usage: !unlockall [content]");
            await connection.SendChatMessage("Content: campaign | story | weekdungeon | schooldungeon | battlepass | mission");
        }
    }
}
