namespace Plana.Database.GameModel
{
    public class AccountGameSettingDB
    {
        public bool BypassTeamDeployment { get; set; } = false;
        public bool EnableArenaTracker { get; set; } = false;
        public bool EnableMultiFloorRaid { get; set; } = false;
        public bool ForceDateTime { get; set; } = false;
        public bool BypassCafeSummon { get; set; } = false;
        public DateTimeOffset ForceDateTimeOffset { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset CurrentDateTime { get; set; } = DateTimeOffset.Now;

        public long ServerDateTimeTicks()
        {
            if (this.ForceDateTime)
                return this.ForceDateTimeOffset.Ticks + (DateTimeOffset.Now.Ticks - this.CurrentDateTime.Ticks);
            else
                return DateTimeOffset.Now.Ticks;
        }

        public DateTime ServerDateTime()
        {
            return new DateTime(ServerDateTimeTicks());
        }

        public DateTimeOffset ServerDateTimeOffset()
        {
            return new DateTimeOffset(ServerDateTimeTicks(), TimeSpan.Zero);
        }
    }
}