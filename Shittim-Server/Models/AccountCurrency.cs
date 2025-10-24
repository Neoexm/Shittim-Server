using System.ComponentModel.DataAnnotations;
using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents the currency holdings for a player account
    /// </summary>
    public class AccountCurrency
    {
        [Key]
        public long ServerId { get; set; }
        
        public long AccountServerId { get; set; }
        public long AccountLevel { get; set; } = 1;
        public long AcademyLocationRankSum { get; set; } = 1;
        
        // Serialized as JSON in database - contains CurrencyTypes enum keys and long values
        public string CurrencyDict { get; set; } = "{}";
        public string UpdateTimeDict { get; set; } = "{}";
        
        // Navigation property
        public User User { get; set; }
    }
}