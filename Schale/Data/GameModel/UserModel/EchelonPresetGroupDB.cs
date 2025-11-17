using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class EchelonPresetGroupDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public int? GroupIndex { get; set; }
        public EchelonExtensionType? ExtensionType { get; set; }
        public string? GroupLabel { get; set; }
        public Dictionary<int, EchelonPresetDBServer>? PresetDBs { get; set; }
        public EchelonPresetDBServer? Item { get; set; }
    }

    public static class EchelonPresetGroupDBServerExtensions
    {
        public static IQueryable<EchelonPresetGroupDBServer> GetAccountEchelonPresetGroups(this SchaleDataContext context, long accountId)
        {
            return context.EchelonPresetGroups.Where(x => x.AccountServerId == accountId);
        }

        public static IQueryable<EchelonPresetGroupDBServer> GetEchelonPresetGroupsByExtensionType(this IQueryable<EchelonPresetGroupDBServer> echelonPresetGroups, EchelonExtensionType extensionType)
        {
            return echelonPresetGroups.Where(x => x.ExtensionType == extensionType);
        }

        public static List<EchelonPresetGroupDBServer> AddEchelonPresetGroups(this SchaleDataContext context, long accountId, params EchelonPresetGroupDBServer[] echelonPresetGroups)
        {
            if (echelonPresetGroups == null || echelonPresetGroups.Length == 0)
                return [];

            foreach (var group in echelonPresetGroups)
            {
                group.AccountServerId = accountId;
                context.EchelonPresetGroups.Add(group);
            }

            return [.. echelonPresetGroups];
        }
    }
}


