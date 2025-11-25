using Shittim.Services.Client;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.Excel;

namespace Shittim.Commands
{
    [CommandHandler("giveall", "Unlocks all playable characters", "/giveall")]
    internal class GiveAllCommand : Command
    {
        public GiveAllCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        public override async Task Execute()
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var characterExcel = connection.ExcelTableService.GetTable<CharacterExcelT>();

            var playableCharacters = characterExcel.Where(x => 
                x.IsPlayableCharacter && 
                !x.IsNPC && 
                x.ProductionStep == ProductionStep.Release).ToList();

            int count = 0;
            foreach (var character in playableCharacters)
            {
                var existing = context.Characters.FirstOrDefault(x => 
                    x.AccountServerId == connection.AccountServerId && 
                    x.UniqueId == character.Id);

                if (existing == null)
                {
                    context.Characters.Add(new CharacterDBServer()
                    {
                        AccountServerId = connection.AccountServerId,
                        UniqueId = character.Id,
                        StarGrade = character.DefaultStarGrade,
                        Level = 1,
                        Exp = 0,
                        FavorRank = 1,
                        FavorExp = 0,
                        PublicSkillLevel = 1,
                        ExSkillLevel = 1,
                        PassiveSkillLevel = 1,
                        ExtraPassiveSkillLevel = 1,
                        LeaderSkillLevel = 1
                    });
                    count++;
                }
            }

            await context.SaveChangesAsync();
            await connection.SendChatMessage($"Unlocked {count} characters! Total: {playableCharacters.Count}");
        }
    }
}
