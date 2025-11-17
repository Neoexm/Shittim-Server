namespace Schale.Data.GameModel
{
    public class TimeAttackDungeonCharacterDBServer
    {
        public long ServerId { get; set; }
        public long UniqueId { get; set; }
        public long CostumeId { get; set; }
        public int StarGrade { get; set; }
        public int Level { get; set; }
        public bool HasWeapon { get; set; }
        public WeaponDBServer? WeaponDB { get; set; }
        public bool IsAssist { get; set; }
        public int CombatStyleIndex { get; set; }
    }
}


