using System.ComponentModel.DataAnnotations;

namespace Schale.Data.Models
{
    public class UserAccount
    {
        [Key]
        public long Id { get; set; }
        
        public long Uid { get; set; }
        
        public long NpSN { get; set; }
        
        public string? NpToken { get; set; }
    }
}


