using Shittim.Services.Client;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.Excel;

namespace Shittim.Commands
{
    [CommandHandler("max", "Max out character stats", "/max <all|charactername>")]
    internal class MaxCommand : Command
    {
        public MaxCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^.+$", "Character name or 'all'")]
        public string CharacterName { get; set; } = "all";

        public override async Task Execute()
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var characterExcel = connection.ExcelTableService.GetTable<CharacterExcelT>();

            if (CharacterName.ToLower() == "all")
            {
                await MaxAllCharacters(context, characterExcel);
            }
            else
            {
                await MaxSingleCharacter(context, characterExcel, CharacterName);
            }
        }

        private async Task MaxAllCharacters(SchaleDataContext context, List<CharacterExcelT> characterExcel)
        {
            var ownedCharacters = context.Characters
                .Where(x => x.AccountServerId == connection.AccountServerId)
                .ToList();

            if (ownedCharacters.Count == 0)
            {
                await connection.SendChatMessage("You don't own any characters!");
                return;
            }

            int count = 0;
            foreach (var character in ownedCharacters)
            {
                var characterData = characterExcel.FirstOrDefault(x => x.Id == character.UniqueId);
                if (characterData != null)
                {
                    character.Level = 90;
                    character.Exp = 0;
                    character.StarGrade = characterData.MaxStarGrade;
                    character.FavorRank = 50;
                    character.FavorExp = 0;
                    character.PublicSkillLevel = 10;
                    character.ExSkillLevel = 5;
                    character.PassiveSkillLevel = 10;
                    character.ExtraPassiveSkillLevel = 10;
                    
                    context.Characters.Update(character);
                    count++;
                }
            }

            await context.SaveChangesAsync();
            await connection.SendChatMessage($"Maxed out {count} characters!");
        }

        private async Task MaxSingleCharacter(SchaleDataContext context, List<CharacterExcelT> characterExcel, string name)
        {
            var characterData = characterExcel.FirstOrDefault(x => 
                x.DevName != null && 
                x.DevName.Contains(name, StringComparison.OrdinalIgnoreCase));

            if (characterData == null)
            {
                await connection.SendChatMessage($"Character '{name}' not found!");
                return;
            }

            var character = context.Characters.FirstOrDefault(x => 
                x.AccountServerId == connection.AccountServerId && 
                x.UniqueId == characterData.Id);

            if (character == null)
            {
                await connection.SendChatMessage($"You don't own {characterData.DevName}!");
                return;
            }

            character.Level = 90;
            character.Exp = 0;
            character.StarGrade = characterData.MaxStarGrade;
            character.FavorRank = 50;
            character.FavorExp = 0;
            character.PublicSkillLevel = 10;
            character.ExSkillLevel = 5;
            character.PassiveSkillLevel = 10;
            character.ExtraPassiveSkillLevel = 10;

            context.Characters.Update(character);
            await context.SaveChangesAsync();
            await connection.SendChatMessage($"Maxed out {characterData.DevName}! (Lv90, {characterData.MaxStarGrade}★, Favor 50, Skills 10/5/10/10)");
        }
    }
}
