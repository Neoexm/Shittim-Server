using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Plana.Database.GameModel
{
	public class MomoTalkOutLineDBServer
	{

		[JsonIgnore]
		public virtual AccountDBServer? Account { get; set; }

		[JsonIgnore]
		public long AccountServerId { get; set; }

		[Key]
		[JsonIgnore]
		public long ServerId { get; set; }

		public long CharacterDBId { get; set; }
		public long CharacterId { get; set; }
		public long LatestMessageGroupId { get; set; }
		public long? ChosenMessageId { get; set; }
		public List<long> ScheduleIds { get; set; } = [];
		public DateTime LastUpdateDate { get; set; }
	}
	
	public static class MomoTalkOutLineDBServerExtensions
	{
		public static IQueryable<MomoTalkOutLineDBServer> GetAccountMomoTalkOutLines(this SCHALEContext context, long accountId)
		{
			return context.MomoTalkOutLines.Where(x => x.AccountServerId == accountId);
		}
	}
}