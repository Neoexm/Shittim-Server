using System.ComponentModel.DataAnnotations;

namespace Schale.Data.GameModel
{
    // Beforehand (pre-registration select) gacha state.
    public class BeforehandGachaHistoryDBServer
    {
        [Key]
        public long ServerId { get; set; }

        public long AccountServerId { get; set; }
        public long ShopUniqueId { get; set; }
        public long GoodsId { get; set; }
        public List<long>? SavedResults { get; set; }
        public bool Picked { get; set; }
    }
}
