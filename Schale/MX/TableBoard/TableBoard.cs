using Newtonsoft.Json;
using Schale.FlatData;
using Schale.MX.Campaign;
using Schale.MX.Data;
using Schale.MX.GameLogic.Parcel;

namespace Schale.MX.TableBoard
{
	public class TBGPlayerDB
	{
		// [JsonIgnore]
		// public ITBGSeasonInfo SeasonInfo { get; set; }

		// [JsonIgnore]
		// public int MaxHitPoint { get; set; }

		// [JsonIgnore]
		// public bool IsMaxHitPointReached { get; set; }

		// [JsonIgnore]
		// public IReadOnlyList<ITBGDiceInfo> CurrentDiceInfo { get; set; }

		// [JsonIgnore]
		// public TBGItemEffectDB ActivatedDefenceEffect { get; set; }

		// [JsonIgnore]
		// public TBGItemEffectDB ActivatedDefenceCriticalEffect { get; set; }

		// [JsonIgnore]
		// public TBGItemEffectDB ActivatedGuideEffect { get; set; }

		// [JsonIgnore]
		// public IEnumerable<TBGItemEffectDB> ActivatedDiceAddDotEffect { get; set; }

		// [JsonIgnore]
		// public IEnumerable<TBGItemEffectDB> ActivatedPermanentDiceAddDotEffect { get; set; }

		// [JsonIgnore]
		// public bool IsMaxPermanentDiceAddDotReached { get; set; }

		// [JsonIgnore]
		// public TBGItemEffectDB ActivatedDiceForceDotEffect { get; set; }

		// [JsonIgnore]
		// public TBGItemDB[] ItemSlots { get; set; }

		[JsonProperty(PropertyName = "loc")]
		[JsonConverter(typeof(TBGHexLocationConverter))]
		public HexLocation Location { get; set; }

		[JsonProperty(PropertyName = "ecid")]
		public long EventContentId { get; set; }

		[JsonProperty(PropertyName = "hp")]
		public int HitPoint { get; set; }

		[JsonProperty(PropertyName = "dice")]
		public long DiceId { get; set; }

		[JsonProperty(PropertyName = "diceparams")]
		public Dictionary<TBGProbModifyCondition, int>? DiceProbModifyParams { get; set; }

		[JsonProperty(PropertyName = "itm")]
		public List<TBGItemDB>? Items { get; set; }

		[JsonProperty(PropertyName = "tempitm")]
		public TBGItemDB? TemporaryItem { get; set; }

		// [JsonIgnore]
		// public bool HasItemsDirty { get; set; }

		[JsonProperty(PropertyName = "eff")]
		public List<TBGItemEffectDB>? ItemEffects { get; set; }

		// [JsonIgnore]
		// public bool HasItemEffectDirty { get; set; }

		// [JsonIgnore]
		// public bool IsDead { get; set; }


		// public TBGPlayerDB()
		// {
		// }

		// private TBGBoardSaveDB _saveDB;
		// private ITBGSeasonInfo _seasonInfoCache;
		// private bool _hasItemsDirty;
		// private bool _hasItemEffectDirty;
	}
	
	public interface ITBGItemEffectDB
	{
		ITBGItemInfo ItemInfo { get; }
		int Stack { get; }
		int RemainEncounterCounter { get; }
	}

	public class TBGBoardSaveDB
	{
		[JsonProperty(PropertyName = "aid")]
		public long AccountId { get; set; }

		[JsonProperty(PropertyName = "ecid")]
		public long EventContentId { get; set; }

		[JsonProperty(PropertyName = "rnd")]
		public int Round { get; set; }

		[JsonProperty(PropertyName = "idx")]
		public int ThemaIndex { get; set; }

		[JsonProperty(PropertyName = "map_type")]
		public TBGThemaType CurrentThemaMapType { get; set; }

		[JsonProperty(PropertyName = "map_main")]
		public TBGHexaMapDB? MainMap { get; set; }

		[JsonProperty(PropertyName = "map_hidden")]
		public TBGHexaMapDB? HiddenMap { get; set; }

		[JsonProperty(PropertyName = "usr")]
		public TBGPlayerDB? Player { get; set; }

		// declared as the base type so the runtime subclass decides which of st/ec/real lands on the wire; the client picks the subclass back off "type" in its own converter.
		[JsonProperty(PropertyName = "enc")]
		public TBGEncounterDB? Encounter { get; set; }

		[JsonProperty(PropertyName = "clearlog")]
		public Dictionary<int, TBGThemaClearRecord>? BestClearRecord { get; set; }

		[JsonProperty(PropertyName = "htr_idx")]
		public List<int>? HiddenTreasureRecord { get; set; }

		[JsonProperty(PropertyName = "hcr_idx")]
		public List<int>? HiddenPotalOpenConditionRecord { get; set; }
	}

	public class TBGHexaMapDB
	{
		[JsonProperty]
		public TBGThemaType MapType { get; set; }

		[JsonProperty(PropertyName = "obj")]
		public Dictionary<long, TBGHexaObjectDB>? Objects { get; set; }

		[JsonProperty(PropertyName = "tutorial")]
		public bool IsTutorial { get; set; }
	}

	public class TBGHexaObjectDB
	{
		[JsonProperty(PropertyName = "sid")]
		public long ServerId { get; set; }

