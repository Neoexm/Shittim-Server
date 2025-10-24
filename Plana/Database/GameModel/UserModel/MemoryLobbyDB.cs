using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.GameLogic.Parcel;

namespace Plana.Database.GameModel
{
    public class MemoryLobbyDBServer : ParcelBase
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        public long AccountServerId { get; set; }

        [Key]
        public long ServerId { get; set; }

        public override ParcelType Type { get => ParcelType.MemoryLobby; }
        public long MemoryLobbyUniqueId { get; set; }

        [NotMapped]
        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }
    }

    public static class MemoryLobbyDBServerExtensions
    {
        public static IQueryable<MemoryLobbyDBServer> GetAccountMemoryLobbies(this SCHALEContext context, long accountId)
        {
            return context.MemoryLobbies.Where(x => x.AccountServerId == accountId);
        }

        public static List<MemoryLobbyDBServer> AddMemoryLobbies(this SCHALEContext context, long accountId, params MemoryLobbyDBServer[] memoryLobbies)
        {
            if (memoryLobbies == null || memoryLobbies.Length == 0)
                return new List<MemoryLobbyDBServer>();

            foreach (var lobby in memoryLobbies)
            {
                lobby.AccountServerId = accountId;
                context.MemoryLobbies.Add(lobby);
            }

            return memoryLobbies.ToList();
        }
    }
}