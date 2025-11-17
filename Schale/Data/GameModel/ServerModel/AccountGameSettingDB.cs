namespace Schale.Data.GameModel
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
            if (ForceDateTime)
            {
                var offset = DateTimeOffset.Now.Ticks - CurrentDateTime.Ticks;
                return ForceDateTimeOffset.Ticks + offset;
            }
            
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


