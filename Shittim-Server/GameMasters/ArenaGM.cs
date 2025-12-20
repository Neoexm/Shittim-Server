using Shittim.Models.GM;
using System.Text.Json;
using Serilog;

namespace Shittim.GameMasters
{
    public static class ArenaUtils
    {
        private static readonly string ArenaDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ArenaBattleStats");
        private static readonly string ArenaJsonPath = Path.Combine(ArenaDirectory, "arena_battle.json");
        private static readonly object FileLock = new object();

        private static readonly HttpClient _httpClient = new HttpClient();

        static ArenaUtils()
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        private static List<ArenaBattleStatEntry> ReadAllStats()
        {
            CheckPath();

            try
            {
                string jsonString = File.ReadAllText(ArenaJsonPath);
                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return new List<ArenaBattleStatEntry>();
                }
                var entries = JsonSerializer.Deserialize<List<ArenaBattleStatEntry>>(jsonString);
                return entries ?? new List<ArenaBattleStatEntry>();
            }
            catch (Exception ex)
            {
                Log.Error($"Error reading battle stats file: {ex.Message}");
                return new List<ArenaBattleStatEntry>();
            }
        }

        private static void WriteStats(List<ArenaBattleStatEntry> entries)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(entries, options);
            File.WriteAllText(ArenaJsonPath, jsonString);
        }

        public static List<ArenaBattleStatEntry> GetAllStats()
        {
            lock (FileLock)
            {
                try
                {
                    return ReadAllStats();
                }
                catch (Exception ex)
                {
                    Log.Error($"Error retrieving all battle stats: {ex.Message}");
                    return new List<ArenaBattleStatEntry>();
                }
            }
        }

        private static void CheckPath()
        {
            if (!Directory.Exists(ArenaDirectory))
            {
                Directory.CreateDirectory(ArenaDirectory);
            }
            if (!File.Exists(ArenaJsonPath))
            {
                File.WriteAllText(ArenaJsonPath, "[]");
            }
        }

        public static void RecordToJson(List<long> attackingIds, List<long> defendingIds, bool isWin)
        {
            CheckPath();

            var battleEntry = new ArenaBattleStatEntry
            {
                AttackingTeamIds = attackingIds,
                DefendingTeamIds = defendingIds,
                Win = isWin,
                Time = DateTime.Now
            };
            lock (FileLock)
            {
                try
                {
                    List<ArenaBattleStatEntry> allEntries = ReadAllStats();
                    allEntries.Add(battleEntry);
                    WriteStats(allEntries);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to update arena battle statistics file: {ex.Message}", ex);
                }
            }

        }

        public static List<ArenaTeamSummary> GetSummaries()
        {
            List<ArenaBattleStatEntry> allEntries = GetAllStats();
            var summariesMap = new Dictionary<TeamCompositionKey, ArenaTeamSummary>();

            foreach (var entry in allEntries)
            {
                var key = new TeamCompositionKey(entry.AttackingTeamIds, entry.DefendingTeamIds);

                if (!summariesMap.TryGetValue(key, out var summary))
                {
                    summary = new ArenaTeamSummary
                    {
                        AttackingTeamIds = entry.AttackingTeamIds != null ? new List<long>(entry.AttackingTeamIds) : new List<long>(),
                        DefendingTeamIds = entry.DefendingTeamIds != null ? new List<long>(entry.DefendingTeamIds) : new List<long>(),
                        Wins = 0,
                        Losses = 0
                    };
                    summariesMap[key] = summary;
                }

                if (entry.Win)
                {
                    summary.Wins++;
                }
                else
                {
                    summary.Losses++;
                }
            }

            List<ArenaTeamSummary> resultList = new List<ArenaTeamSummary>();
            foreach (var summary in summariesMap.Values)
            {
                int totalGames = summary.Wins + summary.Losses;
                summary.WinRate = totalGames > 0 ? Math.Round((double)summary.Wins / totalGames, 4) : 0.0;
                resultList.Add(summary);
            }

            return resultList;
        }

        public static bool DeleteRecord(ArenaBattleStatEntry recordToDelete)
        {
            lock (FileLock)
            {
                List<ArenaBattleStatEntry> allEntries = ReadAllStats();

                int indexToRemove = allEntries.FindIndex(r =>
                    r.Time == recordToDelete.Time &&
                    r.Win == recordToDelete.Win &&
                    r.AttackingTeamIds.SequenceEqual(recordToDelete.AttackingTeamIds) &&
                    r.DefendingTeamIds.SequenceEqual(recordToDelete.DefendingTeamIds)
                );

                if (indexToRemove != -1)
                {
                    allEntries.RemoveAt(indexToRemove);
                    WriteStats(allEntries);
                    Log.Information($"Successfully deleted arena record from {recordToDelete.Time}.");
                    return true;
                }

                Log.Warning($"Could not find matching arena record to delete for time {recordToDelete.Time}.");
                return false;
            }
        }

        public static int DeleteSummary(List<long> attackingTeamIds, List<long> defendingTeamIds)
        {
            lock (FileLock)
            {
                List<ArenaBattleStatEntry> allEntries = ReadAllStats();
                int originalCount = allEntries.Count;

                allEntries.RemoveAll(r =>
                    r.AttackingTeamIds.SequenceEqual(attackingTeamIds) &&
                    r.DefendingTeamIds.SequenceEqual(defendingTeamIds)
                );

                int deletedCount = originalCount - allEntries.Count;

                if (deletedCount > 0)
                {
                    WriteStats(allEntries);
                    Log.Information($"Successfully deleted {deletedCount} records for the specified team summary.");
                }
                else
                {
                    Log.Warning($"No records found to delete for the specified team summary.");
                }

                return deletedCount;
            }
        }

        private class TeamCompositionKey : IEquatable<TeamCompositionKey>
        {
            public List<long> AttackingTeamIds { get; }
            public List<long> DefendingTeamIds { get; }

            public TeamCompositionKey(List<long> attackingTeamIds, List<long> defendingTeamIds)
            {
                AttackingTeamIds = attackingTeamIds != null ? new List<long>(attackingTeamIds) : new List<long>();
                DefendingTeamIds = defendingTeamIds != null ? new List<long>(defendingTeamIds) : new List<long>();
            }

            public bool Equals(TeamCompositionKey other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;
                return AttackingTeamIds.SequenceEqual(other.AttackingTeamIds) &&
                       DefendingTeamIds.SequenceEqual(other.DefendingTeamIds);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as TeamCompositionKey);
            }

            public override int GetHashCode()
            {
                int hash = 17;
                if (AttackingTeamIds != null)
                {
                    foreach (long id in AttackingTeamIds)
                    {
                        hash = hash * 31 + id.GetHashCode();
                    }
                }
                hash = hash * 31 + 1;
                if (DefendingTeamIds != null)
                {
                    foreach (long id in DefendingTeamIds)
                    {
                        hash = hash * 31 + id.GetHashCode();
                    }
                }
                return hash;
            }
        }
    }
}
