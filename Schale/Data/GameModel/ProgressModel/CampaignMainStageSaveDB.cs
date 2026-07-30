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
        /// battle summary alone cannot identify it - it carries character ids, not hex entity ids.
        /// Server-only; official has no such wire member.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public long EngagedEnemyEntityId { get; set; }

        /// <summary>
        /// Whether this run is still going. ContentSave_Get answers with the account's one open save and
        /// nothing else, and the client treats any save it gets back as resumable, so finished and
        /// abandoned runs have to stop being visible. Rows are closed rather than deleted - on stage
        /// clear, retreat, ContentSave_Discard, or when a newer run supersedes them - since the history
        /// is worth keeping. Phrased as "open" rather than "closed" so the reconciler's NOT NULL
        /// DEFAULT 0 backfills historical rows as closed; the other polarity resurrects every finished
        /// mission at the next login. Server-only, no official wire member.
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


