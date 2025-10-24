using System.ComponentModel.DataAnnotations;
using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Tracks individual currency transactions for UpdateTimeDict
    /// </summary>
    public class CurrencyTransaction
    {
        [Key]
        public long Id { get; set; }
        
        public long AccountServerId { get; set; }
        
        public CurrencyTypes CurrencyType { get; set; }
        
        public DateTime TransactionTime { get; set; }
        
        public long AmountChange { get; set; }
        
        public long NewBalance { get; set; }
        
        public string Reason { get; set; } = "";
    }
}