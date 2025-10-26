using Schale.FlatData;

namespace Schale.Excel
{
    public static class CharacterPotentialExcelExt
    {
        public static CharacterPotentialExcelT GetCharacterPotentialByCharacterId(
            this List<CharacterPotentialExcelT> potentials, 
            long characterId, 
            PotentialStatBonusRateType bonusRateType) =>
            potentials.First(p => p.Id == characterId && p.PotentialStatBonusRateType == bonusRateType);
    }
}


