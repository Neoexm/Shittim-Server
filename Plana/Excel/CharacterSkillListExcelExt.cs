using Plana.FlatData;

namespace Plana.Excel
{
    public static class CharacterSkillListExcelExt
    {
        public static CharacterSkillListExcelT GetCharacterSkillListByCharacterId(this List<CharacterSkillListExcelT> characterSkillListExcels, long characterId)
        {
            return characterSkillListExcels.First(x => x.CharacterSkillListGroupId == characterId);
        }
    }
}