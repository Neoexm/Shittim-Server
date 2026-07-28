using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;

namespace Schale.Data.GameModel
{
    public class CampaignMainStageSaveDBServer : ContentSaveDBServer
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        [Key]
        public long ServerId { get; set; }

        public override ContentType ContentType { get; set; }
        public CampaignState CampaignState { get; set; }
        public int CurrentTurn { get; set; }
        public int EnemyClearCount { get; set; }
        public int LastEnemyEntityId { get; set; }
        public int TacticRankSCount { get; set; }
        public Dictionary<long, HexaUnit> EnemyInfos { get; set; } = new();
        public Dictionary<long, HexaUnit> EchelonInfos { get; set; } = new();
        
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<long, List<long>> WithdrawInfos { get; set; } = new();

        /// <summary>
        /// EnemyInfos key of the enemy the player engaged with Campaign_EnterTactic, remembered so
        /// that the following Campaign_TacticResult knows which unit to clear off the map. The
        /// battle summary alone cannot identify it — it carries character ids, not hex entity ids.
        /// Server-only; official has no such wire member.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public long EngagedEnemyEntityId { get; set; }

        /// <summary>
        /// Whether this run is still going. ContentSave_Get answers with the account's one open save
        /// and nothing else — the client treats a save it gets back as resumable, so a finished or
        /// abandoned run has to stop being visible to it. Rows are never deleted (the history is
        /// worth keeping), they are just closed: on stage clear, on retreat, on ContentSave_Discard,
        /// and whenever a newer run supersedes them.
        ///
        /// Deliberately phrased as "open" rather than "closed" so that the column the reconciler adds
        /// to an existing database — NOT NULL DEFAULT 0 — backfills every historical row as closed.
        /// The other polarity would have resurrected months of finished missions at the next login.
        /// Server-only; official has no such wire member.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public bool IsOpen { get; set; }

        public Dictionary<long, Strategy> StrategyObjects { get; set; } = new();
        
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<long, List<ParcelInfo>> StrategyObjectRewards { get; set; } = new();
        
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public List<long> StrategyObjectHistory { get; set; } = new();
        
        public Dictionary<long, List<long>> ActivatedHexaEventsAndConditions { get; set; } = new();
        public Dictionary<long, List<long>> HexaEventDelayedExecutions { get; set; } = new();
        public Dictionary<int, HexaTileState> TileMapStates { get; set; } = new();
        
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
        public List<HexaDisplayInfo> DisplayInfos { get; set; } = new();
        
        public List<HexaUnit> DeployedEchelonInfos { get; set; } = new();
    }

    public static class CampaignMainStageSaveDBServerExtensions
    {
        public static IQueryable<CampaignMainStageSaveDBServer> GetAccountCampaignMainStageSaves(this SchaleDataContext context, long accountId)
        {
            return context.CampaignMainStageSaves.Where(x => x.AccountServerId == accountId);
        }
    }
}


