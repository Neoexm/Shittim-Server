using Schale.FlatData;

namespace Schale.Excel
{
    public static class CharacterWeaponExpBonusExcelExt
    {
        public static CharacterWeaponExpBonusExcelT GetCharacterWeaponExpBonusExcelById(
            this List<CharacterWeaponExpBonusExcelT> bonuses, WeaponType weaponType) =>
            bonuses.First(bonus => bonus.WeaponType == weaponType);
    }
}


