using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Models
{
    public class Mail
    {
        [Key]
        public long ServerId { get; set; }
        public long AccountServerId { get; set; }
        public int Type { get; set; }
        public long UniqueId { get; set; }
        public string Sender { get; set; } = "Arona";
        public string Comment { get; set; } = "";
        public DateTime SendDate { get; set; }
        public DateTime? ReceiptDate { get; set; }
        public DateTime? ExpireDate { get; set; }
        
        [Column(TypeName = "jsonb")]
        public string? ParcelInfosJson { get; set; }
        
        [Column(TypeName = "jsonb")]
        public string? RemainParcelInfosJson { get; set; }

        [NotMapped]
        public List<ParcelInfo> ParcelInfos
        {
            get => string.IsNullOrEmpty(ParcelInfosJson) 
                ? new List<ParcelInfo>() 
                : JsonSerializer.Deserialize<List<ParcelInfo>>(ParcelInfosJson) ?? new List<ParcelInfo>();
            set => ParcelInfosJson = value == null || value.Count == 0 
                ? null 
                : JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public List<ParcelInfo> RemainParcelInfos
        {
            get => string.IsNullOrEmpty(RemainParcelInfosJson) 
                ? new List<ParcelInfo>() 
                : JsonSerializer.Deserialize<List<ParcelInfo>>(RemainParcelInfosJson) ?? new List<ParcelInfo>();
            set => RemainParcelInfosJson = value == null || value.Count == 0 
                ? null 
                : JsonSerializer.Serialize(value);
        }

        [NotMapped]
        public bool IsRead => ReceiptDate != null;

        [NotMapped]
        public bool IsReceived => ReceiptDate != null;
    }
}
