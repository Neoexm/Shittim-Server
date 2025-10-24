using Plana.FlatData;

namespace Plana.Excel
{
    public static class CharacterWeaponExcelExt
    {
        public static CharacterWeaponExcelT GetCharacterWeaponExcelByCharacterId(this List<CharacterWeaponExcelT> weapon, long characterId)
        {
            return weapon.First(x => x.Id == characterId);
        }
    }
}