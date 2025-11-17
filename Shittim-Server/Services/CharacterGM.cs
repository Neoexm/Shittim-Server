using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.Excel;
using Microsoft.EntityFrameworkCore;
using Serilog;
using BlueArchiveAPI.Services;

namespace Shittim_Server.Services;

public class CharacterGM
{
    private readonly ExcelTableService _excelService;
    private readonly SchaleDataContext _context;

    private readonly List<CharacterExcelT> _characterExcels;
    private readonly List<CharacterLevelExcelT> _characterLevelExcels;
    private readonly List<CharacterWeaponExcelT> _weaponExcels;
    private readonly List<EquipmentExcelT> _equipmentExcels;
    private readonly List<CharacterGearExcelT> _uniqueGearExcels;

    public CharacterGM(ExcelTableService excelService, SchaleDataContext context)
    {
        _excelService = excelService;
        _context = context;

        _characterExcels = _excelService.GetTable<CharacterExcelT>();
        _characterLevelExcels = _excelService.GetTable<CharacterLevelExcelT>();
        _weaponExcels = _excelService.GetTable<CharacterWeaponExcelT>();
        _equipmentExcels = _excelService.GetTable<EquipmentExcelT>();
        _uniqueGearExcels = _excelService.GetTable<CharacterGearExcelT>();
    }

