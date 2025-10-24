using Plana.FlatData;

namespace Plana.Excel
{
    public static class CharacterExcelExt
    {
        public static CharacterExcelT GetCharacter(this List<CharacterExcelT> characterExcels, long uniqueId)
        {
            return characterExcels.First(x => x.Id == uniqueId);
        }
        
        public static List<CharacterExcelT> GetReleaseCharacters(this List<CharacterExcelT> characterExcels)
        {
            List<long> otherChar = [20007];
            var releaseCharacters = characterExcels.Where(x => x is
            {
                IsPlayable: true,
                IsPlayableCharacter: true,
                IsDummy: false,
                IsNPC: false,
                ProductionStep: ProductionStep.Release,
                CollectionVisible: true

            }).Concat(characterExcels.Where(x => otherChar.Contains(x.Id)));
            return releaseCharacters.ToList();
        }
    }
}