using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.Campaign;
using Plana.MX.Campaign.HexaTileMapEvent;
using Plana.MX.GameLogic.Parcel;
using Plana.MX.NetworkProtocol;

namespace Plana.Database.GameModel
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
        public Dictionary<long, List<long>> WithdrawInfos { get; set; } = new();
        public Dictionary<long, Strategy> StrategyObjects { get; set; } = new();
        public Dictionary<long, List<ParcelInfo>> StrategyObjectRewards { get; set; } = new();
        public List<long> StrategyObjectHistory { get; set; } = new();
        public Dictionary<long, List<long>> ActivatedHexaEventsAndConditions { get; set; } = new();
        public Dictionary<long, List<long>> HexaEventDelayedExecutions { get; set; } = new();
        public Dictionary<int, HexaTileState> TileMapStates { get; set; } = new();
        public List<HexaDisplayInfo> DisplayInfos { get; set; } = new();
        public List<HexaUnit> DeployedEchelonInfos { get; set; } = new();
    }

    public static class CampaignMainStageSaveDBServerExtensions
    {
        public static IQueryable<CampaignMainStageSaveDBServer> GetAccountCampaignMainStageSaves(this SCHALEContext context, long accountId)
        {
            return context.CampaignMainStageSaves.Where(x => x.AccountServerId == accountId);
        }
    }
}