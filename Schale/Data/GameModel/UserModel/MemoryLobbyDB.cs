using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    public class MemoryLobbyDBServer : ParcelBase
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public override ParcelType Type => ParcelType.MemoryLobby;
        public long MemoryLobbyUniqueId { get; set; }

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
    }

    public static class MemoryLobbyDBServerExtensions
    {
        public static IQueryable<MemoryLobbyDBServer> GetAccountMemoryLobbies(this SchaleDataContext context, long accountId)
        {
            return context.MemoryLobbies.Where(x => x.AccountServerId == accountId);
        }

        public static List<MemoryLobbyDBServer> AddMemoryLobbies(this SchaleDataContext context, long accountId, params MemoryLobbyDBServer[] memoryLobbies)
        {
            if (memoryLobbies == null || memoryLobbies.Length == 0)
                return [];

            foreach (var memoryLobby in memoryLobbies)
            {
                memoryLobby.AccountServerId = accountId;
                context.MemoryLobbies.Add(memoryLobby);
            }

            return [.. memoryLobbies];
        }
    }
}


