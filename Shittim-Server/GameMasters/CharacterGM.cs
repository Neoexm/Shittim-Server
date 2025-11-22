using Schale.FlatData;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Schale.Data;
using Microsoft.EntityFrameworkCore;
using Shittim.Services.Client;
using Shittim.Models.GM;
using Schale.Data.GameModel;
using Schale.Excel;

namespace Shittim.GameMasters
{
    public static class CharacterGM
    {
        public static List<CharacterExcelT>? characterExcel;
        public static List<CharacterLevelExcelT>? characterLevelExcel;
        public static List<CharacterWeaponExcelT>? weaponExcel;
        public static List<EquipmentExcelT>? equipmentExcel;
        public static List<CharacterGearExcelT>? uniqueGearExcel;

        public static async Task<List<FullCharacterData>> GetCharacters(IClientConnection connection)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var characters = context.GetAccountCharacters(connection.AccountServerId);
            if (account == null || characters == null)
                return new List<FullCharacterData>();

            var weapons = context.GetAccountWeapons(connection.AccountServerId)
                .Where(w => w.BoundCharacterServerId != 0)
                .ToLookup(w => w.BoundCharacterServerId);

            var gears = context.GetAccountGears(connection.AccountServerId)
                .Where(g => g.BoundCharacterServerId != 0)
                .ToLookup(g => g.BoundCharacterServerId);

            var equipments = context.
                GetAccountEquipments(connection.AccountServerId)
                .ToDictionary(e => e.ServerId);

            var result = new List<FullCharacterData>();

            foreach (var character in characters)
            {
                WeaponDBServer weapon = null;
                if (weapons != null && weapons[character.ServerId] != null)
                    weapon = weapons[character.ServerId].FirstOrDefault();

                var equipmentList = new List<EquipmentDBServer>();
                if (equipments != null && character.EquipmentServerIds != null)
                {
                    foreach (long equipmentInstanceId in character.EquipmentServerIds)
                    {
                        if (equipments.TryGetValue(equipmentInstanceId, out var equipment))
                            equipmentList.Add(equipment);
                    }
                }

                GearDBServer gear = null;
                if (gears != null && gears[character.ServerId] != null)
                    gear = gears[character.ServerId].FirstOrDefault();

                result.Add(new FullCharacterData
                {
                    Character = character,
                    Weapon = weapon,
                    EquippedEquipment = equipmentList,
                    Gear = gear
                });
            }
            return result;
        }

        public static async Task<FullCharacterData?> GetCharacterCompleteInfo(SchaleDataContext context, long accountId, long characterServerId)
        {
            if (context == null) return null;
            if (characterServerId == 0) return null;

            var character = await context.Characters
                .FirstOrDefaultAsync(c => c.AccountServerId == accountId && c.ServerId == characterServerId);

            if (character == null)
            {
                Log.Warning("Character not found for AccountId: {AccountId}, CharacterServerId: {CharacterServerId}", accountId, characterServerId);
                return null;
            }

            var weapon = await context.Weapons
                .FirstOrDefaultAsync(w => w.AccountServerId == accountId && w.BoundCharacterServerId == character.ServerId);

            var equippedEquipmentList = new List<EquipmentDBServer>();
            if (character.EquipmentServerIds != null && character.EquipmentServerIds.Any())
            {
                foreach (long equipmentServerIdInSlot in character.EquipmentServerIds)
                {
                    if (equipmentServerIdInSlot == 0) continue;

                    var equipmentItem = await context.Equipments
                        .FirstOrDefaultAsync(eq => eq.AccountServerId == accountId && eq.ServerId == equipmentServerIdInSlot);

                    if (equipmentItem != null)
                        equippedEquipmentList.Add(equipmentItem);
                    else
                        Log.Warning("Equipment item with ServerId {EquipmentServerId} for AccountId {AccountId} not found, though listed in character's EquipmentServerIds.", equipmentServerIdInSlot, accountId);
                }
            }

            var gear = await context.Gears
                .FirstOrDefaultAsync(g => g.AccountServerId == accountId && g.BoundCharacterServerId == character.ServerId);

            return new FullCharacterData
            {
                Character = character,
                Weapon = weapon,
                EquippedEquipment = equippedEquipmentList,
                Gear = gear
            };
        }

        public async static Task AddWeapon(SchaleDataContext context, AccountDBServer account, uint characterId, int weaponLevel)
        {
            var owner = context.Characters.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == characterId);
            var data = weaponExcel.GetCharacterWeaponExcelByCharacterId(owner.UniqueId);
            var unlock = data?.Unlock;

