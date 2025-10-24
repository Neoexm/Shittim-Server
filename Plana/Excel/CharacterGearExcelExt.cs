using Plana.FlatData;

namespace Plana.Excel
{
    public static class CharacterGearExcelExt
    {
        public static CharacterGearExcelT GetCharacterGearExcelById(this List<CharacterGearExcelT> characterGears, long id)
        {
            return characterGears.First(x => x.Id == id);
        }

        public static CharacterGearExcelT GetCharacterGearExcelByTier(this List<CharacterGearExcelT> characterGears, long tier)
        {
            return characterGears.First(x => x.Tier == tier);
        }

        public static List<CharacterGearExcelT> GetGearExcelByTier(this List<CharacterGearExcelT> characterGears, long tier)
        {
            return characterGears.Where(x => x.Tier == tier).ToList();
        }

        public static List<CharacterGearExcelT> GetCharacterGearExcelByCharacterId(this List<CharacterGearExcelT> characterGears, long charId)
        {
            return characterGears.Where(x => x.CharacterId == charId).ToList();
        }
    }
}