namespace BlueArchiveAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string PublisherAccountId { get; set; }
        public string Nickname { get; set; }
        public bool IsNew { get; set; } = true;
    }
}
