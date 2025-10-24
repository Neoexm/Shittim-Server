using System.Text.Json;

namespace BlueArchiveAPI.Services
{
    public class MultiFloorRaidSeasonManageExcelT
    {
        public long SeasonId { get; set; }
        public uint LobbyEnterScenario { get; set; }
        public bool ShowLobbyBanner { get; set; }
        public string SeasonStartDate { get; set; } = string.Empty;
        public string EndNoteLabelStartDate { get; set; } = string.Empty;
        public string SeasonEndDate { get; set; } = string.Empty;
        public string SettlementEndDate { get; set; } = string.Empty;
        public string OpenRaidBossGroupId { get; set; } = string.Empty;
        public uint EnterScenarioKey { get; set; }
        public string LobbyImgPath { get; set; } = string.Empty;
        public string LevelImgPath { get; set; } = string.Empty;
        public string PlayTip { get; set; } = string.Empty;
    }

    public static class MultiFloorRaidSeasonLoader
    {
        private static List<MultiFloorRaidSeasonManageExcelT>? _cachedSeasons;

        public static List<MultiFloorRaidSeasonManageExcelT> LoadSeasons()
        {
            if (_cachedSeasons != null)
                return _cachedSeasons;

            var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "Excel", "MultiFloorRaidSeasonManageExcelT.json");
            
            if (!File.Exists(dataPath))
            {
                _cachedSeasons = new List<MultiFloorRaidSeasonManageExcelT>();
                return _cachedSeasons;
            }

            var json = File.ReadAllText(dataPath);
            _cachedSeasons = JsonSerializer.Deserialize<List<MultiFloorRaidSeasonManageExcelT>>(json)
                ?? new List<MultiFloorRaidSeasonManageExcelT>();
            
            return _cachedSeasons;
        }

        public static long GetSeasonServerTimeTicks(int seasonId, DateTime fallbackServerTime)
        {
            var seasons = LoadSeasons();
            var season = seasons.FirstOrDefault(s => s.SeasonId == seasonId);
            
            if (season != null && DateTime.TryParse(season.SeasonStartDate, out var startDate))
            {
                return startDate.AddDays(2).Ticks;
            }
            
            // Fallback to server time if season not found
            return fallbackServerTime.Ticks;
        }
    }
}
