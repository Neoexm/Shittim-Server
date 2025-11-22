using Schale.FlatData;
using Shittim.Services.Client;
using Shittim.GameMasters;
using Schale.Data.GameModel;

namespace Shittim.Commands
{
    [CommandHandler("setseason", "Set season for raids and challenges", "/setseason [type] [seasonId]")]
    internal class SetSeason : Command
    {
        public SetSeason(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^total$|^grand$|^drill$|^final$|^help$|^show$", "Type of content (total, grand, drill, final, help, show)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Type { get; set; } = string.Empty;

        [Argument(1, @"^(?:[0-9])+$", "Season ID (1, 2, 3...)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string SeasonId { get; set; } = string.Empty;

        public override async Task Execute()
        {
            if (string.IsNullOrEmpty(Type) || Type.ToLower() == "help")
            {
                await ShowHelp();
                return;
            }

            if (Type.ToLower() == "show")
            {
                await ShowSeasonList();
                return;
            }

            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);
            long seasonId = 0;

            if (!long.TryParse(SeasonId, out seasonId))
            {
                await connection.SendChatMessage("Invalid Season ID!");
                await ShowHelp();
                return;
            }

            await ContentGM.SetContentSeason(connection, account, context, Type.ToLower(), seasonId);
            await context.SaveChangesAsync();
        }

        private async Task ShowSeasonList()
        {
            var totalSeasons = connection.ExcelTableService.GetTable<RaidSeasonManageExcelT>()
                .OrderBy(x => x.SeasonId);
            await connection.SendChatMessage("Available Total Assault Seasons:");
            foreach (var season in totalSeasons)
            {
                await connection.SendChatMessage($"Season {season.SeasonId} - Boss: {string.Join(", ", season.OpenRaidBossGroup)}");
            }

            var grandSeasons = connection.ExcelTableService.GetTable<EliminateRaidSeasonManageExcelT>()
                .OrderBy(x => x.SeasonId);
            await connection.SendChatMessage("Available Grand Assault Seasons:");
            foreach (var season in grandSeasons)
            {
                List<string> raidBoss = [
                    season.OpenRaidBossGroup01,
                    season.OpenRaidBossGroup02,
                    season.OpenRaidBossGroup03
                ];
                await connection.SendChatMessage($"Season {season.SeasonId} - Boss: {string.Join(", ", raidBoss)}");
            }

            var drillSeasons = connection.ExcelTableService.GetTable<TimeAttackDungeonSeasonManageExcelT>()
                .OrderBy(x => x.Id);
            await connection.SendChatMessage("Available Joint Firing Drill Seasons:");
            foreach (var season in drillSeasons)
            {
                var dungeonInfo = connection.ExcelTableService.GetTable<TimeAttackDungeonExcelT>()
                    .FirstOrDefault(x => x.Id == season.DungeonId);
                string dungeonType = dungeonInfo != null ? string.Join(", ", dungeonInfo.TimeAttackDungeonType) : "Unknown";
                await connection.SendChatMessage($"Season {season.Id} - Type: {dungeonType}");
            }

            var finalSeasons = connection.ExcelTableService.GetTable<MultiFloorRaidSeasonManageExcelT>()
                .OrderBy(x => x.SeasonId);
            await connection.SendChatMessage("Available Final Restriction Seasons:");
            foreach (var season in finalSeasons)
            {
                await connection.SendChatMessage($"Season {season.SeasonId} - Boss: {string.Join(", ", season.OpenRaidBossGroupId)}");
            }
        }

        private async Task ShowHelp() => await ShowHelp(connection);

        public static async Task ShowHelp(IClientConnection connection)
        {
            await connection.SendChatMessage("/setseason - Command to set season for raids and challenges");
            await connection.SendChatMessage("Usage: /setseason [type] [seasonId]");
            await connection.SendChatMessage("Type: total | grand | drill | final");
            await connection.SendChatMessage("SeasonID: 1, 2, 3, ...");
            await connection.SendChatMessage("/setseason show - List all available season id");
        }
    }
}
