using System.ComponentModel.DataAnnotations;

namespace Plana.Database.Models
{
    public class AccountTutorial
    {
        [Key]
        public required long AccountServerId { get; set; }

        public List<long> TutorialIds { get; set; } = [];
    }
}
