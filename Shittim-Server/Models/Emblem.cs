namespace BlueArchiveAPI.Models
{
    public class Emblem
    {
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public long UniqueId { get; set; }
        public DateTime ReceiveDate { get; set; }
    }
}