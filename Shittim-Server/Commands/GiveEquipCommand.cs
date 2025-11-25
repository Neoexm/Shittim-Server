using Shittim.Services.Client;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.Excel;

namespace Shittim.Commands
{
    [CommandHandler("giveequip", "Give yourself equipment by ID or name", "/giveequip <equipmentname> <tierstr> <amountstr>")]
    internal class GiveEquipCommand : Command
    {
        public GiveEquipCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^.+$", "Equipment ID or name")]
        public string EquipmentName { get; set; } = "";

        [Argument(1, @"^\d+$", "Tier level (1-10)")]
        public string TierStr { get; set; } = "1";

        [Argument(2, @"^\d+$", "Amount to give")]
        public string AmountStr { get; set; } = "1";

        public override async Task Execute()
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var equipmentExcel = connection.ExcelTableService.GetTable<EquipmentExcelT>();

            int tier = int.Parse(TierStr);
            int amount = int.Parse(AmountStr);

            if (tier < 1 || tier > 10)
            {
                await connection.SendChatMessage("Tier must be between 1 and 10!");
                return;
            }

            if (long.TryParse(EquipmentName, out long equipId))
            {
                var equipById = equipmentExcel.FirstOrDefault(x => x.Id == equipId);
                if (equipById != null)
                {
                    await GiveEquipment(context, equipById, tier, amount);
                    return;
                }
            }

            var exactMatch = equipmentExcel.FirstOrDefault(x => 
                x.Icon != null && 
                x.Icon.Equals(EquipmentName, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                await GiveEquipment(context, exactMatch, tier, amount);
                return;
            }

            var partialMatches = equipmentExcel.Where(x => 
                x.Icon != null && 
                (x.Icon.Contains(EquipmentName, StringComparison.OrdinalIgnoreCase) ||
                 GetIconBaseName(x.Icon).Contains(EquipmentName, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(x => GetIconBaseName(x.Icon).Length)
                .Take(10)
                .ToList();

            if (partialMatches.Count == 1)
            {
                await GiveEquipment(context, partialMatches[0], tier, amount);
                return;
            }

            if (partialMatches.Count > 1)
            {
                await connection.SendChatMessage($"Multiple equipment found matching '{EquipmentName}':");
                foreach (var equip in partialMatches)
                {
                    var baseName = GetIconBaseName(equip.Icon);
                    await connection.SendChatMessage($"  - {baseName} (ID: {equip.Id}, Category: {equip.EquipmentCategory})");
                }
                await connection.SendChatMessage($"Use equipment ID or be more specific.");
                return;
            }

            await connection.SendChatMessage($"Equipment '{EquipmentName}' not found!");
        }

        private async Task GiveEquipment(SchaleDataContext context, EquipmentExcelT equipData, int tier, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                var equipment = new EquipmentDBServer()
                {
                    AccountServerId = connection.AccountServerId,
                    UniqueId = equipData.Id,
                    Level = 1,
                    Exp = 0,
                    Tier = tier,
                    StackCount = 1,
                    BoundCharacterServerId = 0
                };
                
                context.Equipments.Add(equipment);
                await context.SaveChangesAsync();
            }

            var baseName = GetIconBaseName(equipData.Icon);
            await connection.SendChatMessage($"Added {amount}x {baseName} T{tier} (ID: {equipData.Id})");
        }

        private static string GetIconBaseName(string icon)
        {
            if (string.IsNullOrEmpty(icon)) return "";
            var parts = icon.Split('_');
            return parts.Length > 0 ? parts[^1] : icon;
        }
    }
}
