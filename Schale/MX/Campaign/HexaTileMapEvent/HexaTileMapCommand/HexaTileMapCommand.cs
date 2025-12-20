using System;
using Schale.FlatData;
using Schale.MX.NetworkProtocol;

namespace Schale.MX.Campaign.HexaTileMapEvent.HexaTileMapCommand
{
    public abstract class HexaCommand
    {
        public long CommandId { get; set; }
        public virtual HexaCommandType Type { get; set; }
        public Action<HexaTileMap>? VisualizeDelegate { get; set; }
    }

	public class HexaCommandCharacterEmoji : HexaCommand
	{
		public override HexaCommandType Type { get; set; } = HexaCommandType.CharacterEmoji;
		public IList<long>? UnitEntityIds { get; set; }
		public EmojiEvent Emoticon { get; set; }
    }

    public class HexaCommandEndBattle : HexaCommand
    {
        public override HexaCommandType Type { get; set; } = HexaCommandType.EndBattle;
        public CampaignEndBattle EndBattleType;
    }

    public class HexaCommandPlayScenario : HexaCommand
    {
        public override HexaCommandType Type { get; set; } = HexaCommandType.PlayScenario;
        public List<long>? ScenarioList { get; set; }
    }

    public class HexaCommandStrategyHide : HexaCommand
    {
        public override HexaCommandType Type { get; set; } = HexaCommandType.StrategyHide;
        public List<long>? StrategyEntityIds { get; set; }
    }

    public class HexaCommandStrategySpawn : HexaCommand
    {
        public override HexaCommandType Type { get; set; } = HexaCommandType.StrategySpawn;
        public List<long>? StrategyEntityIds { get; set; }
    }

    public class HexaCommandTileHide : HexaCommand
    {
        public override HexaCommandType Type { get; set; } = HexaCommandType.TileHide;
        public List<HexLocation>? TileLocations { get; set; }
    }

    public class HexaCommandTileSpawn : HexaCommand
    {
        public override HexaCommandType Type { get; set; } = HexaCommandType.TileSpawn;
        public List<HexLocation>? TileLocations { get; set; }
    }

    public class HexaCommandUnitDie : HexaCommand
    {
        public override HexaCommandType Type { get; set; } = HexaCommandType.UnitDie;
        public IList<long>? UnitEntityIds { get; set; }
    }

    public class HexaCommandUnitMove : HexaCommand
    {
        public override HexaCommandType Type { get; set; } = HexaCommandType.UnitMove;
        public long EntityId { get; set; }
        public HexLocation Location { get; set; }
    }

    public class HexaCommandUnitSpawn : HexaCommand
    {
        public override HexaCommandType Type { get; set; } = HexaCommandType.UnitSpawn;
        public IList<long>? UnitEntityIds { get; set; }
    }

    public class HexaCommandWaitTurn : HexaCommand
    {
        public override HexaCommandType Type { get; set; } = HexaCommandType.WaitTurn;
        public int Turn { get; set; }
    }

    public enum HexaCommandType
    {
        None = 0,
        UnitSpawn = 1,
        PlayScenario = 2,
        StrategySpawn = 3,
        TileSpawn = 4,
        TileHide = 5,
        EndBattle = 6,
        WaitTurn = 7,
        StrategyHide = 8,
        UnitDie = 9,
        UnitMove = 10,
        CharacterEmoji = 11,
    }
}




