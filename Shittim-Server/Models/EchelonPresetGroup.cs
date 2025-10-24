using System.ComponentModel.DataAnnotations;

namespace BlueArchiveAPI.Models
{
    /// <summary>
    /// Represents a group of echelon presets
    /// </summary>
    public class EchelonPresetGroup
    {
        [Key]
        public long ServerId { get; set; }
        
        public long AccountServerId { get; set; }
        
        public int GroupIndex { get; set; }
        
        public int ExtensionType { get; set; }
        
        public string GroupLabel { get; set; } = "";
    }
}