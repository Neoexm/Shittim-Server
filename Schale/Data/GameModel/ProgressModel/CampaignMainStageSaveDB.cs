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


