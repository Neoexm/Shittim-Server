using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.Logic.Data;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.Logic.BattlesEntities;
using System.Text.Json;

namespace Schale.MX.Logic.Battles.Summary
{
    public class ArenaSummary
    {
        public long ArenaMapId { get; set; }
        public long EnemyAccountId { get; set; }
        public long EnemyAccountLevel { get; set; }
    }

    public class RaidSummary
    {
        public long RaidSeasonId { get; set; }
        public long GivenDamage { get; set; }
        public int TotalGroggyCount { get; set; }
        public int RaidBossIndex { get; set; }
        public RaidBossResultCollection? RaidBossResults { get; set; }
    }

    public class BattleSummary : IEquatable<BattleSummary>
    {
        public long HashKey { get; set; }
        public bool IsBossBattle { get; set; }
        public BattleTypes BattleType { get; set; }
        public long StageId { get; set; }
        public long GroundId { get; set; }
        public double ResultValue { get; set; }
        public double UnitType { get; set; }
        
        public GroupTag Winner { get; set; }

        [JsonIgnore]
        public bool IsPlayerWin { get; set; }

        public BattleEndType EndType { get; set; }
        public GroupSummary? Group01Summary { get; set; }
        public GroupSummary? Group02Summary { get; set; }
        public WeekDungeonSummary? WeekDungeonSummary { get; set; }
        public RaidSummary? RaidSummary { get; set; }
        public ArenaSummary? ArenaSummary { get; set; }

        [JsonIgnore]
        public TimeSpan EndTime { get; set; }

        public int EndFrame { get; set; }
        public int ContinueCount { get; set; }
        public float ElapsedRealtime { get; set; }

        [JsonIgnore]
        public string? FindGiftClearText { get; set; }

        [JsonIgnore]
        public long EventContentId { get; set; }

        [JsonIgnore]
        public long FixedEchelonId { get; set; }

        public bool IsAbort { get; set; }
        public bool IsDefeatBattle { get; set; }

        [JsonIgnore]
        public bool IsDefeatFailure { get; }

        public bool Equals(BattleSummary? other)
        {
            return other != null && this.HashKey == other.HashKey;
        }
    }

    public class WeekDungeonSummary : IEquatable<WeekDungeonSummary>
    {
        public WeekDungeonType DungeonType { get; set; }
        public List<FindGiftSummary>? FindGifts { get; set; }

        [JsonIgnore]
        public int TotalFindGiftClearCount { get; }

        public virtual bool Equals(WeekDungeonSummary? other)
        {
            return other != null && other == this;
        }
    }

    public class FindGiftSummary
    {
        public string? UniqueName { get; set; }
        public int ClearCount { get; set; }
    }

    public class GroupSummary : IEquatable<GroupSummary>
    {
        public long TeamId { get; set; }
        public EntityId LeaderEntityId { get; set; }

        [JsonIgnore]
        public long LeaderCharacterId { get; }

        public HeroSummaryCollection? Heroes { get; set; }
        public HeroSummaryCollection? Supporters { get; set; }
        public SkillCostSummary? SkillCostSummary { get; set; }

        [JsonIgnore]
        public int AliveCount { get; }

        public bool UseAutoSkill { get; set; }
        public long TSSInteractionServerId { get; set; }
        public long TSSInteractionUniqueId { get; set; }
        public Dictionary<long, AssistRelation>? AssistRelations { get; set; }

        [JsonIgnore]
        public int StrikerMaxLevel { get; }

        [JsonIgnore]
        public int SupporterMaxLevel { get; }

        [JsonIgnore]
        public int StrikerMinLevel { get; }

        [JsonIgnore]
        public int SupporterMinLevel { get; }

        [JsonIgnore]
        public int MaxCharacterLevel { get; }

        [JsonIgnore]
        public int MinCharacterLevel { get; }

        [JsonIgnore]
        public long TotalDamageGivenApplied { get; }

        public bool Equals(GroupSummary? other)
        {
            return other != null && other == this;
        }
    }

    public class HeroSummaryCollection : KeyedCollection<EntityId, HeroSummary>
    {
        protected override EntityId GetKeyForItem(HeroSummary item)
        {
            return item.BattleEntityId;
        }

