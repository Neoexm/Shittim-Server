namespace Plana.Database.GameModel
{
    public class WorldRaidWorldBossDBServer
    {
        public long GroupId { get; set; }
        public long HP { get; set; }
        public long Participants { get; set; } = 0;

        public WorldRaidWorldBossDBServer() { }

        public WorldRaidWorldBossDBServer(long groupId, long hp)
        {
            this.GroupId = groupId;
            this.HP = hp;
        }
    }
}