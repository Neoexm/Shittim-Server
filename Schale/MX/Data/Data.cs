using System;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Campaign;
using System.Text.Json.Serialization;
using Schale.MX.Logic.Data;
using Schale.MX.Logic.BattlesEntities;

namespace Schale.MX.Data
{
    public class AttendanceBookReward
    {
        public long UniqueId { get; set; }
        public AttendanceType Type { get; set; }
        public AccountState AccountType { get; set; }
        public long DisplayOrder { get; set; }
        public long AccountLevelLimit { get; set; }
        public string? Title { get; set; }
        public string? TitleImagePath { get; set; }
        public AttendanceCountRule CountRule { get; set; }
        public AttendanceResetType CountReset { get; set; }
        public long BookSize { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime StartableEndDate { get; set; }
        public DateTime EndDate { get; set; }
        public long ExpiryDate { get; set; }
        public MailType MailType { get; set; }
        public Dictionary<long, string>? DailyRewardIcons { get; set; }
        public Dictionary<long, List<ParcelInfo>>? DailyRewards { get; set; }
    }

    public class CampaignStageInfo
    {
        public long UniqueId { get; set; }
        public string? DevName { get; set; }
        public long ChapterNumber { get; set; }
        public string? StageNumber { get; set; }
        public long TutorialStageNumber { get; set; }
        public long[]? PrerequisiteScenarioIds { get; set; }
        public int RecommandLevel { get; set; }
        public string? StrategyMap { get; set; }
        public string? BackgroundBG { get; set; }
        public long StoryUniqueId { get; set; }
        public long ChapterUniqueId { get; set; }
        public long DailyPlayCountLimit { get; set; }
        public StageTopography StageTopography { get; set; }
        public int StageEnterCostAmount { get; set; }
        public int MaxTurn { get; set; }
        public int MaxEchelonCount { get; set; }
        public StageDifficulty StageDifficulty { get; set; }
        public HashSet<long>? PrerequisiteStageUniqueIds { get; set; }
        public long DailyPlayLimit { get; set; }
        public TimeSpan PlayTimeLimit { get; set; }
        public long PlayTurnLimit { get; set; }
        public ParcelCost? EnterCost { get; set; }
        public ParcelCost? PurchasePlayCountHardStageCost { get; set; }
        public HexaTileMap? HexaTileMap { get; set; }
        public long StarConditionTurnCount { get; set; }
        public long StarConditionSTacticRackCount { get; set; }
        public long RewardUniqueId { get; set; }
        public long TacticRewardPlayerExp { get; set; }
        public long TacticRewardExp { get; set; }
        public virtual bool ShowClearDeckButton { get; set; }
        public List<ValueTuple<ParcelInfo, RewardTag>>? StageReward { get; set; }
        public List<ValueTuple<ParcelInfo, RewardTag>>? DisplayReward { get; set; }
        public StrategyEnvironment StrategyEnvironment { get; set; }
        public ContentType ContentType { get; set; }
        public long GroundId { get; set; }
        public int StrategySkipGroundId { get; set; }
        public long BattleDuration { get; set; }
        public long BGMId { get; set; }
        public long FixedEchelonId { get; set; }
        public bool IsEventContent { get; set; }
        public ParcelInfo? EnterParcelInfo { get; set; }
        public bool IsDeprecated { get; set; }
        public EchelonExtensionType EchelonExtensionType { get; set; }
    }

    public class RaidMemberDescription : IEquatable<RaidMemberDescription>
    {
        public long AccountId { get; set; }

        public string? AccountName { get; set; }

        public long CharacterId { get; set; }

        [JsonIgnore]
        public long DamageGiven { get; set; }

        [JsonIgnore]
        public long GroggyGiven { get; set; }

        public RaidDamageCollection? DamageCollection { get; set; }

        public bool Equals(RaidMemberDescription? other)
        {
            return other != null && this.AccountId == other.AccountId;
        }
    }

    public class EventContentTreasureInfo
    {
        public long EventContentId { get; set; }
        public int LoopRound { get; set; }
        public string? TitleLocalize { get; set; }
        public string? UsePregabName { get; set; }
        public string? TreasureBGImagePath { get; set; }
    }

    public class EventContentTreasureItem
    {
        public long RewardId { get; set; }
        public int Amount { get; set; }
        public int Width { get; set; }
        public int Weight { get; set; }
        public bool IsHiddenImage { get; set; }
        public bool IsSquare { get; set; }
    }

    public class EventContentTreasureRoundInfo
    {
        public long EventContentId { get; set; }
        public int Round { get; set; }
        public long CostGoodsId { get; set; }
        public long CellRewardId { get; set; }
        public int BoardSizeX { get; set; }
        public int BoardSizeY { get; set; }
        public List<EventContentTreasureItem>? Treasures { get; set; }
        public List<ParcelInfo>? CellRewards { get; set; }
        public List<ParcelInfo>? CellCosts { get; set; }
        public bool IsVisualSortUnstructed { get; set; }
        public int TreasureTotalCount { get; set; }
    }

