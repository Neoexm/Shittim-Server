using Schale.FlatData;

namespace Schale.Excel
{
    public static class CharacterGearExcelExt
    {
        public static CharacterGearExcelT GetCharacterGearExcelById(this List<CharacterGearExcelT> gears, long id) =>
            gears.First(gear => gear.Id == id);

        public static CharacterGearExcelT GetCharacterGearExcelByTier(this List<CharacterGearExcelT> gears, long tier) =>
            gears.First(gear => gear.Tier == tier);

        public static List<CharacterGearExcelT> GetGearExcelByTier(this List<CharacterGearExcelT> gears, long tier)
        {
            var filtered = gears.Where(gear => gear.Tier == tier);
            return filtered.ToList();
        }

        public static List<CharacterGearExcelT> GetCharacterGearExcelByCharacterId(this List<CharacterGearExcelT> gears, long charId) =>
            gears.Where(gear => gear.CharacterId == charId).ToList();
    }
}


