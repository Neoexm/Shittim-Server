using System;
using Schale.MX.NetworkProtocol;

namespace Schale.MX.Campaign.HexaTileMapEvent.HexaTileMapCondition
{
    public abstract class HexaCondition
    {
        public long ConditionId { get; set; }
        public abstract HexaConditionType Type { get; set; }
        public abstract bool Resuable { get; set; }
        public bool AlreadyTriggered { get; set; }
    }

    public class HexaConditionAnyEnemyArriveTile : HexaCondition
	{
		public override HexaConditionType Type { get; set; } = HexaConditionType.EnemyArrivedInTileFirstTime;
		public override bool Resuable { get; set; }
		public HexLocation TileLocation { get; set; }
	}

    public class HexaConditionAnyEnemyDead : HexaCondition
    {
        public override HexaConditionType Type { get; set; } = HexaConditionType.AnyEnemyDead;
		public override bool Resuable { get; set; }
    }

    public class HexaConditionArriveTile : HexaCondition
	{
		public override HexaConditionType Type { get; set; } = HexaConditionType.PlayerArrivedInTileFirstTime;
		public override bool Resuable { get; set; }
		public HexLocation TileLocation { get; set; }
	}

    public class HexaConditionEveryTurn : HexaCondition
    {
        public override HexaConditionType Type { get; set; } = HexaConditionType.EveryTurn;
		public override bool Resuable { get; set; }
    }

    public class HexaConditionSpecificEnemyArriveTile : HexaCondition
    {
        public override HexaConditionType Type { get; set; } = HexaConditionType.SpecificEnemyArrivedInTileFirstTime;
		public override bool Resuable { get; set; }
        public long EntityId { get; set; }
		public HexLocation TileLocation { get; set; }
    }

    public class HexaConditionStartCampaign : HexaCondition
    {
        public override HexaConditionType Type { get; set; } = HexaConditionType.StartCampaign;
		public override bool Resuable { get; set; }
    }

    public class HexaConditionTurn : HexaCondition
    {
        public override HexaConditionType Type { get; set; } = HexaConditionType.TurnBeginEnd;
		public override bool Resuable { get; set; }
		public CampaignState CampaignState { get; set; }
		public int TurnNumber { get; set; }
    }

    public class HexaConditionUnitDead : HexaCondition
    {
        public override HexaConditionType Type { get; set; } = HexaConditionType.UnitDead;
		public override bool Resuable { get; set; }
        public List<long>? UnitEntityIds { get; set; }
    }

    public enum HexaConditionType
    {
        None = 0,
        StartCampaign = 1,
        TurnBeginEnd = 2,
        UnitDead = 3,
        PlayerArrivedInTileFirstTime = 4,
        AnyEnemyDead = 5,
        EveryTurn = 6,
        EnemyArrivedInTileFirstTime = 7,
        SpecificEnemyArrivedInTileFirstTime = 8,
    }
}




