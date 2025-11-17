using Schale.FlatData;

namespace Schale.Excel
{
    public static class CharacterExcelExt
    {
        public static CharacterExcelT GetCharacter(this List<CharacterExcelT> characterExcels, long uniqueId) =>
            characterExcels.First(character => character.Id == uniqueId);
        
        public static List<CharacterExcelT> GetReleaseCharacters(this List<CharacterExcelT> characterExcels)
        {
            List<long> specialCharacters = [20007];
            var playableCharacters = characterExcels.Where(character => character is
            {
                IsPlayable: true,
                IsPlayableCharacter: true,
                IsDummy: false,
                IsNPC: false,
                ProductionStep: ProductionStep.Release,
                CollectionVisible: true
            });
            
            var specialItems = characterExcels.Where(character => specialCharacters.Contains(character.Id));
            return playableCharacters.Concat(specialItems).ToList();
        }
    }
}


