namespace BlueArchiveAPI.Models
{
    public class Costume
    {
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long UniqueId { get; set; }
        public bool IsLocked { get; set; }
    }
}