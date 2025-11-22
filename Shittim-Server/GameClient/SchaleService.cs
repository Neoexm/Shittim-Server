using AutoMapper;
using Shittim.GameMasters;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Shittim_Server.Services;
using Schale.Data.ModelMapping;

namespace Shittim_Server.GameClient
{
    public class SchaleService
    {
        private static List<AssistCharacterDB> assistCharData = [];
        private static bool isCreated = false;

        private static long fakeAssistServerId = 1_000_000_000L;
        private static long fakeCharServerId = 1_000_000_000L;
        private static long fakeEqServerId = 1_000_000_000L;
        private static long fakeWpServerId = 1_000_000_000L;
        private static long fakeUniqueEqServerId = 1_000_000_000L;

        public static List<AssistCharacterDB> GetAssistCharacter(EchelonType echelonType)
        {
            assistCharData.ForEach(x => x.EchelonType = echelonType);
            return assistCharData;
        }

        public static async Task CreateArenaEchelonDB(SchaleDataContext context, AccountDBServer account)
        {
            var defenseEchelon = EchelonService.GetArenaDefenseEchelon(context, account.ServerId);

            if (defenseEchelon != null)
                return;

            defenseEchelon = new EchelonDBServer
            {
                AccountServerId = account.ServerId,
                EchelonType = EchelonType.ArenaDefence,
                EchelonNumber = 1,
                ExtensionType = EchelonExtensionType.Base,
                MainSlotCount = 0,
                SupportSlotCount = 0,
                TSSInteractionServerId = 0,
                UsingFlag = 0,
                IsUsing = false,
                AllCharacterServerIds = new List<long>(),
                AllCharacterWithoutTSSServerIds = new List<long>(),
                AllCharacterWithEmptyServerIds = new List<long>(),
                BattleCharacterServerIds = new List<long>(),
                SkillCardMulliganCharacterIds = new List<long>(),
                CombatStyleIndex = Enumerable.Repeat(0, 6).ToArray()
            };

            uint[] mainCharacters = [10000, 13006, 13005, 16000];
            List<CharacterDBServer> mainCharacterDBs = [];
            foreach (uint characterId in mainCharacters)
            {
                await CharacterGM.AddCharacter(context, account, characterId, "max");
                mainCharacterDBs.Add(context.Characters.First(c => c.UniqueId == characterId));
            }
            defenseEchelon.LeaderServerId = context.Characters.First(c => c.UniqueId == mainCharacters[0]).ServerId;
            defenseEchelon.MainSlotServerIds = mainCharacterDBs.Select(c => c.ServerId).ToList();

            uint[] supportCharacters = [20033, 20016];
            List<CharacterDBServer> supportCharacterDBs = [];
            foreach (uint characterId in supportCharacters)
            {
                await CharacterGM.AddCharacter(context, account, characterId, "max");
                supportCharacterDBs.Add(context.Characters.First(c => c.UniqueId == characterId));
            }
            defenseEchelon.SupportSlotServerIds = supportCharacterDBs.Select(c => c.ServerId).ToList();

            context.Echelons.Add(defenseEchelon);
            await context.SaveChangesAsync();
        }

