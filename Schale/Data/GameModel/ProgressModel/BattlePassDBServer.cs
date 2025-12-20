using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.GameModel
{
    public class BattlePassDBServer : BattlePassInfoDB
    {
        [Key]
        public long ServerId { get; set; }
        
        public long AccountServerId { get; set; }

        [ForeignKey(nameof(AccountServerId))]
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }
    }
}
