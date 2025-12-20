namespace Shittim.Models.GM
{
    public class GetUserRequest : BaseAPIRequest
    {
    }

    public class CommandRequest : BaseAPIRequest
    {
        public string Command { get; set; } = string.Empty;
    }

    public class SetRaidRequest : BaseAPIRequest
    {
        public string RaidType { get; set; }
        public int SeasonId { get; set; }
    }

    public class ModifyCharacterRequest : BaseAPIRequest
    {
        public CharacterInfo Character { get; set; }
        public WeaponInfo? Weapon { get; set; }
        public List<EquippedEquipmentInfo> EquippedEquipment { get; set; } = new List<EquippedEquipmentInfo>();
        public GearInfo? Gear { get; set; }
    }

    public class ArenaDeleteRecordRequest : BaseAPIRequest
    {
        public ArenaBattleStatEntry Record { get; set; }
    }

    public class ArenaDeleteSummaryRequest : BaseAPIRequest
    {
        public List<long> AttackingTeamIds { get; set; }
        public List<long> DefendingTeamIds { get; set; }
    }

    public class GetRaidResponse
    {
        public List<TotalAssaultDataWeb> TotalAssault { get; set; }
        public List<GrandAssaultDataWeb> GrandAssault { get; set; }
        public List<TADDataWeb> TimeAttackDungeon { get; set; }
        public List<MultiFloorDataWeb> MultiFloor { get; set; }
    }

    public class TotalAssaultDataWeb
    {
        public long SeasonId { get; set; }
        public string BossName { get; set; }
        public string Date { get; set; }
        public string PortraitPath { get; set; }
        public string BGPath { get; set; }
        public string GroundType { get; set; }
        public string AttackType { get; set; }
        public string ArmorType { get; set; }
    }

    public class GrandAssaultDataWeb
    {
        public long SeasonId { get; set; }
        public string BossName { get; set; }
        public string Date { get; set; }
        public string PortraitPath { get; set; }
        public string BGPath { get; set; }
        public string GroundType { get; set; }
        public string AttackType { get; set; }
        public List<string> ArmorTypes { get; set; }
    }

    public class TADDataWeb
    {
        public long Id { get; set; }
        public string Date { get; set; }
        public string DungeonType { get; set; }
        public List<string> GeasIconPaths { get; set; }
    }

    public class MultiFloorDataWeb
    {
        public long SeasonId { get; set; }
        public string BossName { get; set; }
        public string Date { get; set; }
        public string GroundType { get; set; }
        public string AttackType { get; set; }
        public string ArmorType { get; set; }
    }
    public class SetSettingsRequest : BaseAPIRequest
    {
        public bool? TrackPvp { get; set; }
        public bool? UseFinal { get; set; }
        public bool? BypassTeam { get; set; }
        public bool? BypassSummon { get; set; }

        public ChangetimeDto Changetime { get; set; }
    }

    public class ChangetimeDto
    {
        public bool? Enabled { get; set; }
        public int? Offset { get; set; }
    }

    public class GetRaidRecordsRequest : BaseAPIRequest
    {
        public long SeasonId { get; set; }
    }
    public class RaidRecordsResponse
    {
        public List<TotalAssaultBattleRecord> records { get; set; } = new();
    }

    public class GrandAssaultRecordsResponse
    {
        public List<GrandAssaultBattleRecord> records { get; set; } = new();
    }

    public class TotalAssaultBattleRecord
    {
        public long BattleId { get; set; }
        public int Score { get; set; }
        public int Difficulty { get; set; }
        public Dictionary<int, List<RaidTeamMember>> Teams { get; set; }
    }

    public class GrandAssaultBattleRecord
    {
        public long BattleId { get; set; }
        public int Score { get; set; }
        public int Difficulty { get; set; }
        public string Armor { get; set; }
        public Dictionary<int, List<RaidTeamMember>> Teams { get; set; }
    }

    public class RaidTeamMember
    {
        public long Id { get; set; }
        public int Level { get; set; }
        public int StarGrade { get; set; }
        public long WeaponStarGrade { get; set; }
    }
}
