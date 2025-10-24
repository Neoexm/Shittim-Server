namespace BlueArchiveAPI.Models
{
    public class GameSettings
    {
        public bool EnableMultiFloorRaid { get; set; } = false;
        public bool ForceDateTime { get; set; } = false;
        public DateTimeOffset ForceDateTimeOffset { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset CurrentDateTime { get; set; } = DateTimeOffset.Now;

        public long ServerDateTimeTicks()
        {
            if (ForceDateTime)
                return ForceDateTimeOffset.Ticks + (DateTimeOffset.Now.Ticks - CurrentDateTime.Ticks);
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

    public class ContentInfo
    {
        public ArenaDataInfo ArenaDataInfo { get; set; } = new();
        public MultiFloorRaidDataInfo MultiFloorRaidDataInfo { get; set; } = new();
    }

    public class ArenaDataInfo
    {
        public long SeasonId { get; set; } = 1;
    }

    public class MultiFloorRaidDataInfo
    {
        public long SeasonId { get; set; } = 1;
    }
}
