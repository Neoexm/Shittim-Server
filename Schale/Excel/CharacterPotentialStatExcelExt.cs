using Schale.FlatData;

namespace Schale.Excel
{
    public static class CharacterPotentialStatExcelExt
    {
        public static IEnumerable<CharacterPotentialStatExcelT> GetCharacterPotentialByGroupId(
            this IEnumerable<CharacterPotentialStatExcelT> potentialStats, long groupId) =>
            potentialStats.Where(stat => stat.PotentialStatGroupId == groupId);

        public static CharacterPotentialStatExcelT GetCharacterPotentialByLevel(
            this IEnumerable<CharacterPotentialStatExcelT> potentialStats, long level) =>
            potentialStats.First(stat => stat.PotentialLevel == level);
    }
}