            if (owner != null && unlock != null)
            {
                context.AddWeapons(account.ServerId, [
                    new WeaponDBServer
                    {
                        UniqueId = owner.UniqueId,
                        BoundCharacterServerId = owner.ServerId,
                        StarGrade = unlock.TakeWhile(x => x).Count(),
                        Level = weaponLevel
                    }
                ]);
                await context.SaveChangesAsync();
            }
        }

        public async static Task AddEquipment(SchaleDataContext context, AccountDBServer account, uint characterId)
        {
            var characterEquipmentData = characterExcel.GetCharacter(characterId);
            var charEquipmentData = context.Characters.FirstOrDefault(y => y.AccountServerId == account.ServerId && y.UniqueId == characterEquipmentData.Id);
            if (characterEquipmentData != null)
            {
                var characterEquipment = characterEquipmentData.EquipmentSlot.Select(x =>
                {
                    var equipmentData = equipmentExcel
                        .GetEquipmentExcelByCategory(x)
                        .GetCharacterEquipment()
                        .OrderByDescending(y => y.MaxLevel)
                        .FirstOrDefault();
                    return new EquipmentDBServer()
                    {
                        UniqueId = equipmentData.Id,
                        Level = equipmentData.MaxLevel,
                        Tier = (int)equipmentData.TierInit,
                        StackCount = 1,
                        BoundCharacterServerId = charEquipmentData.ServerId
                    };
                }).ToList();
                context.AddEquipment(account.ServerId, [.. characterEquipment]);
                await context.SaveChangesAsync();
                charEquipmentData.EquipmentServerIds.Clear();
                charEquipmentData.EquipmentServerIds.AddRange(characterEquipment.Select(x => x.ServerId));
                await context.SaveChangesAsync();
            }
        }

        public async static Task AddGear(SchaleDataContext context, AccountDBServer account, uint characterId)
        {
            var uniqueGear = uniqueGearExcel
                .GetCharacterGearExcelByCharacterId(characterId)
                .GetCharacterGearExcelByTier(2);
            var characterGear = context.Characters.FirstOrDefault(z => z.AccountServerId == account.ServerId && z.UniqueId == uniqueGear.CharacterId);
            if (uniqueGear.CharacterId != 0)
            {
                var uniqueGearDB = new GearDBServer()
                {
                    UniqueId = uniqueGear.Id,
                    Level = 1,
                    SlotIndex = 4,
                    BoundCharacterServerId = characterGear.ServerId,
                    Tier = (int)uniqueGear.Tier,
                    Exp = 0,
                };
                context.AddGears(account.ServerId, [uniqueGearDB]);
                await context.SaveChangesAsync();
            }
        }

        public async static Task AddCharacter(SchaleDataContext context, AccountDBServer account, uint characterId, string addOption)
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

            var chData = characterExcel.GetCharacter(characterId);

            if (context.Characters.Any(x => x.AccountServerId == account.ServerId && x.UniqueId == characterId))
            {
                var character = context.Characters.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == characterId);
                character.StarGrade = starGrade == 0 ? starGrade : chData.DefaultStarGrade;
                character.Level = starGrade == 0 ? 1 : characterLevelExcel.Count;
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
                await context.SaveChangesAsync();
            }
            else
            {
                var characterDB = new CharacterDBServer()
                {
                    UniqueId = characterId,
                    StarGrade = starGrade == 0 ? starGrade : chData.DefaultStarGrade,
                    Level = starGrade == 0 ? 1 : characterLevelExcel.Count,
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

                context.AddCharacters(account.ServerId, [characterDB]);
                await context.SaveChangesAsync();
            }

            if (useOptions && weaponLevel != 1)
                await AddWeapon(context, account, characterId, weaponLevel);
            if (useOptions && useEquipment)
                await AddEquipment(context, account, characterId);
            if (useOptions && useGear && uniqueGearExcel.Any(x => x.CharacterId == characterId))
                await AddGear(context, account, characterId);
        }

        public async static Task RemoveCharacter(IClientConnection connection, uint characterId)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var defaultCharacterExcel = connection.ExcelTableService.GetTable<DefaultCharacterExcelT>();

            var character = context.Characters.FirstOrDefault(x =>
                x.AccountServerId == account.ServerId && x.UniqueId == characterId);
            var weapon = context.Weapons.FirstOrDefault(
                x => x.AccountServerId == account.ServerId && x.BoundCharacterServerId == character.ServerId);
            var equipment = context.GetAccountEquipments(connection.AccountServerId).Where(x =>
                x.BoundCharacterServerId == character.ServerId);
            var gear = context.Gears.FirstOrDefault(x =>
                x.AccountServerId == account.ServerId && x.BoundCharacterServerId == character.ServerId);

            if (character == null)
            {
                await connection.SendChatMessage($"{characterId} does not exist!");
                return;
            };

            if (character != null && !defaultCharacterExcel.Select(x => x.CharacterId).ToList().Contains(character.UniqueId))
            {
                context.Characters.Remove(character);
                await connection.SendChatMessage($"Character {characterId} successfully removed!");
            }
            else
            {
                var defaultChar = context.Characters.FirstOrDefault(x => x.UniqueId == characterId);
                defaultChar.StarGrade = characterExcel.GetCharacter(character.UniqueId).DefaultStarGrade;
                defaultChar.PublicSkillLevel = 1;
                defaultChar.ExSkillLevel = 1;
                defaultChar.PassiveSkillLevel = 1;
                defaultChar.ExtraPassiveSkillLevel = 1;
                defaultChar.Level = 1;
                defaultChar.Exp = 0;
                defaultChar.FavorRank = 1;
                defaultChar.PotentialStats = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } };
                defaultChar.EquipmentServerIds = [0, 0, 0];
                context.Characters.Update(defaultChar);
                await connection.SendChatMessage($"Default character cannot be removed!");
            }

            if (weapon != null) context.Weapons.Remove(weapon);
            if (equipment != null) context.Equipments.RemoveRange(equipment);
            if (gear != null) context.Gears.Remove(gear);
            await context.SaveChangesAsync();
        }

        public async static Task ModifyEquipment(SchaleDataContext context, CharacterDBServer character, ModifyCharacterRequest characterReq)
        {
            var characterTemplate = characterExcel.GetCharacter(character.UniqueId);
            var equipmentSlots = characterTemplate.EquipmentSlot;

            int slotIndex = 0;
            foreach (var equipInfo in characterReq.EquippedEquipment)
            {
                var existingEquipment = context.Equipments.FirstOrDefault(e => e.ServerId == equipInfo.ServerId && e.BoundCharacterServerId == character.ServerId);
                if (existingEquipment != null)
                {
                    var category = equipmentSlots[slotIndex];
                    var targetEqTemplate = equipmentExcel
                        .GetEquipmentExcelByCategory(category)
                        .GetEquipmentExcelByTier(equipInfo.Tier);

                    if (targetEqTemplate != null)
                    {
                        Log.Information($"Updating equipment {existingEquipment.ServerId} for character {character.UniqueId}.");
                        existingEquipment.Level = targetEqTemplate.MaxLevel;
                        existingEquipment.Tier = (int)targetEqTemplate.TierInit;
                        existingEquipment.UniqueId = targetEqTemplate.Id;
                    }
                    else
                        Log.Warning($"No equipment found for category {category} and tier {equipInfo.Tier}. Equipment {existingEquipment.ServerId} not updated.");
                }
                else
                    Log.Warning($"Equipment with ServerId {equipInfo.ServerId} not found for character {character.UniqueId}.");
                slotIndex++;
            }
        }

        private static void UpdateWeaponStats(WeaponDBServer weapon, int starGrade, int level)
        {
            weapon.StarGrade = starGrade;

            int maxLevel = starGrade switch
            {
                1 => 30,
                2 => 40,
                3 => 50,
                4 => 60,
                _ => 1
            };

            weapon.Level = Math.Clamp(level, 1, maxLevel);
        }

        public async static Task ModifyWeapon(SchaleDataContext context, AccountDBServer account, CharacterDBServer character, ModifyCharacterRequest characterReq)
        {
            var existingWeapon = context.Weapons.FirstOrDefault(w => w.BoundCharacterServerId == character.ServerId);
            bool isRemovalRequest = characterReq.Weapon.Level is null || characterReq.Weapon.Level == 0 || characterReq.Weapon.StarGrade is null || characterReq.Weapon.StarGrade == 0;

            if (isRemovalRequest)
            {
                if (existingWeapon != null)
                {
                    context.Weapons.Remove(existingWeapon);
                    Log.Information("Removed weapon from character {CharacterUniqueId} due to invalid Level or StarGrade (is null or 0).", character.UniqueId);
                }
            }
            else
            {
                if (existingWeapon != null)
                {
                    Log.Information("Updating weapon for character {CharacterUniqueId} (Weapon ServerId: {WeaponServerId}, UniqueId: {WeaponUniqueId})",
                        character.UniqueId, existingWeapon.ServerId, existingWeapon.UniqueId);
                    existingWeapon.UniqueId = characterReq.Character.UniqueId;
                    UpdateWeaponStats(existingWeapon, characterReq.Weapon.StarGrade.Value, characterReq.Weapon.Level.Value);
                }
                else
                {
                    Log.Information("No weapon found. Creating new weapon for character {CharacterUniqueId}.", character.UniqueId);

                    await AddWeapon(context, account, (uint)character.UniqueId, characterReq.Weapon.Level.Value);

                    var newWeapon = context.Weapons.FirstOrDefault(w => w.BoundCharacterServerId == character.ServerId);
                    if (newWeapon != null)
                        UpdateWeaponStats(newWeapon, characterReq.Weapon.StarGrade.Value, characterReq.Weapon.Level.Value);
                }
            }
        }
        public async static Task ModifyGear(SchaleDataContext context, AccountDBServer account, CharacterDBServer character, ModifyCharacterRequest characterReq)
        {
            var existingGear = context.Gears.FirstOrDefault(g => g.BoundCharacterServerId == character.ServerId);
            bool isRemovalRequest = characterReq.Gear.Tier == 0;

            if (isRemovalRequest)
            {
                if (existingGear != null)
                {
                    context.Gears.Remove(existingGear);
                    Log.Information("Removed gear from character {CharacterUniqueId} due to Tier being 0.", character.UniqueId);
                }
                return;
            }

            int requestedTier = Math.Clamp(characterReq.Gear.Tier, 0, 2);
            var targetGearTemplate = uniqueGearExcel
                .GetCharacterGearExcelByCharacterId(character.UniqueId);
                
            if (targetGearTemplate.Count == 0)
            {
                if (existingGear != null)
                {
                    context.Gears.Remove(existingGear);
                    Log.Information("Character {CharacterUniqueId} does not have a gear, removing existing gear.", character.UniqueId);
                }
                return;
            }

            var targetGearTier = targetGearTemplate.GetCharacterGearExcelByTier(requestedTier);
            if (existingGear != null)
            {
                Log.Information("Updating gear for character {CharacterUniqueId}", character.UniqueId);
                existingGear.UniqueId = targetGearTier.Id;
                existingGear.Level = 1;
                existingGear.Tier = requestedTier;
                existingGear.Exp = 0;
                existingGear.SlotIndex = 4;
            }
            else
            {
                Log.Information("No gear found. Creating new gear for character {CharacterUniqueId}.", character.UniqueId);
                var newGear = new GearDBServer()
                {
                    UniqueId = targetGearTier.Id,
                    Level = 1,
                    SlotIndex = 4,
                    BoundCharacterServerId = character.ServerId,
                    Tier = requestedTier,
                    Exp = 0,
                };
                context.AddGears(account.ServerId, [newGear]);
            }
        }

        public async static Task ModifyCharacter(IClientConnection connection, ModifyCharacterRequest characterReq)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            Log.Information($"Attempting to modify character UniqueId: {characterReq.Character.UniqueId} for account ServerId: {account.ServerId}");

            var character = context.Characters.FirstOrDefault(x => x.ServerId == characterReq.Character.ServerId);

            if (character == null)
                throw new Exception($"Character with ServerId {characterReq.Character.ServerId} not found!");

            character.Level = characterReq.Character.Level;
            character.UniqueId = characterReq.Character.UniqueId;
            character.StarGrade = characterReq.Character.StarGrade;
            character.FavorRank = characterReq.Character.FavorRank;
            character.PublicSkillLevel = characterReq.Character.PublicSkillLevel;
            character.ExSkillLevel = characterReq.Character.ExSkillLevel;
            character.PassiveSkillLevel = characterReq.Character.PassiveSkillLevel;
            character.ExtraPassiveSkillLevel = characterReq.Character.ExtraPassiveSkillLevel;

            var newPotentialStats = new Dictionary<int, int>();
            foreach (var kvp in characterReq.Character.PotentialStats)
            {
                if (int.TryParse(kvp.Key, out int keyAsInt))
                    newPotentialStats[keyAsInt] = kvp.Value;
            }
            character.PotentialStats = newPotentialStats;

            if (characterReq.Weapon != null)
                await ModifyWeapon(context, account, character, characterReq);

            if (characterReq.EquippedEquipment != null && characterReq.EquippedEquipment.Any())
                await ModifyEquipment(context, character, characterReq);

            if (characterReq.Gear != null)
                await ModifyGear(context, account, character, characterReq);

            try
            {
                await context.SaveChangesAsync();
                Log.Information($"Character {characterReq.Character.UniqueId} data updated successfully!");
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to update character {characterReq.Character.UniqueId}. Error: {ex.Message}");
            }
        }

        public async static Task ModifyCharacter(IClientConnection connection, uint characterId, string option, string parameters)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var character = context.Characters.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == characterId);
            if (character == null)
            {
                await connection.SendChatMessage($"Character {characterId} not found!");
                return;
            }

            switch (option.ToLower())
            {
                case "level":
                    if (int.TryParse(parameters, out int level))
                    {
                        character.Level = level;
                        await connection.SendChatMessage($"Character {characterId} level set to {level}");
                    }
                    else
                        await connection.SendChatMessage("Invalid level parameter!");
                    break;
                case "star":
                    if (int.TryParse(parameters, out int star))
                    {
                        character.StarGrade = star;
                        await connection.SendChatMessage($"Character {characterId} star grade set to {star}");
                    }
                    else
                        await connection.SendChatMessage("Invalid star parameter!");
                    break;
                case "skill":
                    var skillLevels = parameters.Split(' ');
                    if (skillLevels.Length == 4 && skillLevels.All(x => int.TryParse(x, out _)))
                    {
                        character.ExSkillLevel = int.Parse(skillLevels[0]);
                        character.PublicSkillLevel = int.Parse(skillLevels[1]);
                        character.PassiveSkillLevel = int.Parse(skillLevels[2]);
                        character.ExtraPassiveSkillLevel = int.Parse(skillLevels[3]);
                        await connection.SendChatMessage($"Character {characterId} skill levels updated");
                    }
                    else
                        await connection.SendChatMessage("Invalid skill levels! Format: {skill1 skill2 skill3 skill4}");
                    break;
                case "ps":
                    var potentialStats = parameters.Split(' ');
                    if (potentialStats.Length == 3 && potentialStats.All(x => int.TryParse(x, out _)))
                    {
                        character.PotentialStats = new Dictionary<int, int>
                        {
                            { 1, int.Parse(potentialStats[0]) },
                            { 2, int.Parse(potentialStats[1]) },
                            { 3, int.Parse(potentialStats[2]) }
                        };
                        await connection.SendChatMessage($"Character {characterId} potential stats updated");
                    }
                    else
                        await connection.SendChatMessage("Invalid potential stats! Format: {ps1 ps2 ps3}");
                    break;
                default:
                    await connection.SendChatMessage("Invalid modify option!");
                    return;
            }

            await context.SaveChangesAsync();
        }

        public async static Task ModifyAllCharacters(IClientConnection connection, string option, string parameters)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);
            var allCharacters = context.GetAccountCharacters(connection.AccountServerId).ToList();

            switch (option.ToLower())
            {
                case "level":
                    if (int.TryParse(parameters, out int level))
                    {
                        foreach (var character in allCharacters)
                            character.Level = level;
                    }
                    break;
                case "star":
                    if (int.TryParse(parameters, out int star))
                    {
                        foreach (var character in allCharacters)
                            character.StarGrade = star;
                    }
                    break;
                case "skill":
                    var skillLevels = parameters.Split(' ');
                    if (skillLevels.Length == 4 && skillLevels.All(x => int.TryParse(x, out _)))
                    {
                        foreach (var character in allCharacters)
                        {
                            character.ExSkillLevel = int.Parse(skillLevels[0]);
                            character.PublicSkillLevel = int.Parse(skillLevels[1]);
                            character.PassiveSkillLevel = int.Parse(skillLevels[2]);
                            character.ExtraPassiveSkillLevel = int.Parse(skillLevels[3]);
                        }
                    }
                    break;
                case "ps":
                    var potentialStats = parameters.Split(' ');
                    if (potentialStats.Length == 3 && potentialStats.All(x => int.TryParse(x, out _)))
                    {
                        foreach (var character in allCharacters)
                        {
                            character.PotentialStats = new Dictionary<int, int>
                            {
                                { 1, int.Parse(potentialStats[0]) },
                                { 2, int.Parse(potentialStats[1]) },
                                { 3, int.Parse(potentialStats[2]) }
                            };
                        }
                    }
                    break;
                default:
                    await connection.SendChatMessage("Invalid modify option!");
                    return;
            }

            await context.SaveChangesAsync();
            await connection.SendChatMessage("All characters have been modified!");
        }
    }
}
