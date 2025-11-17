using System;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.Campaign.HexaTileMapEvent;
using Schale.MX.GameLogic.Parcel;

namespace Schale.MX.Campaign
{
    public class HexaTileMap
    {
        public static readonly float XOffset;
        public static readonly float YOffset;
        public static readonly float EmptyOffset;
        public static readonly float Up;
        public int LastEntityId { get; set; }
        public bool IsBig { get; set; }
        public List<HexaTile>? HexaTileList { get; set; }
        public List<HexaUnit>? HexaUnitList { get; set; }
        public List<Strategy>? HexaStrageyList { get; set; }
        public List<HexaEvent>? Events { get; set; }

        [JsonIgnore]
        public Dictionary<HexLocation, HexaTile>? TileLocationMap { get; set; }
    }

    [Serializable]
    public class HexaTile
    {
        public string? ResourcePath { get; set; }
        public bool IsHide { get; set; }
        public bool IsFog { get; set; }
        public bool CanNotMove { get; set; }
        public HexLocation Location { get; set; }
        public Strategy? Strategy { get; set; }
        public HexaUnit? Unit { get; set; }
	    [JsonIgnore]
        public HexaUnit? ChallengeUnit { get; set; }

        public bool PlayAnimation { get; set; }
        [JsonIgnore]
        public bool IsBattleReady { get; set; }
        [JsonIgnore]
        public bool StartTile { get; set; }
    }

    public class SimpleVector3
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }

    public class HexLocation2D
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }
    
    public class HexaUnit
    {
        public long EntityId { get; set; }
        public Dictionary<long, long>? HpInfos { get; set; }
        public Dictionary<long, long>? DyingInfos { get; set; }
        public Dictionary<long, int>? BuffInfos { get; set; }
        public int ActionCountMax { get; set; }
        public int ActionCount { get; set; }
        public int Mobility { get; set; }
        public int StrategySightRange { get; set; }
        public long Id { get; set; }
        public virtual SimpleVector3? Rotate { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public virtual HexLocation2D? Location { get; set; }
        
        public HexLocation AIDestination { get; set; }
        public bool IsActionComplete { get; set; }
        public bool IsPlayer { get; set; }
        public bool IsFixedEchelon { get; set; }
        public int MovementOrder { get; set; }
	    public Dictionary<TacticEntityType, List<ParcelInfo>>? RewardParcelInfosWithDropTacticEntityType { get; set; }

        [JsonIgnore]
        public CampaignUnitExcel CampaignUnitExcel { get; set; }
        [JsonIgnore]
        public List<HexaTile>? MovableTiles { get; set; }
        [JsonIgnore]
        public List<List<HexaTile>>? MovementMap { get; set; }

        [JsonIgnore]
        public List<string>? BuffGroupIds { get; set; }
        
        public virtual SkillCardHand? SkillCardHand { get; set; }
        public bool PlayAnimation { get; set; }

        [JsonIgnore]
        public Dictionary<TacticEntityType, List<long>>? RewardItems { get; set; }
    }

    public struct HexLocation
    {
        public int x { get; set; }
        public int y { get; set; }
        public int z { get; set; }

        [JsonIgnore]
        public static readonly int NeighborCount;
        [JsonIgnore]
        public static readonly HexLocation[]? Directions;

        [JsonIgnore]
        public static HexLocation Zero { get; set; }
        [JsonIgnore]
        public static HexLocation Invalid { get; set; }
    }

    public class HexaTileState
	{
		public int Id { get; set; }
		public bool IsHide { get; set; }
		public bool IsFog { get; set; }
		public bool CanNotMove { get; set; }
	}

    public class SkillCardHand
    {
        public float Cost { get; set; }
        public virtual List<SkillCardInfo>? SkillCardsInHand { get; set; }
    }

    public  struct SkillCardInfo
    {
        public long CharacterId { get; set; }
        public int HandIndex { get; set; }
        public string SkillId { get; set; }
        public int RemainCoolTime { get; set; }
    }

    public class Strategy
    {
        public long EntityId { get; set; }
        public SimpleVector3? Rotate { get; set; }
        public long Id { get; set; }
        public HexLocation2D? Location {get; set;}

	    [JsonIgnore]
        public CampaignStrategyObjectExcel CampaignStrategyExcel { get; set; }
    }
}




