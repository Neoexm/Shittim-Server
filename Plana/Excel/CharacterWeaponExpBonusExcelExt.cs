using Plana.FlatData;

namespace Plana.Excel
{
    public static class CharacterWeaponExpBonusExcelExt
    {
        public static CharacterWeaponExpBonusExcelT GetCharacterWeaponExpBonusExcelById(this List<CharacterWeaponExpBonusExcelT> weaponExpBonus, WeaponType weaponType)
        {
            return weaponExpBonus.First(x => x.WeaponType == weaponType);
        }
    }
}