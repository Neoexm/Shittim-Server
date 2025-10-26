using Newtonsoft.Json;
using Schale.MX.Campaign;
using Schale.MX.Data;

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

		// [JsonProperty(PropertyName = "loc")]
		// [JsonConverter(typeof(TBGHexLocationConverter))]
		// public HexLocation Location { get; set; }

		// [JsonProperty(PropertyName = "ecid")]
		// public long EventContentId { get; set; }

		// [JsonProperty(PropertyName = "hp")]
		// public int HitPoint { get; set; }

		// [JsonProperty(PropertyName = "dice")]
		// public long DiceId { get; set; }

		// [JsonProperty(PropertyName = "diceparams")]
		// public Dictionary<TBGProbModifyCondition, int> DiceProbModifyParams { get; set; }

		// [JsonProperty(PropertyName = "itm")]
		// public List<TBGItemDB> Items { get; set; }

		// [JsonProperty(PropertyName = "tempitm")]
		// public TBGItemDB TemporaryItem { get; set; }

		// [JsonIgnore]
		// public bool HasItemsDirty { get; set; }

		// [JsonProperty(PropertyName = "eff")]
		// public List<TBGItemEffectDB> ItemEffects { get; set; }

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
}