    public class FavorLevelReward
    {
        public long CharacterId { get; set; }
        public long FavorLevel { get; set; }
        public List<ValueTuple<EquipmentOptionType, long>>? AddedStats { get; set; }
        public List<ParcelInfo>? RewardParcels { get; set; }
    }

    public class MultiSweepParameter
    {
        public MultiSweepParameter() { }

        public MultiSweepParameter(ContentType contentType, long stageId, int sweepCount)
        {
            this.ContentType = contentType;
            this.StageId = stageId;
            this.SweepCount = sweepCount;
        }

        public MultiSweepParameter(long eventContentId, ContentType contentType, long stageId, int sweepCount)
        {
            this.EventContentId = eventContentId;
            this.ContentType = contentType;
            this.StageId = stageId;
            this.SweepCount = sweepCount;
        }

        public Nullable<long> EventContentId;
        public ContentType ContentType;
        public long StageId;
        public int SweepCount;
    }

    public struct DebuffDescription : IEquatable<DebuffDescription>
    {
        public long AccountId { get; set; }
        public string LogicEffectTemplateId { get; set; }
        public string LogicEffectGroupId { get; set; }
        public int LogicEffectLevel { get; set; }
        public int DurationFrame { get; set; }
        public SkillSlot SkillSlot { get; set; }
        public int IssuedTimestamp { get; set; }

        public bool IsValid(int currentTimestamp)
        {
            return default(bool);
        }

        public bool Equals(DebuffDescription other)
        {
            return default(bool);
        }

        public override bool Equals(object? obj)
        {
            return default(bool);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public static bool operator ==(DebuffDescription left, DebuffDescription right)
        {
            return default(bool);
        }

        public static bool operator !=(DebuffDescription left, DebuffDescription right)
        {
            return default(bool);
        }
    }

    public interface ITBGItemInfo
    {
        long UniqueId { get; set; }
        TBGItemType ItemType { get; set; }
        TBGItemEffectType ItemEffectType { get; set; }
        int ItemParameter { get; set; }
        int EncounterCount { get; set; }
        string LocalizeEtcId { get; set; }
        string Icon { get; set; }
        string BuffIcon { get; set; }
        string DiceEffectAniClip { get; set; }
        bool BuffIconHUDVisible { get; set; }
        // string GetItemDesc(LocalizeEtcData localizeEtcData);
    }


    public interface ITBGSeasonInfo
    {
        long EventContentId { get; set; }
        int ItemSlotCount { get; set; }
        long DefaultItemDiceId { get; set; }
        int DefaultEchelonHP { get; set; }
        int HitPointUpperLimit { get; set; }
        int MaxDicePlus { get; set; }
        List<long> EchelonSlotCharacterIds { get; set; }
        List<string> EchelonSlotPortraits { get; set; }
        ParcelInfo EchelonRevivalCost { get; set; }
        int EnemyBossHitPoint { get; set; }
        int EnemyMinionHitPoint { get; set; }
        int AttackDamage { get; set; }
        int CriticalAttackDamage { get; set; }
        int RoundItemSelectLimit { get; set; }
        int InstantClearRound { get; set; }
        long EventUseCostId { get; set; }
        ParcelType EventUseCostType { get; set; }
        int StartThemaIndex { get; set; }
        int LoopThemaIndex { get; set; }
        string MapImagePath { get; set; }
        string MapNameLocalize { get; set; }
    }

    public interface ITBGDiceInfo
    {
        long EventContentId { get; set; }
        long UniqueId { get; set; }
        int DiceGroup { get; set; }
        int DiceResult { get; set; }
        int Prob { get; set; }

        IReadOnlyDictionary<TBGProbModifyCondition, TBGProbModify>? ProbModifies { get; }
        public struct TBGProbModify
        {
            public TBGProbModifyCondition ProbModifyCondition { get; set; }
            public int ProbModifyValue { get; set; }
            public int ProbModifyLimit { get; set; }
        }
    }

    public class TBGDiceInfo : ITBGDiceInfo
    {
        public long EventContentId { get; set; }
        public long UniqueId { get; set; }
        public int DiceGroup { get; set; }
        public int DiceResult { get; set; }
        public int Prob { get; set; }

        public IReadOnlyDictionary<TBGProbModifyCondition, ITBGDiceInfo.TBGProbModify>? ProbModifies { get; }
        public MinigameTBGDiceExcel Excel { get; }
    }

    public enum ConquestEventObjectType
    {
        None,
        UnexpectedEnemy,
        TreasureBox,
        Erosion,
        End
    }
    
    public enum BannerDisplayType
	{
		Lobby,
		Gacha
	}
}




