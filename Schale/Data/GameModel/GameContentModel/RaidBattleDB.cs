using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.Logic.Data;

namespace Schale.Data.GameModel
{
    public class RaidBattleDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [Key]
        [JsonIgnore]
        public long ServerId { get; set; }

        public ContentType ContentType { get; set; }
        public long RaidUniqueId { get; set; }
        public int RaidBossIndex { get; set; }
        public long CurrentBossHP { get; set; }
        public long CurrentBossGroggy { get; set; }
        public long CurrentBossAIPhase { get; set; }
        public string BIEchelon { get; set; } = string.Empty;
        public bool IsClear { get; set; }
        public RaidMemberCollection RaidMembers { get; set; } = [];
        public List<long>? SubPartsHPs { get; set; } = [];
    }

    public static class RaidBattleDBServerExtensions
    {
        public static IQueryable<RaidBattleDBServer> GetAccountRaidBattles(this SchaleDataContext context, long accountId)
        {
            return context.RaidBattles.Where(x => x.AccountServerId == accountId);
        }
    }
}


