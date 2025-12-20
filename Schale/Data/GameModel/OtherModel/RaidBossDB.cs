using Schale.FlatData;

namespace Schale.Data.GameModel
{
    public class RaidBossDBServer
    {
        public ContentType ContentType { get; set; }
        public int BossIndex { get; set; }
        public long BossCurrentHP { get; set; }
        public long BossGroggyPoint { get; set; }
    }
}


