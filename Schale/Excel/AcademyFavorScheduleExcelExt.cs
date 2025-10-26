using Schale.FlatData;

namespace Schale.Excel
{
    public static class AcademyFavorScheduleExcelExt
    {
        public static AcademyFavorScheduleExcelT? GetScheduleById(this List<AcademyFavorScheduleExcelT> schedules, long scheduleId) =>
            schedules.FirstOrDefault(schedule => schedule.Id == scheduleId);
    }
}


