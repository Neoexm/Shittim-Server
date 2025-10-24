using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.GameLogic.Parcel;

namespace Plana.Database.GameModel
{
	public class MultiFloorRaidDBServer
	{
		[JsonIgnore]
		public virtual AccountDBServer? Account { get; set; }

		[JsonIgnore]
		public long AccountServerId { get; set; }

		[JsonIgnore]
		[Key]
		public long ServerId { get; set; }

		public long SeasonId { get; set; }
		public int ClearedDifficulty { get; set; }
		public DateTime LastClearDate { get; set; }
		public int RewardDifficulty { get; set; }
		public DateTime LastRewardDate { get; set; }
		public int ClearBattleFrame { get; set; }
		public bool AllCleared { get; set; }
		public bool HasReceivableRewards { get; set; }
		public List<ParcelInfo>? TotalReceivableRewards { get; set; }
		public List<ParcelInfo>? TotalReceivedRewards { get; set; }
	}
	
	public static class MultiFloorRaidDBServerExtensions
	{
		public static IQueryable<MultiFloorRaidDBServer> GetAccountMultiFloorRaids(this SCHALEContext context, long accountId)
		{
			return context.MultiFloorRaids.Where(x => x.AccountServerId == accountId);
		}
	}
}