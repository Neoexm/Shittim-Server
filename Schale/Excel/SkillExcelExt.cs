using Schale.FlatData;

namespace Schale.Excel
{
    public static class SkillExcelExt
    {
        public static IEnumerable<SkillExcelT> GetSkillExcelByGroupId(
            this IEnumerable<SkillExcelT> skills, string groupId) =>
            skills.Where(skill => skill.GroupId == groupId);

        public static SkillExcelT GetCharacterSkillByLevel(
            this IEnumerable<SkillExcelT> skills, long level) =>
            skills.First(skill => skill.Level == level);
    }
}


