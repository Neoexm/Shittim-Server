using System.ComponentModel.DataAnnotations;

namespace Plana.Database.Models
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
