using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ClearDeckHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;

    public ClearDeckHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
    }

    [ProtocolHandler(Protocol.ClearDeck_List)]
    public async Task<ClearDeckListResponse> List(
        SchaleDataContext db,
        ClearDeckListRequest request,
        ClearDeckListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // No other players to browse, so the decks come from the account's own strongest students.
        var squadTypes = _excelService.GetTable<CharacterExcelT>()
            .Where(x => x.IsPlayableCharacter && x.SquadType is SquadType.Main or SquadType.Support)
            .ToDictionary(x => x.Id, x => x.SquadType);

        var characters = db.GetAccountCharacters(account.ServerId).ToList()
            .Where(c => squadTypes.ContainsKey(c.UniqueId))
            .OrderByDescending(c => c.StarGrade)
            .ThenByDescending(c => c.Level)
            .ToList();

        var weapons = db.GetAccountWeapons(account.ServerId).ToList()
            .GroupBy(w => w.BoundCharacterServerId)
            .ToDictionary(g => g.Key, g => g.First());

        var strikers = new Queue<CharacterDBServer>(characters.Where(c => squadTypes[c.UniqueId] == SquadType.Main));
        var specials = new Queue<CharacterDBServer>(characters.Where(c => squadTypes[c.UniqueId] == SquadType.Support));

        var echelonType = request.ClearDeckKey.ContentType == ContentType.TimeAttackDungeon
            ? EchelonType.TimeAttack
            : EchelonType.Raid;

        var decks = new List<ClearDeckDB>();
        while (decks.Count < 3 && strikers.Count >= 4 && specials.Count >= 2)
        {
            var members = new List<CharacterDBServer>();
            for (var i = 0; i < 4; i++) members.Add(strikers.Dequeue());
            for (var i = 0; i < 2; i++) members.Add(specials.Dequeue());

            decks.Add(new ClearDeckDB
            {
                LeaderUniqueId = members[0].UniqueId,
                EchelonType = echelonType,
                MulliganUniqueIds = [],
                ClearDeckCharacterDBs = members.Select((c, slot) =>
                {
                    weapons.TryGetValue(c.ServerId, out var weapon);
                    return new ClearDeckCharacterDB
                    {
                        UniqueId = c.UniqueId,
                        StarGrade = c.StarGrade,
                        Level = c.Level,
                        SlotNumber = slot + 1,
                        SquadType = squadTypes[c.UniqueId],
                        HasWeapon = weapon != null,
                        WeaponStarGrade = weapon?.StarGrade ?? 0
                    };
                }).ToList()
            });
        }

        response.ClearDeckDBs = decks;
        return response;
    }
}
