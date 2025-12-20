using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class EchelonDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public EchelonType EchelonType { get; set; }
        public long EchelonNumber { get; set; }
        public EchelonExtensionType ExtensionType { get; set; }
        public long LeaderServerId { get; set; }
        public int MainSlotCount { get; set; }
        public int SupportSlotCount { get; set; }
        public List<long> MainSlotServerIds { get; set; } = [];
        public List<long> SupportSlotServerIds { get; set; } = [];
        public long TSSInteractionServerId { get; set; }
        public EchelonStatusFlag UsingFlag { get; set; }
        public bool IsUsing { get; set; }
        public List<long> AllCharacterServerIds { get; set; } = [];
        public List<long> AllCharacterWithoutTSSServerIds { get; set; } = [];
        public List<long> AllCharacterWithEmptyServerIds { get; set; } = [];
        public List<long> BattleCharacterServerIds { get; set; } = [];
        public List<long> SkillCardMulliganCharacterIds { get; set; } = [];
        public int[] CombatStyleIndex { get; set; } = [];

        public enum EchelonStatusFlag
        {
            None,
            BeforeDeploy,
            OnDuty
        }
    }

    public static class EchelonDBServerExtensions
    {
        public static IQueryable<EchelonDBServer> GetAccountEchelons(this SchaleDataContext context, long accountId)
        {
            return context.Echelons.Where(x => x.AccountServerId == accountId);
        }

        public static List<EchelonDBServer> AddEchelons(this SchaleDataContext context, long accountId, params EchelonDBServer[] echelons)
        {
            if (echelons == null || echelons.Length == 0)
                return [];

            foreach (var echelon in echelons)
            {
                echelon.AccountServerId = accountId;
                context.Echelons.Add(echelon);
            }

            return [.. echelons];
        }
    }
}


