using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class EchelonPresetDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public int GroupIndex { get; set; }
        public int Index { get; set; }
        public string? Label { get; set; }
        public long? LeaderUniqueId { get; set; }
        public long? TSSInteractionUniqueId { get; set; }
        public long[]? StrikerUniqueIds { get; set; }
        public long[]? SpecialUniqueIds { get; set; }
        public int[]? CombatStyleIndex { get; set; }
        public List<long>? MulliganUniqueIds { get; set; }
        public EchelonExtensionType? ExtensionType { get; set; }
        public int? StrikerSlotCount { get; set; }
        public int? SpecialSlotCount { get; set; }
    }

    public static class EchelonPresetDBServerExtensions
    {
        public static IQueryable<EchelonPresetDBServer> GetAccountEchelonPresets(this SchaleDataContext context, long accountId)
        {
            return context.EchelonPresets.Where(x => x.AccountServerId == accountId);
        }

        public static List<EchelonPresetDBServer> AddEchelonPresets(this SchaleDataContext context, long accountId, params EchelonPresetDBServer[] echelonPresets)
        {
            if (echelonPresets == null || echelonPresets.Length == 0)
                return [];

            foreach (var preset in echelonPresets)
            {
                preset.AccountServerId = accountId;
                context.EchelonPresets.Add(preset);
            }

            return [.. echelonPresets];
        }
    }
}


