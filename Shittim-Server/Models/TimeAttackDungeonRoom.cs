namespace BlueArchiveAPI.Models
{
    public class TimeAttackDungeonRoom
    {
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long AccountId { get; set; }
        public long SeasonId { get; set; }
        public long RoomId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime RewardDate { get; set; }
        public bool IsPractice { get; set; }
    }
}