    public async Task AddCharacter(AccountDBServer account, long characterId, string addOption = "")
    {
        bool useOptions = false;
        int starGrade = 3;
        int favorRank = 1;
        bool breakLimit = false;
        int weaponLevel = 1;
        bool useEquipment = false;
        bool useGear = false;

        if (!string.IsNullOrEmpty(addOption)) useOptions = true;

        switch (addOption)
        {
            case "barebone":
                starGrade = 0;
                break;
            case "basic":
                favorRank = 20;
                useEquipment = true;
                useGear = true;
                break;
            case "ue30":
                starGrade = 5;
                favorRank = 20;
                weaponLevel = 30;
                useEquipment = true;
                useGear = true;
                break;
            case "ue50":
                starGrade = 5;
                favorRank = 50;
                weaponLevel = 50;
                useEquipment = true;
                useGear = true;
                break;
            case "max":
                starGrade = 5;
                favorRank = 100;
                breakLimit = true;
                weaponLevel = 60;
                useEquipment = true;
                useGear = true;
                break;
        }

        var chData = _characterExcels.GetCharacter(characterId);

        if (_context.Characters.Any(x => x.AccountServerId == account.ServerId && x.UniqueId == characterId))
        {
            var character = _context.Characters.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == characterId);
            if (character == null) return;

            character.StarGrade = starGrade == 0 ? starGrade : chData.DefaultStarGrade;
            character.Level = starGrade == 0 ? 1 : _characterLevelExcels.Count;
            character.Exp = 0;
            character.ExSkillLevel = useOptions ? 5 : 1;
            character.PublicSkillLevel = useOptions ? 10 : 1;
            character.PassiveSkillLevel = useOptions ? 10 : 1;
            character.ExtraPassiveSkillLevel = useOptions ? 10 : 1;
            character.LeaderSkillLevel = 1;
            character.FavorRank = useOptions ? favorRank : 1;
            character.PotentialStats = breakLimit ?
                new Dictionary<int, int> { { 1, 25 }, { 2, 25 }, { 3, 25 } } :
                new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } };
            await _context.SaveChangesAsync();
        }
        else
        {
            var characterDB = new CharacterDBServer()
            {
                UniqueId = characterId,
                StarGrade = starGrade == 0 ? starGrade : chData.DefaultStarGrade,
                Level = starGrade == 0 ? 1 : _characterLevelExcels.Count,
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
                EquipmentServerIds = [0, 0, 0]
            };

            _context.AddCharacters(account.ServerId, [characterDB]);
            await _context.SaveChangesAsync();
        }

        if (useOptions && weaponLevel != 1)
            await AddWeapon(account, characterId, weaponLevel);
        if (useOptions && useEquipment)
            await AddEquipment(account, characterId);
        if (useOptions && useGear && _uniqueGearExcels.Any(x => x.CharacterId == characterId))
            await AddGear(account, characterId);
    }

    public async Task AddWeapon(AccountDBServer account, long characterId, int weaponLevel)
    {
        var owner = _context.Characters.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == characterId);
        if (owner == null) return;

        var data = _weaponExcels.GetCharacterWeaponExcelByCharacterId(owner.UniqueId);
        var unlock = data?.Unlock;

        if (unlock != null)
        {
            _context.AddWeapons(account.ServerId, [
                new WeaponDBServer
                {
                    UniqueId = owner.UniqueId,
                    BoundCharacterServerId = owner.ServerId,
                    StarGrade = unlock.TakeWhile(x => x).Count(),
                    Level = weaponLevel
                }
            ]);
            await _context.SaveChangesAsync();
        }
    }

    public async Task AddEquipment(AccountDBServer account, long characterId)
    {
        var characterEquipmentData = _characterExcels.GetCharacter(characterId);
        var charEquipmentData = _context.Characters.FirstOrDefault(y => y.AccountServerId == account.ServerId && y.UniqueId == characterEquipmentData.Id);
        if (characterEquipmentData == null || charEquipmentData == null) return;

        var characterEquipment = characterEquipmentData.EquipmentSlot.Select(x =>
        {
            var equipmentData = _equipmentExcels
                .GetEquipmentExcelByCategory(x)
                .Where(y => y.RecipeId != 0)
                .FirstOrDefault();

            return new EquipmentDBServer
            {
                UniqueId = equipmentData.Id,
                Level = 1,
                Exp = 0,
                BoundCharacterServerId = charEquipmentData.ServerId,
                Tier = 1,
                StackCount = 1
            };
        }).ToList();

        foreach (var equipment in characterEquipment)
        {
            _context.Equipments.Add(equipment);
        }

        var equipmentIds = characterEquipment.Select(x => x.ServerId).ToList();
        charEquipmentData.EquipmentServerIds = [.. equipmentIds];

        await _context.SaveChangesAsync();
    }

    public async Task AddGear(AccountDBServer account, long characterId)
    {
        var owner = _context.Characters.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == characterId);
        if (owner == null) return;

        var data = _uniqueGearExcels.FirstOrDefault(x => x.CharacterId == characterId);
        if (data == null) return;

        _context.AddGears(account.ServerId, [
            new GearDBServer
            {
                UniqueId = data.Id,
                Level = 1,
                Exp = 0,
                BoundCharacterServerId = owner.ServerId,
                Tier = 1,
                SlotIndex = 4
            }
        ]);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveCharacter(AccountDBServer account, long characterId)
    {
        var defaultCharacterExcel = _excelService.GetTable<DefaultCharacterExcelT>();

        var character = _context.Characters.FirstOrDefault(x =>
            x.AccountServerId == account.ServerId && x.UniqueId == characterId);

        if (character == null)
        {
            Log.Warning($"Character {characterId} does not exist for account {account.ServerId}");
            return;
        }

        var weapon = _context.Weapons.FirstOrDefault(
            x => x.AccountServerId == account.ServerId && x.BoundCharacterServerId == character.ServerId);
        var equipment = _context.GetAccountEquipments(account.ServerId).Where(x =>
            x.BoundCharacterServerId == character.ServerId);
        var gear = _context.Gears.FirstOrDefault(x =>
            x.AccountServerId == account.ServerId && x.BoundCharacterServerId == character.ServerId);

        if (!defaultCharacterExcel.Select(x => x.CharacterId).ToList().Contains(character.UniqueId))
        {
            _context.Characters.Remove(character);
            Log.Information($"Character {characterId} successfully removed!");
        }
        else
        {
            var defaultChar = _context.Characters.FirstOrDefault(x => x.UniqueId == characterId);
            if (defaultChar != null)
            {
                defaultChar.StarGrade = _characterExcels.GetCharacter(character.UniqueId).DefaultStarGrade;
                defaultChar.PublicSkillLevel = 1;
                defaultChar.ExSkillLevel = 1;
                defaultChar.PassiveSkillLevel = 1;
                defaultChar.ExtraPassiveSkillLevel = 1;
                defaultChar.Level = 1;
                defaultChar.Exp = 0;
                defaultChar.FavorRank = 1;
                defaultChar.PotentialStats = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } };
                defaultChar.EquipmentServerIds = [0, 0, 0];
                _context.Characters.Update(defaultChar);
                Log.Warning($"Default character {characterId} cannot be removed, resetting to defaults instead");
            }
        }

        if (weapon != null) _context.Weapons.Remove(weapon);
        if (equipment != null) _context.Equipments.RemoveRange(equipment);
        if (gear != null) _context.Gears.Remove(gear);
        await _context.SaveChangesAsync();
    }
}
