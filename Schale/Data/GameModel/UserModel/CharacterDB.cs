using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Schale.Data.GameModel
{
    public class CharacterDBServer : ParcelBase
    {
        [NotMapped]
        public override ParcelType Type => ParcelType.Character;

        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        [JsonIgnore]
        public long AccountServerId { get; set; }

        [JsonIgnore]
        [NotMapped]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }

        [Key]
        public long ServerId { get; set; }

        public long UniqueId { get; set; }
        public int StarGrade { get; set; } = 1;
        public int Level { get; set; } = 1;
        public long Exp { get; set; } = 0;
        public int FavorRank { get; set; } = 1;
        public long FavorExp { get; set; } = 0;
        public int PublicSkillLevel { get; set; } = 1;
        public int ExSkillLevel { get; set; } = 1;
        public int PassiveSkillLevel { get; set; } = 1;
        public int ExtraPassiveSkillLevel { get; set; } = 1;
        public int LeaderSkillLevel { get; set; } = 1;
        public bool IsFavorite { get; set; } = false;
        public List<long> EquipmentServerIds { get; set; } = [.. Enumerable.Repeat(0L, 3)];
        public Dictionary<int, int> PotentialStats { get; set; } = new() { { 1, 0 }, { 2, 0 }, { 3, 0 } };
        public Dictionary<int, long> EquipmentSlotAndDBIds { get; set; } = [];

        public CharacterDBServer() { }

        public CharacterDBServer(long accountId) : this()
        {
            AccountServerId = accountId;
        }

        public CharacterDBServer(long accountId, long uniqueId) : this(accountId)
        {
            UniqueId = uniqueId;
        }
    }

    public static class CharacterDBServerExtensions
    {
        public static IQueryable<CharacterDBServer> GetAccountCharacters(this SchaleDataContext context, long accountId)
        {
            return context.Characters.Where(x => x.AccountServerId == accountId);
        }

        public static CharacterDBServer GetCharacterByUniqueId(this IQueryable<CharacterDBServer> characters, long characterId)
        {
            return characters.First(x => x.UniqueId == characterId);
        }

        public static CharacterDBServer GetCharacterByServerId(this IQueryable<CharacterDBServer> characters, long serverId)
        {
            return characters.First(x => x.ServerId == serverId);
        }

        public static List<CharacterDBServer> AddCharacters(this SchaleDataContext context, long accountId, params CharacterDBServer[] characters)
        {
            if (characters == null || characters.Length == 0)
                return [];

            foreach (var character in characters)
            {
                character.AccountServerId = accountId;
                context.Characters.Add(character);
            }

            return [.. characters];
        }
    }
}


