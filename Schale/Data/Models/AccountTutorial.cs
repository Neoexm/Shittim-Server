using System.ComponentModel.DataAnnotations;

namespace Schale.Data.Models
{
    public class AccountTutorial
    {
        [Key]
        public required long AccountServerId { get; set; }

        public List<long> TutorialIds { get; set; } = [];
    }
}