        public static void CreateBatchAssistCharacterDB(ExcelTableService excelTableService, IMapper mapper)
        {
            if(!isCreated)
            {
                var assistCharacterData = new Dictionary<string, (int starGrade, int bond, Dictionary<int, int> potentialStats, int weaponStarGrade, int weaponLvl)>
                {
                    { "3Star", (3, 20, new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } }, 0, 0) },
                    { "UE30", (5, 20, new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } }, 1, 30) },
                    { "UE40", (5, 20, new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } }, 2, 40) },
                    { "UE50", (5, 50, new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } }, 3, 50) },
                    { "UE50MAX", (5, 50, new Dictionary<int, int> { { 1, 25 }, { 2, 25 }, { 3, 25 } }, 3, 50) },
                    { "UE60", (5, 50, new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } }, 4, 60) },
                    { "UE60MAX", (5, 50, new Dictionary<int, int> { { 1, 25 }, { 2, 25 }, { 3, 25 } }, 4, 60) },
                };

                foreach (var character in assistCharacterData)
                {
                    PrepareBatchAssistCharacterDB(
                        excelTableService, mapper,
                        character.Key,
                        character.Value.starGrade,
                        character.Value.bond,
                        character.Value.potentialStats,
                        character.Value.weaponStarGrade,
                        character.Value.weaponLvl
                    );
                }
                isCreated = true;
            }
        }

        private static void PrepareBatchAssistCharacterDB(
            ExcelTableService excelTableService, IMapper mapper,
            string userAssistName, int starGrade, int bond, Dictionary<int, int> potentialStats, int weaponStarGrade, int weaponLvl
        )
        {
            var equipmentExcel = excelTableService.GetTable<EquipmentExcelT>();
            var weaponExcel = excelTableService.GetTable<CharacterWeaponExcelT>();
            var uniqueGearExcel = excelTableService.GetTable<CharacterGearExcelT>();
            var characterExcel = excelTableService.GetTable<CharacterExcelT>()
                .GetReleaseCharacters();

            foreach (var character in characterExcel)
            {
                var equipmentData = character.EquipmentSlot.Select(slot =>
                {
                    var equipmentData = equipmentExcel
                    .GetEquipmentExcelByCategory(slot)
                    .GetCharacterEquipment()
                    .OrderByDescending(y => y.MaxLevel)
                    .FirstOrDefault();
                    if (equipmentData == null) return new();

                    return new EquipmentDB
                    {
                        UniqueId = equipmentData.Id,
                        Level = equipmentData.MaxLevel,
                        Tier = (int)equipmentData.TierInit,
                        StackCount = 1,
                        BoundCharacterServerId = fakeCharServerId,
                        ServerId = fakeEqServerId++
                    };
                }).ToList();


                WeaponDBServer weaponData = new();
                if (weaponStarGrade != 0 && weaponLvl != 0)
                {
                    weaponData = weaponExcel.GetCharacterWeaponExcelByCharacterId(character.Id) is { }
                    ? new WeaponDBServer
                    {
                        UniqueId = character.Id,
                        StarGrade = weaponStarGrade,
                        Level = weaponLvl,
                        BoundCharacterServerId = fakeCharServerId,
                        ServerId = fakeWpServerId++
                    }
                    : new();
                }

                GearDBServer gearData = new();
                if (uniqueGearExcel.Any(x => x.CharacterId == character.Id))
                {
                    var matchingGear = uniqueGearExcel
                        .GetCharacterGearExcelByCharacterId(character.Id)
                        .GetCharacterGearExcelByTier(2);
                    gearData = new GearDBServer
                    {
                        UniqueId = matchingGear.Id,
                        Level = 1,
                        SlotIndex = 4,
                        Tier = 2,
                        Exp = 0,
                        BoundCharacterServerId = fakeCharServerId,
                        ServerId = fakeUniqueEqServerId++
                    };
                }

                var assistCharacter = new AssistCharacterDB
                {
                    AccountId = 100000,
                    AssistCharacterServerId = fakeAssistServerId,
                    AssistRelation = AssistRelation.Stranger,
                    NickName = userAssistName,
                    
                    UniqueId = character.Id,
                    Level = 90,
                    Exp = 0,
                    FavorRank = bond,
                    FavorExp = 0,
                    StarGrade = starGrade,
                    ExSkillLevel = 5,
                    PublicSkillLevel = 10,
                    PassiveSkillLevel = 10,
                    ExtraPassiveSkillLevel = 10,
                    LeaderSkillLevel = 1,
                    PotentialStats = potentialStats,
                    EquipmentServerIds = equipmentData.Select(e => e?.ServerId != 0 ? e.ServerId : 0).ToList(),

                    EquipmentDBs = equipmentData,
                    WeaponDB = weaponData.ToMap(mapper),
                    GearDB = gearData.ToMap(mapper),
                };
                assistCharData.Add(assistCharacter);
                fakeAssistServerId++;
                fakeCharServerId++;
            }
        }
    }
    
    public static class SchaleServiceExtensions
    {

    }
}
