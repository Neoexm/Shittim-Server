using Schale.FlatData;

namespace Schale.Excel
{
    public static class CharacterSkillListExcelExt
    {
        public static CharacterSkillListExcelT GetCharacterSkillListByCharacterId(
            this List<CharacterSkillListExcelT> skillLists, long characterId) =>
            skillLists.First(list => list.CharacterSkillListGroupId == characterId);
    }
}


