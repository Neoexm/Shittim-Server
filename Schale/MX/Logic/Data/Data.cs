using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using Schale.MX.Data;

namespace Schale.MX.Logic.Data
{
    public class RaidDamage
    {
        public int Index { get; set; }
        public long GivenDamage { get; set; }
        public long GivenGroggyPoint { get; set; }
    }

    public class RaidMemberCollection : KeyedCollection<long, RaidMemberDescription>
    {
        public long TotalDamage { get; set; }
        protected override long GetKeyForItem(RaidMemberDescription item)
        {
            return -1;
        }
        public IEnumerable<RaidDamage>? RaidDamages { get; set; }
    }

    public class RaidDamageCollection : KeyedCollection<int, RaidDamage>
	{
		protected override int GetKeyForItem(RaidDamage item)
		{
			return item.Index;
		}

        public void Insert(IEnumerable<RaidDamage> enumerable)
        {
            throw new NotImplementedException();
        }

        [JsonIgnore]
		public int MaxIndex { get; set; }
		[JsonIgnore]
		public long TotalDamage { get; set; }
		[JsonIgnore]
		public long CurrentDamage { get; set; }
		[JsonIgnore]
		public long TotalGroggyPoint { get; set; }
		[JsonIgnore]
		public long CurrentGroggyPoint { get; set; }
    }

    public struct RaidBossResult : IEquatable<RaidBossResult>
    {
        [JsonIgnore]
        public int Index { get; set; }
        
        [JsonIgnore]
        public long GivenDamage { get; set; }

        [JsonIgnore]
        public long GivenGroggyPoint { get; set; }

        public RaidDamage RaidDamage { get; set; }
        public long EndHpRateRawValue { readonly get; set; }
        public long GroggyRateRawValue { readonly get; set; }
        public int GroggyCount { readonly get; set; }
        public List<long> SubPartsHPs { readonly get; set; }
        public long AIPhase { readonly get; set; }

        public bool Equals(RaidBossResult other)
        {
            return this.RaidDamage.Index == other.RaidDamage.Index;
        }
    }

    public class RaidBossResultCollection : KeyedCollection<int, RaidBossResult>
    {
		[JsonIgnore]
        public int LastIndex { get; set; }

        [JsonIgnore]
        public long TotalDamage { get; set; }
        
        [JsonIgnore]
        public long CurrentDamage { get; set; }

        [JsonIgnore]
        public long TotalGroggyPoint { get; set; }

        [JsonIgnore]
        public long CurrentGroggyPoint { get; set; }

        [JsonIgnore]
        public int TotalGroggyCount { get; set; }

        protected override int GetKeyForItem(RaidBossResult item)
        {
            return item.RaidDamage.Index;
        }
    }

    [Flags]
    public enum BattleTypes
    {
        None = 0,
        Adventure = 1,
        ScenarioMode = 2,
        WeekDungeonChaserA = 4,
        WeekDungeonBlood = 8,
        WeekDungeonChaserB = 16,
        WeekDungeonChaserC = 32,
        WeekDungeonFindGift = 64,
        EventContent = 128,
        TutorialAdventure = 256,
        Profiling = 512,
        SingleRaid = 2048,
        MultiRaid = 4096,
        PracticeRaid = 8192,
        EliminateRaid = 16384,
        MultiFloorRaid = 32768,
        MinigameDefense = 1048576,
        Arena = 2097152,
        TimeAttack = 8388608,
        SchoolDungeonA = 33554432,
        SchoolDungeonB = 67108864,
        SchoolDungeonC = 134217728,
        WorldRaid = 268435456,
        Conquest = 536870912,
        FieldStory = 1073741824,
        FieldContent = -2147483648,
        PvE = -301988865,
        WeekDungeon = 124,
        SchoolDungeon = 234881024,
        Raid = 30720,
        PvP = 2097152,
        All = -1,
    }
}




