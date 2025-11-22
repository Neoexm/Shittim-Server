using System.Text.Json.Serialization;
using Schale.Data.GameModel;

namespace Shittim.Models.GM
{
    public class ArenaDummyCharacter
    {
        public long CharacterUniqueId { get; set; }
        public int Level { get; set; } = 90;
        public int StarGrade { get; set; } = 5;
        public int PublicSkillLevel { get; set; } = 10;
        public int ExSkillLevel { get; set; } = 5;
        public int PassiveSkillLevel { get; set; } = 10;
        public int ExtraPassiveSkillLevel { get; set; } = 5;
        public int LeaderSkillLevel { get; set; } = 10;

        public List<long> EquipmentUniqueIds { get; set; } = new List<long>();

        public long WeaponUniqueId { get; set; }
    }

    public class CharacterInfo
    {
        [JsonPropertyName("serverId")]
        public long ServerId { get; set; }

        [JsonPropertyName("uniqueId")]
        public int UniqueId { get; set; }

        [JsonPropertyName("starGrade")]
        public int StarGrade { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("exp")]
        public int Exp { get; set; }

        [JsonPropertyName("favorRank")]
        public int FavorRank { get; set; }

        [JsonPropertyName("favorExp")]
        public int FavorExp { get; set; }

        [JsonPropertyName("publicSkillLevel")]
        public int PublicSkillLevel { get; set; }

        [JsonPropertyName("exSkillLevel")]
        public int ExSkillLevel { get; set; }

        [JsonPropertyName("passiveSkillLevel")]
        public int PassiveSkillLevel { get; set; }

        [JsonPropertyName("extraPassiveSkillLevel")]
        public int ExtraPassiveSkillLevel { get; set; }

        [JsonPropertyName("leaderSkillLevel")]
        public int LeaderSkillLevel { get; set; }

        [JsonPropertyName("isFavorite")]
        public bool IsFavorite { get; set; }

        [JsonPropertyName("equipmentServerIds")]
        public List<long> EquipmentServerIds { get; set; }

        [JsonPropertyName("potentialStats")]
        public Dictionary<string, int> PotentialStats { get; set; }

        [JsonPropertyName("equipmentSlotAndDBIds")]
        public object EquipmentSlotAndDBIds { get; set; }
    }

    public class WeaponInfo
    {
        [JsonPropertyName("serverId")]
        public long ServerId { get; set; }

        [JsonPropertyName("uniqueId")]
        public int UniqueId { get; set; }

        [JsonPropertyName("level")]
        public int? Level { get; set; }

        [JsonPropertyName("exp")]
        public int Exp { get; set; }

        [JsonPropertyName("starGrade")]
        public int? StarGrade { get; set; }

        [JsonPropertyName("boundCharacterServerId")]
        public long BoundCharacterServerId { get; set; }
    }

    public class GearInfo
    {
        [JsonPropertyName("serverId")]
        public long ServerId { get; set; }

        [JsonPropertyName("uniqueId")]
        public int UniqueId { get; set; }

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("exp")]
        public int Exp { get; set; }

        [JsonPropertyName("tier")]
        public int Tier { get; set; }

        [JsonPropertyName("slotIndex")]
        public int SlotIndex { get; set; }

        [JsonPropertyName("boundCharacterServerId")]
        public long BoundCharacterServerId { get; set; }
    }

    public class EquippedEquipmentInfo
    {

        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("exp")]
        public int Exp { get; set; }

        [JsonPropertyName("tier")]
        public int Tier { get; set; }

        [JsonPropertyName("boundCharacterServerId")]
        public long BoundCharacterServerId { get; set; }

        [JsonPropertyName("serverId")]
        public long ServerId { get; set; }

        [JsonPropertyName("uniqueId")]
        public int UniqueId { get; set; }

        [JsonPropertyName("stackCount")]
        public int StackCount { get; set; }
    }

    public class ArenaBattleStatEntry
    {
        public List<long> AttackingTeamIds { get; set; }
        public List<long> DefendingTeamIds { get; set; }
        public bool Win { get; set; }
        public DateTime Time { get; set; }
    }

    public class ArenaTeamSummary
    {
        public List<long> AttackingTeamIds { get; set; }
        public List<long> DefendingTeamIds { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public double WinRate { get; set; }
    }

    public class ArenaTeamData
    {
        public List<FullCharacterData?> Main { get; set; } = new List<FullCharacterData?> { null, null, null, null };
        public List<FullCharacterData?> Support { get; set; } = new List<FullCharacterData?> { null, null };
    }

    public class FullCharacterData
    {
        public required CharacterDBServer Character { get; set; }
        public WeaponDBServer? Weapon { get; set; }
        public List<EquipmentDBServer> EquippedEquipment { get; set; } = new List<EquipmentDBServer>();
        public GearDBServer? Gear { get; set; }
    }
}
