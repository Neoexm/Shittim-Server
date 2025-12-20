using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Services;

public class AronaService
{
    private readonly ExcelTableService _excelTableService;
    private readonly IMapper _mapper;
    private static List<AssistCharacterDB> _assistCharData = new();
    private static bool _isCreated = false;

    private static long _fakeAssistServerId = 1_000_000_000L;
    private static long _fakeCharServerId = 1_000_000_000L;
    private static long _fakeEqServerId = 1_000_000_000L;
    private static long _fakeWpServerId = 1_000_000_000L;
    private static long _fakeUniqueEqServerId = 1_000_000_000L;

    private readonly List<EquipmentExcelT> equipmentExcels;
    private readonly List<CharacterWeaponExcelT> weaponExcels;
    private readonly List<CharacterGearExcelT> uniqueGearExcels;
    private readonly List<CharacterExcelT> characterExcels;
    private readonly List<CharacterLevelExcelT> characterLevelExcels;

    public AronaService(ExcelTableService excelTableService, IMapper mapper)
    {
        _excelTableService = excelTableService;
        _mapper = mapper;

        equipmentExcels = _excelTableService.GetTable<EquipmentExcelT>();
        weaponExcels = _excelTableService.GetTable<CharacterWeaponExcelT>();
        uniqueGearExcels = _excelTableService.GetTable<CharacterGearExcelT>();
        characterExcels = _excelTableService.GetTable<CharacterExcelT>();
        characterLevelExcels = _excelTableService.GetTable<CharacterLevelExcelT>();
    }

    public List<AssistCharacterDB> GetAssistCharacter(EchelonType echelonType)
    {
        _assistCharData.ForEach(x => x.EchelonType = echelonType);
        return _assistCharData;
    }

    public async Task CreateArenaEchelon(SchaleDataContext context, AccountDBServer account)
    {
        var defenseEchelon = context.Echelons.FirstOrDefault(x =>
            x.AccountServerId == account.ServerId &&
            x.EchelonType == EchelonType.ArenaDefence &&
            x.EchelonNumber == 1);

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
        List<CharacterDBServer> mainCharacterDBs = new();
        foreach (uint characterId in mainCharacters)
        {
            await AddCharacter(context, account, characterId, "max");
            mainCharacterDBs.Add(context.Characters.First(c => c.UniqueId == characterId && c.AccountServerId == account.ServerId));
        }
        defenseEchelon.LeaderServerId = mainCharacterDBs[0].ServerId;
        defenseEchelon.MainSlotServerIds = mainCharacterDBs.Select(c => c.ServerId).ToList();

        uint[] supportCharacters = [20033, 20016];
        List<CharacterDBServer> supportCharacterDBs = new();
        foreach (uint characterId in supportCharacters)
        {
            await AddCharacter(context, account, characterId, "max");
            supportCharacterDBs.Add(context.Characters.First(c => c.UniqueId == characterId && c.AccountServerId == account.ServerId));
        }
        defenseEchelon.SupportSlotServerIds = supportCharacterDBs.Select(c => c.ServerId).ToList();

        context.Echelons.Add(defenseEchelon);
        await context.SaveChangesAsync();
    }

