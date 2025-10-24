using Plana.FlatData;

namespace Plana.Excel
{
    public static class CharacterPotentialExcelExt
    {
        public static CharacterPotentialExcelT GetCharacterPotentialByCharacterId(this List<CharacterPotentialExcelT> characterSkillListExcels, long characterId, PotentialStatBonusRateType potentialStatBonusRate)
        {
            return characterSkillListExcels.First(x => x.Id == characterId && x.PotentialStatBonusRateType == potentialStatBonusRate);
        }
    }
}