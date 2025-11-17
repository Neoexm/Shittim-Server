using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    public class SchoolDungeonStageHistory
    {
        [Key]
        public long ServerId { get; set; }
        
        public long AccountServerId { get; set; }
        
        public long StageUniqueId { get; set; }
        
        public bool Star1Flag { get; set; }
        
        public bool Star2Flag { get; set; }
        
        public bool Star3Flag { get; set; }
        
        public int BestStarRecord { get; set; }
        
        public DateTime LastPlay { get; set; }
        
        public User User { get; set; }
    }
}