    public void CreateBatchAssistCharacters()
    {
        if (_isCreated)
            return;

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
            PrepareBatchAssistCharacter(
                character.Key,
                character.Value.starGrade,
                character.Value.bond,
                character.Value.potentialStats,
                character.Value.weaponStarGrade,
                character.Value.weaponLvl
            );
        }
        _isCreated = true;
    }

    private void PrepareBatchAssistCharacter(
        string userAssistName, int starGrade, int bond, Dictionary<int, int> potentialStats, int weaponStarGrade, int weaponLvl)
    {
        var equipmentExcel = equipmentExcels;
        var weaponExcel = weaponExcels;
        var uniqueGearExcel = uniqueGearExcels;
        var characterExcel = characterExcels
            .Where(c => c.IsPlayable && !c.IsNPC)
            .ToList();

        foreach (var character in characterExcel)
        {
            var equipmentData = character.EquipmentSlot.Select(slot =>
            {
                var equipmentDataForSlot = equipmentExcel
                    .Where(e => e.EquipmentCategory == slot)
                    .OrderByDescending(y => y.MaxLevel)
                    .FirstOrDefault();

                if (equipmentDataForSlot == null)
                    return new EquipmentDB();

                return new EquipmentDB
                {
                    UniqueId = equipmentDataForSlot.Id,
                    Level = equipmentDataForSlot.MaxLevel,
                    Tier = (int)equipmentDataForSlot.TierInit,
                    StackCount = 1,
                    BoundCharacterServerId = _fakeCharServerId,
                    ServerId = _fakeEqServerId++
                };
            }).ToList();

            WeaponDB weaponData = new();
            if (weaponStarGrade != 0 && weaponLvl != 0)
            {
                var weaponExcelData = weaponExcel.FirstOrDefault(w => w.Id == character.Id);
                if (weaponExcelData != null)
                {
                    weaponData = new WeaponDB
                    {
                        UniqueId = character.Id,
                        StarGrade = weaponStarGrade,
                        Level = weaponLvl,
                        BoundCharacterServerId = _fakeCharServerId
                    };
                    _fakeWpServerId++;
                }
            }

            GearDB gearData = new();
            var matchingGear = uniqueGearExcel
                .Where(x => x.CharacterId == character.Id && x.Tier == 2)
                .FirstOrDefault();
            if (matchingGear != null)
            {
                gearData = new GearDB
                {
                    UniqueId = matchingGear.Id,
                    Level = 1,
                    SlotIndex = 4,
                    Tier = 2,
                    Exp = 0,
                    BoundCharacterServerId = _fakeCharServerId,
                    ServerId = _fakeUniqueEqServerId++
                };
            }

            var assistCharacter = new AssistCharacterDB
            {
                AccountId = 100000,
                AssistCharacterServerId = _fakeAssistServerId,
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
                WeaponDB = weaponData,
                GearDB = gearData,
            };
            _assistCharData.Add(assistCharacter);
            _fakeAssistServerId++;
            _fakeCharServerId++;
        }
    }

    private async Task AddCharacter(SchaleDataContext context, AccountDBServer account, uint characterId, string option)
    {
        var characterExcel = characterExcels;
        var characterLevelExcel = characterLevelExcels;
        var chData = characterExcel.FirstOrDefault(c => c.Id == characterId);

        if (chData == null)
            return;

        bool useOptions = !string.IsNullOrEmpty(option);
        int starGrade = chData.DefaultStarGrade;
        int favorRank = 1;
        bool breakLimit = false;
        int weaponLevel = 1;
        bool useEquipment = false;
        bool useGear = false;

        switch (option)
        {
            case "max":
                starGrade = 5;
                favorRank = 100;
                breakLimit = true;
                weaponLevel = 60;
                useEquipment = true;
                useGear = true;
                break;
        }

        var existingCharacter = context.Characters.FirstOrDefault(x =>
            x.AccountServerId == account.ServerId && x.UniqueId == characterId);

        if (existingCharacter != null)
            return;

        var characterDB = new CharacterDBServer
        {
            AccountServerId = account.ServerId,
            UniqueId = characterId,
            StarGrade = starGrade,
            Level = characterLevelExcel.Count,
            Exp = 0,
            ExSkillLevel = useOptions ? 5 : 1,
            PublicSkillLevel = useOptions ? 10 : 1,
            PassiveSkillLevel = useOptions ? 10 : 1,
            ExtraPassiveSkillLevel = useOptions ? 10 : 1,
            LeaderSkillLevel = 1,
            FavorRank = useOptions ? favorRank : 1,
            PotentialStats = breakLimit ?
                new Dictionary<int, int> { { 1, 25 }, { 2, 25 }, { 3, 25 } } :
                new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } },
            EquipmentServerIds = new List<long> { 0, 0, 0 }
        };

        context.Characters.Add(characterDB);
        await context.SaveChangesAsync();

        if (useOptions && weaponLevel != 1)
            await AddWeapon(context, account, characterId, weaponLevel);
        if (useOptions && useEquipment)
            await AddEquipment(context, account, characterId);
        if (useOptions && useGear)
            await AddGear(context, account, characterId);
    }

    private async Task AddWeapon(SchaleDataContext context, AccountDBServer account, uint characterId, int level)
    {
        var weaponExcel = weaponExcels;
        var weaponData = weaponExcel.FirstOrDefault(w => w.Id == characterId);

        if (weaponData == null)
            return;

        var character = context.Characters.FirstOrDefault(c =>
            c.AccountServerId == account.ServerId && c.UniqueId == characterId);

        if (character == null)
            return;

        var existingWeapon = context.Weapons.FirstOrDefault(w =>
            w.AccountServerId == account.ServerId && w.BoundCharacterServerId == character.ServerId);

        if (existingWeapon != null)
            return;

        int starGrade = level switch
        {
            >= 60 => 4,
            >= 50 => 3,
            >= 40 => 2,
            >= 30 => 1,
            _ => 0
        };

        var weapon = new WeaponDBServer
        {
            AccountServerId = account.ServerId,
            UniqueId = characterId,
            StarGrade = starGrade,
            Level = level,
            BoundCharacterServerId = character.ServerId
        };

        context.Weapons.Add(weapon);
        await context.SaveChangesAsync();
    }

    private async Task AddEquipment(SchaleDataContext context, AccountDBServer account, uint characterId)
    {
        var equipmentExcel = equipmentExcels;
        var characterExcel = characterExcels;
        var chData = characterExcel.FirstOrDefault(c => c.Id == characterId);

        if (chData == null)
            return;

        var character = context.Characters.FirstOrDefault(c =>
            c.AccountServerId == account.ServerId && c.UniqueId == characterId);

        if (character == null)
            return;

        var equipmentServerIds = new List<long>();

        foreach (var slot in chData.EquipmentSlot)
        {
            var equipmentData = equipmentExcel
                .Where(e => e.EquipmentCategory == slot)
                .OrderByDescending(y => y.MaxLevel)
                .FirstOrDefault();

            if (equipmentData == null)
            {
                equipmentServerIds.Add(0);
                continue;
            }

            var equipment = new EquipmentDBServer
            {
                AccountServerId = account.ServerId,
                UniqueId = equipmentData.Id,
                Level = equipmentData.MaxLevel,
                Tier = (int)equipmentData.TierInit,
                StackCount = 1,
                BoundCharacterServerId = character.ServerId
            };

            context.Equipments.Add(equipment);
            await context.SaveChangesAsync();

            equipmentServerIds.Add(equipment.ServerId);
        }

        character.EquipmentServerIds = equipmentServerIds;
        await context.SaveChangesAsync();
    }

    private async Task AddGear(SchaleDataContext context, AccountDBServer account, uint characterId)
    {
        var uniqueGearExcel = uniqueGearExcels;
        var matchingGear = uniqueGearExcel
            .Where(x => x.CharacterId == characterId && x.Tier == 2)
            .FirstOrDefault();

        if (matchingGear == null)
            return;

        var character = context.Characters.FirstOrDefault(c =>
            c.AccountServerId == account.ServerId && c.UniqueId == characterId);

        if (character == null)
            return;

        var existingGear = context.Gears.FirstOrDefault(g =>
            g.AccountServerId == account.ServerId && g.BoundCharacterServerId == character.ServerId);

        if (existingGear != null)
            return;

        var gear = new GearDBServer
        {
            AccountServerId = account.ServerId,
            UniqueId = matchingGear.Id,
            Level = 1,
            SlotIndex = 4,
            Tier = 2,
            Exp = 0,
            BoundCharacterServerId = character.ServerId
        };

        context.Gears.Add(gear);
        await context.SaveChangesAsync();
    }
}
