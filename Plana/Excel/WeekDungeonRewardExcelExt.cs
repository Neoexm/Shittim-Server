using Plana.FlatData;

namespace Plana.Excel
{
    public static class WeekDungeonRewardExcelExt
    {
        public static IEnumerable<WeekDungeonRewardExcelT> GetAllRewardsByGroupId(this IEnumerable<WeekDungeonRewardExcelT> weekDungeonRewardExcels, long groupId)
        {
            return weekDungeonRewardExcels.Where(x => x.GroupId == groupId);
        }
    }
}