        public void Add(IEnumerable<HeroSummary> enumerable)
        {
            foreach (var item in enumerable)
            {
                this.Add(item);
            }
        }
    }

    public class HeroSummary : IEquatable<HeroSummary>
    {
        public long ServerId { get; set; }
        public long OwnerAccountId { get; set; }
        public EntityId BattleEntityId { get; set; }
        public long CharacterId { get; set; }
        public long CostumeId { get; set; }
        public int Grade { get; set; }
        public int Level { get; set; }
        public IDictionary<StatType, int>? PotentialStatLevel { get; set; }
        public int ExSkillLevel { get; set; }
        public int PublicSkillLevel { get; set; }
        public int PassiveSkillLevel { get; set; }
        public int ExtraPassiveSkillLevel { get; set; }
        public int FavorRank { get; set; }
        public StatSnapshotCollection? StatSnapshotCollection { get; set; }
        public long HPRateBefore { get; set; }
        public long HPRateAfter { get; set; }
        public int CrowdControlCount { get; set; }
        public int CrowdControlDuration { get; set; }
        public int EvadeCount { get; set; }
        public int DamageImmuneCount { get; set; }
        public int CrowdControlImmuneCount { get; set; }
        public long MaxAttackPower { get; set; }
        public int AverageCriticalRate { get; set; }
        public int AverageStabilityRate { get; set; }
        public int AverageAccuracyRate { get; set; }
        public int DeadFrame { get; set; }
        public long DamageGivenAbsorbedSum { get; set; }
        public TacticEntityType TacticEntityType { get; set; }

        [JsonIgnore]
        public HeroSummaryDetailFlag DetailFlag { get; }

        [JsonIgnore]
        public bool IsDead { get; }

        public List<BattleNumericLog>? GivenNumericLogs { get; set; }
        public List<BattleNumericLog>? TakenNumericLogs { get; set; }
        public List<BattleNumericLog>? ObstacleBattleNumericLogs { get; set; }
        public List<EquipmentSetting>? Equipments { get; set; }
        public Nullable<WeaponSetting> CharacterWeapon { get; set; }
        public Nullable<GearSetting> CharacterGear { get; set; }

        [JsonIgnore]
        public IDictionary<int, long>? HitPointByFrame { get; set; }
        public IDictionary<SkillSlot, int>? SkillCount { get; set; }

        [JsonIgnore]
        public int ExSkillUseCount { get; }

        // Originally use an KillLogCollection class
        // public KillLogCollection? KillLog { get; set; }
        public Dictionary<string, int>? KillLog { get; set; }

        [JsonIgnore]
        public int KillCount { get; }

        [JsonIgnore]
        public Dictionary<int, string>? FullSnapshot { get; set; }
        
        public static IEqualityComparer<HeroSummary>? HeroSummaryAlmostEqualityComparer { get; }

        public bool Equals(HeroSummary? other)
        {
            return other != null && this.Equals(other);
        }
    }

    public class StatSnapshotCollection : KeyedCollection<StatType, StatSnapshot>
    {
        protected override StatType GetKeyForItem(StatSnapshot item)
        {
            return item.Stat;
        }
    }

    public class StatSnapshot
    {
        public StatType Stat { get; set; }
        public long Start { get; set; }
        public long End { get; set; }
        [JsonIgnore]
        public long Diff { get; }
    }

    [Flags]
    public enum HeroSummaryDetailFlag
    {
        None = 0,
        BattleProperty = 2,
        BattleStatistics = 4,
        NumericLogs = 8,
        StatSnapshot = 16,
        Default = 14,
        All = 30,
    }

    public class BattleNumericLog : IEquatable<BattleNumericLog>
    {
        public BattleEntityType EntityType { get; set; }
        public BattleLogCategory Category { get; set; }
        public BattleLogSourceType Source { get; set; }
        public long CalculatedSum { get; set; }
        public long AppliedSum { get; set; }
        public long Count { get; set; }
        public long CriticalMultiplierMax { get; set; }
        public long CriticalCount { get; set; }
        public long CalculatedMin { get; set; }
        public long CalculatedMax { get; set; }
        public long AppliedMin { get; set; }
        public long AppliedMax { get; set; }
        public bool Equals(BattleNumericLog? other)
        {
            return other != null && this.Equals(other);
        }
    }

