namespace Schale.Data.GameModel
{
    public class WorldRaidWorldBossDBServer
    {
        public long GroupId { get; set; }
        public long HP { get; set; }
        public long Participants { get; set; } = 0;

        public WorldRaidWorldBossDBServer() { }

        public WorldRaidWorldBossDBServer(long groupId, long hp)
        {
            GroupId = groupId;
            HP = hp;
        }
    }
}


