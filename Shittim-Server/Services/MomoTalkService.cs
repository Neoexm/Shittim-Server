using Schale.Data.GameModel;

namespace BlueArchiveAPI.Services
{
    public static class MomoTalkService
    {
        public static Dictionary<long, List<long>> GetAllFavorSchedules(List<MomoTalkOutLineDBServer> momoTalkOutLines)
        {
            Dictionary<long, List<long>> favorSchedules = new();
            foreach (var outline in momoTalkOutLines)
            {
                favorSchedules[outline.CharacterId] = outline.ScheduleIds;
            }
            return favorSchedules;
        }
    }
}
