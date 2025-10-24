using Plana.FlatData;

namespace Plana.Excel
{
    public static class SkillExcelExt
    {
        public static IEnumerable<SkillExcelT> GetSkillExcelByGroupId(this IEnumerable<SkillExcelT> skillExcels, string groupId)
        {
            return skillExcels.Where(x => x.GroupId == groupId);
        }

        public static SkillExcelT GetCharacterSkillByLevel(this IEnumerable<SkillExcelT> skillExcels, long level)
        {
            return skillExcels.First(x => x.Level == level);
        }
    }
}