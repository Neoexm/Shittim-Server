using Plana.FlatData;

namespace Plana.Excel
{
    public static class AcademyFavorScheduleExcelExt
    {
        public static AcademyFavorScheduleExcelT? GetScheduleById(this List<AcademyFavorScheduleExcelT> academyFavorSchedules, long ScheduleId)
        {
            return academyFavorSchedules.FirstOrDefault(x => x.Id == ScheduleId);
        }
    }
}