using AutoMapper;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;

namespace Shittim_Server.Services;

public class ArenaService
{
    public static ArenaTeamSettingDB DummyTeamFormation = new()
    {
        EchelonType = EchelonType.ArenaDefence,
        LeaderCharacterId = 10065,
        MainCharacters =
        [
            new ArenaCharacterDB()
            {
                UniqueId = 10065,
                StarGrade = 3,
                Level = 90,
                PublicSkillLevel = 1,
                ExSkillLevel = 1,
                PassiveSkillLevel = 1,
                ExtraPassiveSkillLevel = 1,
                LeaderSkillLevel = 1
            }
        ],
        MapId = 1001,
    };

    public static List<ArenaUserDB> CreateOpponents(SchaleDataContext context, AccountDBServer account, IMapper mapper)
    {
        List<ArenaUserDB> result = [];

        var accountDefense = CreateArenaUser(
            account.ServerId,
            account.RepresentCharacterServerId,
            "Your Defense",
            2,
            account.Level,
            CreateArenaTeamSetting(context, account, mapper, true));
        result.Add(accountDefense);

        var otherAccounts = context.Accounts
            .Where(x => x.ServerId != account.ServerId)
            .ToList();

        if (otherAccounts.Any())
        {
            var randomAccount = otherAccounts[Random.Shared.Next(otherAccounts.Count)];
            var randomDefense = CreateArenaUser(
                randomAccount.ServerId,
                randomAccount.RepresentCharacterServerId,
                randomAccount.Nickname ?? "Sensei",
                3,
                randomAccount.Level,
                CreateArenaTeamSetting(context, randomAccount, mapper, true));
            result.Add(randomDefense);
        }

        while (result.Count < 3)
        {
            var dummyDefense = CreateArenaUser(-1, 10065, "Dummy Opponent", result.Count + 2, 90, DummyTeamFormation);
            result.Add(dummyDefense);
        }

        return result;
    }

    public static ArenaUserDB CreateArenaUser(long accountId, long representCharacterServerId, string nickname, long rank, int level, ArenaTeamSettingDB teamSettingDB)
    {
        return new ArenaUserDB()
        {
            AccountServerId = accountId,
            RepresentCharacterUniqueId = representCharacterServerId,
            NickName = nickname ?? "Unknown Sensei",
            Rank = rank,
            Level = level,
            TeamSettingDB = teamSettingDB
        };
    }

    public static ArenaTeamSettingDB CreateArenaTeamSetting(
        SchaleDataContext context, AccountDBServer account, IMapper mapper, bool isDefense = false)
    {
        var echelonType = isDefense ? EchelonType.ArenaDefence : EchelonType.ArenaAttack;
        var arenaEchelon = context.GetAccountEchelons(account.ServerId)
            .OrderBy(e => e.ServerId)
            .LastOrDefault(e =>
                e.EchelonType == echelonType &&
                e.EchelonNumber == 1 &&
                e.ExtensionType == EchelonExtensionType.Base);

        if (arenaEchelon == null) return DummyTeamFormation;

        var leaderCharacterId = context.Characters
            .FirstOrDefault(c => c.ServerId == arenaEchelon.LeaderServerId)?.UniqueId ?? 0;

        return new ArenaTeamSettingDB()
        {
            EchelonType = arenaEchelon.EchelonType,
            LeaderCharacterId = leaderCharacterId,
            MainCharacters = CreateArenaCharacterDBs(context, account, mapper, arenaEchelon.MainSlotServerIds),
            SupportCharacters = CreateArenaCharacterDBs(context, account, mapper, arenaEchelon.SupportSlotServerIds),
            MapId = 1001
        };
    }

    public static List<ArenaCharacterDB> CreateArenaCharacterDBs(SchaleDataContext context, AccountDBServer account, IMapper mapper, List<long> characterServerIds)
    {
        List<ArenaCharacterDB> arenaCharacters = [];
        
        foreach (var characterServerId in characterServerIds)
        {
            var character = context.GetAccountCharacters(account.ServerId)
                .FirstOrDefault(c => c.ServerId == characterServerId);
            if (character == null) continue;

            var arenaCharacter = new ArenaCharacterDB
            {
                UniqueId = character.UniqueId,
                StarGrade = character.StarGrade,
                Level = character.Level,
                PublicSkillLevel = character.PublicSkillLevel,
                ExSkillLevel = character.ExSkillLevel,
                PassiveSkillLevel = character.PassiveSkillLevel,
                ExtraPassiveSkillLevel = character.ExtraPassiveSkillLevel,
                LeaderSkillLevel = character.LeaderSkillLevel,
                FavorRankInfo = new() { { character.UniqueId, character.FavorRank } },
                PotentialStats = character.PotentialStats,
                EquipmentDBs = character.EquipmentServerIds
                    .Select(i => context.GetAccountEquipments(account.ServerId).FirstOrDefault(e => e.ServerId == i))
                    .Where(e => e != null)
                    .Select(e => e!.ToMap(mapper))
                    .ToList()
            };

            var weaponDB = context.GetAccountWeapons(account.ServerId)
                .FirstOrDefault(w => w.BoundCharacterServerId == character.ServerId);
            if (weaponDB != null) 
                arenaCharacter.WeaponDB = weaponDB.ToMap(mapper);

            var gearDB = context.GetAccountGears(account.ServerId)
                .FirstOrDefault(g => g.BoundCharacterServerId == character.ServerId);
            if (gearDB != null) 
                arenaCharacter.GearDB = gearDB.ToMap(mapper);

            arenaCharacters.Add(arenaCharacter);
        }

        return arenaCharacters;
    }
}
