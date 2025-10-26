namespace Schale.MX.Conquest.ConquestTileMapEvent
{
	public class ConquestDisplayInfo : IComparable
	{
		public ConquestTriggerType TriggerType { get; set; }
		public ConquestDisplayType Type { get; set; }
		public long EntityId { get; set; }
		public long TileUniqueId { get; set; }
		public string? Parameter { get; set; }
		public int DisplayOrder { get; set; }
		public bool DisplayOnce { get; set; }

        public int CompareTo(object? obj)
		{
			return 0;
		}
	}

    public enum ConquestTriggerType
	{
		None,
		TileConquer,
		TileUpgrade,
		MapEnter,
		SyncState,
		AcquireCalculateReward,
		UnexpectedEvent,
		MassErosion,
		MassErosionEnd,
		TileErosion,
		TileErosionEnd
	}

    public enum ConquestDisplayType
	{
		None,
		TileConquered,
		TileUpgraded,
		UnexpectedEvent,
		BossOpen,
		PropAnimation,
		PropAnimationAndBlock,
		PropAnimationHoldAndPlay,
		Operator,
		StepComplete,
		MassErosion,
		Erosion,
		ErosionRemove,
		CheckTileErosion,
		StepOpen,
		BossClear,
		HideConquestUI,
		ShowConquestUI,
		HideHexaUI,
		ShowHexaUI,
		StepObjectComplete,
		CameraSetting,
		PlayMapEnterScenario,
		ShowTileConquerReward
	}
}




