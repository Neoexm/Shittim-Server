using Schale.FlatData;
using Shittim.Services.Client;
using Schale.Data.GameModel;

namespace Shittim.Commands
{
    [CommandHandler("unlockall", "Command to unlock all of its contents", "/unlockall [target content]")]
    internal class UnlockAllCommand : Command
    {
        public UnlockAllCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^campaign$|^weekdungeon$|^schooldungeon$|^help$", "Target content name (campaign, weekdungeon, schooldungeon)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
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

                    foreach (var excel in campaignChapterExcel)
                    {
                        foreach (var stageId in excel.NormalCampaignStageId.Concat(excel.HardCampaignStageId).Concat(excel.NormalExtraStageId).Concat(excel.VeryHardCampaignStageId))
                        {
                            context.CampaignStageHistories.Add(new()
                            {
                                AccountServerId = account.ServerId,
                                StageUniqueId = stageId,
                                ChapterUniqueId = excel.Id,
                                ClearTurnRecord = 1,
                                Star1Flag = true,
                                Star2Flag = true,
                                Star3Flag = true,
                                LastPlay = account.GameSettings.ServerDateTime(),
                                TodayPlayCount = 1,
                                FirstClearRewardReceive = account.GameSettings.ServerDateTime(),
                                StarRewardReceive = account.GameSettings.ServerDateTime(),
                            });
                        }
                    }

                    await context.SaveChangesAsync();
                    await connection.SendChatMessage("Unlocked all of stages of campaign!");
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

                default:
                    throw new ArgumentException("Invalid target!");
            }
        }

        private async Task ShowHelp()
        {
            await connection.SendChatMessage("/unlockall - Command to unlock all of its contents");
            await connection.SendChatMessage("Usage: /unlockall [content]");
            await connection.SendChatMessage("Content: campaign | weekdungeon | schooldungeon");
        }
    }
}