    // public class KillLogCollectionConverter : JsonConverter<KillLogCollection>
    // {
    //     public override KillLogCollection Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    //     {
    //         var collection = new KillLogCollection();
    //         reader.Read(); 
    //         while (reader.TokenType != JsonTokenType.EndArray)
    //         {
    //             KillLog? log = JsonSerializer.Deserialize<KillLog>(ref reader, options);
    //             if (log.HasValue)
    //             {
    //                 collection.Add(log.Value);
    //             }
    //             reader.Read(); 
    //         }

    //         return collection;
    //     }

    //     public override void Write(Utf8JsonWriter writer, KillLogCollection value, JsonSerializerOptions options)
    //     {
    //         writer.WriteStartArray();

    //         foreach (var log in value)
    //         {
    //             JsonSerializer.Serialize(writer, log, options);
    //         }

    //         writer.WriteEndArray();
    //     }
    // }

    // [JsonConverter(typeof(KillLogCollectionConverter))]
	// public class KillLogCollection : Collection<KillLog>
	// {
    //     public void Add(int frame, EntityId targetId)
    //     {
    //         this.Add(new KillLog(frame, targetId));
    //     }
	// }

    public struct KillLog : IEquatable<KillLog>
    {
        public int Frame { get; set; }
        public EntityId EntityId { get; set; }

        public KillLog(int frame, EntityId entityId)
        {
            Frame = frame;
            EntityId = entityId;
        }

        public bool Equals(KillLog other)
        {
            return other.EntityId.Equals(EntityId);
        }
    }

    public class CostRegenSnapshotCollection : KeyedCollection<long, SkillCostRegenSnapshot>
    {
        private SkillCostRegenSnapshot _lastSnapshot;
        protected override long GetKeyForItem(SkillCostRegenSnapshot item)
        {
            return item.Frame;
        }
    }

    public struct SkillCostRegenSnapshot
    {
        public long Frame { get; set; }
        public float Regen { get; set; }
    }

    public class TacticSkipSummary
    {
        public long StageId { get; set; }
        public long Group01HexaUnitId { get; set; }
        public long Group02HexaUnitId { get; set; }
    }

    public enum BattleLogCategory
    {
        None = 0,
        Damage = 1,
        Heal = 2,
    }

    public enum BattleLogSourceType
    {
        None = 0,
        Normal = 1,
        Ex = 2,
        Public = 3,
        Passive = 4,
        ExtraPassive = 5,
        Etc = 6
    }

    public struct EquipmentSetting : IEquatable<EquipmentSetting>
    {
        public const int InvalidId = -1;

        [JsonIgnore]
        public bool IsValid { get; }

        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public int Level { get; set; }
        public int Tier { get; set; }

        public bool Equals(EquipmentSetting other)
        {
            return this.Equals(other);
        }
    }

    public struct WeaponSetting : IEquatable<WeaponSetting>
    {
        public const int InvalidId = -1;

        [JsonIgnore]
        public bool IsValid { get; }

        public long UniqueId { get; set; }
        public int StarGrade { get; set; }
        public int Level { get; set; }

        public bool Equals(WeaponSetting other)
        {
            return this.Equals(other);
        }
    }

    public struct GearSetting : IEquatable<GearSetting>
    {
        public const int InvalidId = -1;

        [JsonIgnore]
        public bool IsValid { get; }

        public long UniqueId { get; set; }
        public int Tier { get; set; }
        public int Level { get; set; }

        public bool Equals(GearSetting other)
        {
            return this.Equals(other);
        }
    }

    public class SkillCostSummary
    {
        public float InitialCost { get; set; }
        public CostRegenSnapshotCollection? CostPerFrameSnapshots { get; set; }
        public List<SkillCostAddSnapshot>? CostAddSnapshots { get; set; }
        public List<SkillCostUseSnapshot>? CostUseSnapshots { get; set; }
    }

    public struct SkillCostAddSnapshot
    {
        public long Frame { get; set; }
        public float Added { get; set; }
    }
    
    public struct SkillCostUseSnapshot
	{
		public long Frame { get; set; }
		public float Used { get; set; }
		public long CharId { get; set; }
		public int Level { get; set; }
	}
}




