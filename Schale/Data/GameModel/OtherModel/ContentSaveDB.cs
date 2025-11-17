using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    public abstract class ContentSaveDBServer
    {
        public abstract ContentType ContentType { get; set; }
        public long AccountServerId { get; set; }
        public DateTime CreateTime { get; set; }
        public long StageUniqueId { get; set; }
        public long LastEnterStageEchelonNumber { get; set; }
        public List<ParcelInfo> StageEntranceFee { get; set; } = [];
        public Dictionary<long, long> EnemyKillCountByUniqueId { get; set; } = [];
        public long TacticClearTimeMscSum { get; set; }
        public long AccountLevelWhenCreateDB { get; set; }
        public string? BIEchelon { get; set; }
        public string? BIEchelon1 { get; set; }
        public string? BIEchelon2 { get; set; }
        public string? BIEchelon3 { get; set; }
        public string? BIEchelon4 { get; set; }
    }
}


