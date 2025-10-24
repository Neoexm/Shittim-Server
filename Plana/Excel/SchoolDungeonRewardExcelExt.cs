using Plana.FlatData;

namespace Plana.Excel
{
    public static class SchoolDungeonRewardExcelExt
    {
        public static IEnumerable<SchoolDungeonRewardExcelT> GetAllRewardsByGroupId(this IEnumerable<SchoolDungeonRewardExcelT> schoolDungeonRewardExcels, long groupId)
        {
            return schoolDungeonRewardExcels.Where(x => x.GroupId == groupId);
        }
    }
}