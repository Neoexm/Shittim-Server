using Plana.FlatData;

namespace Plana.Excel
{
    public static class CharacterPotentialStatExcelExt
    {
        public static IEnumerable<CharacterPotentialStatExcelT> GetCharacterPotentialByGroupId(this IEnumerable<CharacterPotentialStatExcelT> characterPotentialStatExcels, long groupId)
        {
            return characterPotentialStatExcels.Where(x => x.PotentialStatGroupId == groupId);
        }

        public static CharacterPotentialStatExcelT GetCharacterPotentialByLevel(this IEnumerable<CharacterPotentialStatExcelT> characterPotentialStatExcels, long level)
        {
            return characterPotentialStatExcels.First(x => x.PotentialLevel == level);
        }
    }
}