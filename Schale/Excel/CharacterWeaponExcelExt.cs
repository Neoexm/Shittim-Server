using Schale.FlatData;

namespace Schale.Excel
{
    public static class CharacterWeaponExcelExt
    {
        public static CharacterWeaponExcelT GetCharacterWeaponExcelByCharacterId(
            this List<CharacterWeaponExcelT> weapons, long characterId) =>
            weapons.First(w => w.Id == characterId);
    }
}


