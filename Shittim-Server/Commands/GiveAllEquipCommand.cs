using Shittim.Services.Client;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.Excel;

namespace Shittim.Commands
{
    [CommandHandler("giveallequip", "Give yourself all equipment at all tiers", "/giveallequip")]
    internal class GiveAllEquipCommand : Command
    {
        public GiveAllEquipCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        public override async Task Execute()
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var equipmentExcel = connection.ExcelTableService.GetTable<EquipmentExcelT>();

            int totalAdded = 0;

            foreach (var equipData in equipmentExcel)
            {
                for (int tier = 1; tier <= 10; tier++)
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
                    totalAdded++;
                }
            }

            await context.SaveChangesAsync();
            await connection.SendChatMessage($"Added {totalAdded} equipment pieces ({equipmentExcel.Count} types x 10 tiers)");
        }
    }
}
