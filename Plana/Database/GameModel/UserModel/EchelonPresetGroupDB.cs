using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Plana.FlatData;

namespace Plana.Database.GameModel
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
        public static IQueryable<EchelonPresetGroupDBServer> GetEchelonPresetGroupsByExtensionType(this IQueryable<EchelonPresetGroupDBServer> echelonPresetGroups, EchelonExtensionType extensionType)
        {
            return echelonPresetGroups.Where(x => x.ExtensionType == extensionType);
        }

        public static IQueryable<EchelonPresetGroupDBServer> GetAccountEchelonPresetGroups(this SCHALEContext context, long accountId)
        {
            return context.EchelonPresetGroups.Where(x => x.AccountServerId == accountId);
        }

        public static List<EchelonPresetGroupDBServer> AddEchelonPresetGroups(this SCHALEContext context, long accountId, params EchelonPresetGroupDBServer[] echelonPresetGroups)
        {
            if (echelonPresetGroups == null || echelonPresetGroups.Length == 0)
                return new List<EchelonPresetGroupDBServer>();

            foreach (var echelon in echelonPresetGroups)
            {
                echelon.AccountServerId = accountId;
                context.EchelonPresetGroups.Add(echelon);
            }

            return echelonPresetGroups.ToList();
        }
    }
}