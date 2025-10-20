namespace BlueArchiveAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string UserKey { get; set; }
        public string Gid { get; set; }
        public string Guid { get; set; }
        public string NpSN { get; set; }
        public string UmKey { get; set; }
        public string PlatformType { get; set; }
        public string PlatformUserId { get; set; }
        public string? SteamId { get; set; }
        public string PublisherAccountId { get; set; }
        public string Nickname { get; set; }
        public int Level { get; set; } = 1;
        public string Attribute { get; set; } = "[]";
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }
        public long? LastLogin { get; set; }
        public bool IsGuest { get; set; } = false;
        public bool NeedsNameSetup { get; set; } = false;
        public string ExtraData { get; set; } = "{}";
        public bool IsNew { get; set; } = true;
        public int RepresentCharacterServerId { get; set; } = 9;
        public DateTime LastConnectTime { get; set; } = DateTime.UtcNow;
        public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    }
}
