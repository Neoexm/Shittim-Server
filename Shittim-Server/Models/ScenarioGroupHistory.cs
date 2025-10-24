namespace BlueArchiveAPI.Models
{
    public class ScenarioGroupHistory
    {
        public long AccountServerId { get; set; }
        public long ScenarioGroupId { get; set; }
        public DateTime ClearDate { get; set; }
        public int ClearCount { get; set; }
    }
}