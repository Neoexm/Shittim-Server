using BlueArchiveAPI.Models;

namespace BlueArchiveAPI.Services
{
    public static class MomoTalkService
    {
        public static Dictionary<long, List<long>> GetAllFavorSchedules(List<MomoTalkOutline> momoTalkOutLines)
        {
            // Group by CharacterId and aggregate schedule IDs
            // The schedule list represents the favor level progression for each character
            return momoTalkOutLines
                .GroupBy(o => o.CharacterId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(o => (long)o.FavorLevel).OrderBy(x => x).ToList()
                );
        }
    }
}
