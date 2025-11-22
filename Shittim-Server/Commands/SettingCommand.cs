using Shittim.Services.Client;
using Schale.Data.GameModel;

namespace Shittim.Commands
{
    [CommandHandler("setting", "Command to change player's account settings", "/setting [type] [value]")]
    internal class SettingCommand : Command
    {
        public SettingCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^(trackpvp|usefinal|bypassteam|bypasssummon|changetime)$", "Type of settings", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Type { get; set; } = string.Empty;

        [Argument(1, @"^(?:disable|enable|-?\d+)$", "Value for the setting (enable/disable or numeric offset for changetime)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Value { get; set; } = string.Empty;

        public override async Task Execute()
        {
            if (string.IsNullOrEmpty(Type) || Type.ToLower() == "help")
            {
                await ShowHelp();
                return;
            }

            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);
            bool targetValue;

            switch (Type.ToLower())
            {
                case "trackpvp":
                    if (string.IsNullOrEmpty(Value))
                    {
                        targetValue = !account.GameSettings.EnableArenaTracker;
                        account.GameSettings.EnableArenaTracker = targetValue;
                        await connection.SendChatMessage($"Arena Battles export toggled to: {(targetValue ? "Enabled" : "Disabled")}.");
                    }
                    else
                    {
                        targetValue = Value.ToLower() == "enable";
                        account.GameSettings.EnableArenaTracker = targetValue;
                        await connection.SendChatMessage($"Arena Battles export set to: {(targetValue ? "Enabled" : "Disabled")}.");
                    }
                    break;
                case "usefinal":
                    if (string.IsNullOrEmpty(Value))
                    {
                        targetValue = !account.GameSettings.EnableMultiFloorRaid;
                        account.GameSettings.EnableMultiFloorRaid = targetValue;
                        await connection.SendChatMessage($"Final Restriction toggled to: {(targetValue ? "Enabled" : "Disabled")}.");
                    }
                    else
                    {
                        targetValue = Value.ToLower() == "enable";
                        account.GameSettings.EnableMultiFloorRaid = targetValue;
                        await connection.SendChatMessage($"Final Restriction set to: {(targetValue ? "Enabled" : "Disabled")}.");
                    }
                    break;
                case "bypassteam":
                    if (string.IsNullOrEmpty(Value))
                    {
                        targetValue = !account.GameSettings.BypassTeamDeployment;
                        account.GameSettings.BypassTeamDeployment = targetValue;
                        await connection.SendChatMessage($"Team Deployment Bypass toggled to: {(targetValue ? "Enabled" : "Disabled")}.");
                    }
                    else
                    {
                        targetValue = Value.ToLower() == "enable";
                        account.GameSettings.BypassTeamDeployment = targetValue;
                        await connection.SendChatMessage($"Team Deployment Bypass set to: {(targetValue ? "Enabled" : "Disabled")}.");
                    }
                    break;
                case "bypasssummon":
                    if (string.IsNullOrEmpty(Value))
                    {
                        targetValue = !account.GameSettings.BypassCafeSummon;
                        account.GameSettings.BypassCafeSummon = targetValue;
                        await connection.SendChatMessage($"Cafe Character Summon toggled to: {(targetValue ? "Enabled" : "Disabled")}.");
                    }
                    else
                    {
                        targetValue = Value.ToLower() == "enable";
                        account.GameSettings.BypassCafeSummon = targetValue;
                        await connection.SendChatMessage($"Cafe Character Summon set to: {(targetValue ? "Enabled" : "Disabled")}.");
                    }
                    break;
                case "changetime":
                    if (string.IsNullOrEmpty(Value))
                    {
                        account.GameSettings.ForceDateTime = !account.GameSettings.ForceDateTime;
                        if (account.GameSettings.ForceDateTime)
                        {
                            account.GameSettings.CurrentDateTime = DateTimeOffset.Now;
                            await connection.SendChatMessage($"Time Changer toggled ON. Current forced time is now {account.GameSettings.ForceDateTimeOffset}.");
                        }
                        else
                        {
                            await connection.SendChatMessage($"Time Changer toggled OFF. Using real time.");
                        }
                    }
                    else if (Value.ToLower() == "enable")
                    {
                        account.GameSettings.ForceDateTime = true;
                        account.GameSettings.CurrentDateTime = DateTimeOffset.Now;
                        await connection.SendChatMessage($"Time Changer explicitly enabled. Current forced time is now {account.GameSettings.ForceDateTimeOffset}.");
                    }
                    else if (Value.ToLower() == "disable")
                    {
                        account.GameSettings.ForceDateTime = false;
                        await connection.SendChatMessage("Time Changer explicitly disabled. Using real time.");
                    }
                    else if (int.TryParse(Value, out int daysOffset))
                    {
                        account.GameSettings.ForceDateTime = true;
                        account.GameSettings.ForceDateTimeOffset = DateTimeOffset.Now.AddDays(daysOffset);
                        await connection.SendChatMessage($"Time Changer enabled. Forced time moved {daysOffset} days from now to {account.GameSettings.ForceDateTimeOffset}.");
                    }
                    else
                    {
                        await connection.SendChatMessage("Invalid value for 'changetime'. Please use 'disable', an integer (e.g., '5' or '-12'), or leave empty to toggle.");
                        await ShowHelp();
                    }
                    break;
                default:
                    await connection.SendChatMessage("Invalid Settings!");
                    await ShowHelp();
                    break;
            }


            await context.SaveChangesAsync();
        }

        
        private async Task ShowHelp()
        {
            await connection.SendChatMessage("/setting - Command to change player's account settings");
            await connection.SendChatMessage("Usage: /setting [type] [value]");
            await connection.SendChatMessage("Available Types:");
            await connection.SendChatMessage("  trackpvp: Enable/disable exporting Arena Battles data.");
            await connection.SendChatMessage("  usefinal: Enable/disable Multi-Floor Raid.");
            await connection.SendChatMessage("  bypassteam: Enable/disable Team Deployment Bypass.");
            await connection.SendChatMessage("  bypasssummon: Enable/disable Cafe Character Summon.");
            await connection.SendChatMessage("  changetime: Manipulate game's current time.");
            await connection.SendChatMessage("Value (optional for toggle, required for specific settings):");
            await connection.SendChatMessage("  For trackpvp, usefinal, bypassteam: 'enable' or 'disable'. Omit to toggle.");
            await connection.SendChatMessage("  For changetime: 'enable' to turn on (resets to current real time). 'disable' to turn off. An integer (e.g., '5' or '-12') to move days from current real time. Omit to toggle.");
        }
    }
}
