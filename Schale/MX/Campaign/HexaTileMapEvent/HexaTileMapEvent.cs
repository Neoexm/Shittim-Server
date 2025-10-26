using System;
using Schale.FlatData;
using Schale.MX.Campaign.HexaTileMapEvent.HexaTileMapCondition;
using Schale.MX.Campaign.HexaTileMapEvent.HexaTileMapCommand;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using System.Text.Json.Serialization;

namespace Schale.MX.Campaign.HexaTileMapEvent
{
    public class StrategyClearRewardInfo
	{
		public List<ParcelInfo>? FirstClearReward { get; set; }
		public List<ParcelInfo>? ThreeStarReward { get; set; }
		public Dictionary<long, List<ParcelInfo>>? StrategyObjectRewards{ get; set; }
		public ParcelResultDB? ParcelResultDB { get; set; }

		[JsonIgnore]
		public List<ParcelInfo>? ClearReward { get; set; }

		[JsonIgnore]
		public List<ParcelInfo>? ExpReward { get; set; }

		[JsonIgnore]
		public List<ParcelInfo>? TotalReward { get; set; }

		[JsonIgnore]
		public List<ParcelInfo>? EventContentReward { get; set; }

		public List<ParcelInfo>? EventContentBonusReward { get; set; }
		public CampaignStageHistoryDB? CampaignStageHistoryDB { get; set; }
	}

    public class HexaEvent
    {
        public string? EventName { get; set; }
        public long EventId { get; set; }
        public IList<HexaCondition>? HexaConditions { get; set; }
        public MultipleConditionCheckType MultipleConditionCheckType { get; set; }
        public IList<HexaCommand>? HexaCommands { get; set; }
    }

    public class HexaDisplayInfo
	{
		public HexaDisplayType Type { get; set; }
		public long EntityId { get; set; }
		public long UniqueId { get; set; }
		public HexLocation Location { get; set; }
		public long Parameter { get; set; }
		public StrategyClearRewardInfo? StageRewardInfo { get; set; }
	}

    public enum HexaDisplayType
	{
		None,
		EndBattle,
		PlayScenario,
		SpawnUnitFromUniqueId,
		StatBuff,
		DieUnit,
		HideStrategy,
		SpawnUnit,
		SpawnStrategy,
		SpawnTile,
		HideTile,
		ClearFogOfWar,
		MoveUnit,
		WarpUnit,
		SetTileMovablity,
		WarpUnitFromHideTile,
		BossExile
	}
}




