using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
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

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
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
	    [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public HexaUnit? ChallengeUnit { get; set; }

        public bool PlayAnimation { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public bool IsBattleReady { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
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
        
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public float z { get; set; }
    }
    
    public class HexaUnit
    {
        public long EntityId { get; set; }
        public Dictionary<long, long>? HpInfos { get; set; }
        public Dictionary<long, long>? DyingInfos { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<long, int>? BuffInfos { get; set; }
        
        public int ActionCountMax { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ActionCount { get; set; }
        
        public int Mobility { get; set; }
        public int StrategySightRange { get; set; }
        public long Id { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public virtual SimpleVector3? Rotate { get; set; }

        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public virtual HexLocation2D? Location { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public HexLocation AIDestination { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public bool IsActionComplete { get; set; }
        
        public bool IsPlayer { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public bool IsFixedEchelon { get; set; }
        
        public int MovementOrder { get; set; }
        
	    public Dictionary<TacticEntityType, List<ParcelInfo>>? RewardParcelInfosWithDropTacticEntityType { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public CampaignUnitExcel CampaignUnitExcel { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public List<HexaTile>? MovableTiles { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public List<List<HexaTile>>? MovementMap { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public List<string>? BuffGroupIds { get; set; }
        
        public virtual SkillCardHand? SkillCardHand { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public bool PlayAnimation { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Dictionary<TacticEntityType, List<long>>? RewardItems { get; set; }
    }

    public struct HexLocation
    {
        public int x { get; set; }
        public int y { get; set; }
        public int z { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public static readonly int NeighborCount;
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public static readonly HexLocation[]? Directions;

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public static HexLocation Zero { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
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
        
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public SimpleVector3? Rotate { get; set; }
        
        public long Id { get; set; }
        public HexLocation2D? Location {get; set;}

	    [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public CampaignStrategyObjectExcel CampaignStrategyExcel { get; set; }
    }
}