		[JsonProperty(PropertyName = "oid")]
		public long UniqueId { get; set; }

		[JsonProperty(PropertyName = "eid")]
		public long EncounterId { get; set; }

		[JsonProperty(PropertyName = "map")]
		public TBGThemaType MapType { get; set; }

		[JsonProperty(PropertyName = "loc")]
		[JsonConverter(typeof(TBGHexLocationConverter))]
		public HexLocation Location { get; set; }

		[JsonProperty(PropertyName = "active")]
		public bool Activated { get; set; }

		[JsonProperty(PropertyName = "hp")]
		public int? HitPoint { get; set; }

		[JsonProperty(PropertyName = "bso")]
		public int? BeforeStoryOption { get; set; }

		[JsonProperty(PropertyName = "paid")]
		public bool EncounterCostAlreadyPaid { get; set; }

		[JsonProperty(PropertyName = "isFake")]
		public bool? IsFakeTreasure { get; set; }

		[JsonProperty(PropertyName = "fixReward")]
		public Dictionary<int, long>? FixRewardUniqueIdByIndex { get; set; }
	}

	public class TBGEncounterDB
	{
		[JsonProperty(PropertyName = "eid")]
		public long EncounterId { get; set; }

		[JsonProperty(PropertyName = "oid")]
		public long InvokerServerId { get; set; }

		[JsonProperty(PropertyName = "type")]
		public int ObjectType { get; set; }

		[JsonProperty(PropertyName = "itm")]
		public bool ShouldDecreaseItemEffectCounter { get; set; }

		[JsonProperty(PropertyName = "rwd")]
		public Dictionary<int, long>? RewardUniqueIdByIndex { get; set; }
	}

	public class TBGBattleEncounterDB : TBGEncounterDB
	{
		public enum TBGBattleEncounterStage
		{
			None = 0,
			BeforeStoryOption = 1,
			PreBattlePhase = 2,
			InBattlePhase = 3,
			PostBattlePhase = 4,
			ExitRunAway = 5,
			ExitBattleVictory = 6,
			EncounterRewardOption = 7,
			ExitBattleRetreat = 8,
		}

		[JsonProperty(PropertyName = "st")]
		public TBGBattleEncounterStage Stage { get; set; }
	}

	public class TBGFacilityEncounterDB : TBGEncounterDB
	{
		public enum TBGFacilityEncounterStage
		{
			None = 0,
			EncounterOption = 1,
			EncounterRewardOption = 2,
			Exit = 3,
		}

		[JsonProperty(PropertyName = "st")]
		public TBGFacilityEncounterStage Stage { get; set; }

		[JsonProperty(PropertyName = "ec")]
		public int EncounterOptionChoice { get; set; }
	}

	public class TBGRandomEncounterDB : TBGEncounterDB
	{
		public enum TBGRandomEncounterStage
		{
			None = 0,
			BeforeStoryOption = 1,
			EncounterOption = 2,
			EncounterRewardOption = 3,
			Exit = 4,
		}

		[JsonProperty(PropertyName = "st")]
		public TBGRandomEncounterStage Stage { get; set; }

		[JsonProperty(PropertyName = "ec")]
		public int EncounterOptionChoice { get; set; }
	}

	public class TBGTreasureBoxEncounterDB : TBGEncounterDB
	{
		public enum TBGTreasureEncounterStage
		{
			None = 0,
			PreReceiveReward = 1,
			PostReceiveReward = 2,
			ExitTreasureFake = 3,
			ExitToClearThema = 4,
			ExitToContinue = 5,
		}

		[JsonProperty(PropertyName = "st")]
		public TBGTreasureEncounterStage Stage { get; set; }

		[JsonProperty(PropertyName = "real")]
		public bool IsRealTreasure { get; set; }
	}

	public class TBGItemDB
	{
		[JsonProperty(PropertyName = "id")]
		public long UniqueId { get; set; }
	}

	public class TBGItemEffectDB
	{
		[JsonProperty(PropertyName = "id")]
		public long ItemUniqueId { get; set; }

		[JsonProperty(PropertyName = "item")]
		public TBGItemType ItemType { get; set; }

		[JsonProperty(PropertyName = "eff")]
		public TBGItemEffectType EffectType { get; set; }

		[JsonProperty(PropertyName = "rc")]
		public int RemainEncounterCounter { get; set; }
	}

	public class TBGThemaClearRecord
	{
		public int ThemaIndex { get; set; }
		public List<ParcelInfo>? SweepCosts { get; set; }
	}

	public enum TBGDiceRollResult
	{
		Failure = 0,
		Success = 1,
		CriticalSuccess = 2,
	}

	// the client sends and reads cube coordinates as one "x,y,z" string here, not as an object - its own converter is a String.Format on the way out and a split on the way back, so an object-shaped loc throws inside ReadJson and takes the whole response with it.
	public class TBGHexLocationConverter : JsonConverter<HexLocation>
	{
		public override void WriteJson(JsonWriter writer, HexLocation value, JsonSerializer serializer)
		{
			writer.WriteValue($"{value.x},{value.y},{value.z}");
		}

		public override HexLocation ReadJson(JsonReader reader, Type objectType, HexLocation existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			var parts = ((string)reader.Value!).Split(',');
			return new HexLocation { x = int.Parse(parts[0]), y = int.Parse(parts[1]), z = int.Parse(parts[2]) };
		}
	}
}




