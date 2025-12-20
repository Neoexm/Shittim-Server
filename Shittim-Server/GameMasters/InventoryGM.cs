using Schale.FlatData;
using System.Data;
using Microsoft.IdentityModel.Tokens;
using Shittim.Services.Client;
using Schale.Data.GameModel;
using Schale.Excel;
using Microsoft.EntityFrameworkCore;

namespace Shittim.GameMasters
{
    public static class InventoryGM
    {
        public static async Task AddAllCharacters(IClientConnection connection, string addOption)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var characterExcel = connection.ExcelTableService.GetTable<CharacterExcelT>();
            var defaultCharacterExcel = connection.ExcelTableService.GetTable<DefaultCharacterExcelT>();
            var characterLevelExcel = connection.ExcelTableService.GetTable<CharacterLevelExcelT>();
            var favorLevelExcel = connection.ExcelTableService.GetTable<FavorLevelExcelT>();

            bool useOptions = !string.IsNullOrEmpty(addOption);
            int starGrade = 3;
            int favorRank = 1;
            bool breakLimit = false;

            var optionMap = new Dictionary<string, (int StarGrade, int FavorRank, bool BreakLimit)>
            {
                { "barebone", (0, 1, false) },
                { "basic", (3, 20, false) },
                { "ue30", (5, 20, false) },
                { "ue50", (5, 50, false) },
                { "max", (5, 100, true) }
            };

            if (useOptions && optionMap.TryGetValue(addOption, out var values))
            {
                starGrade = values.StarGrade;
                favorRank = values.FavorRank;
                breakLimit = values.BreakLimit;
            }

            var allCharacters = characterExcel
            .GetReleaseCharacters()
            .Where(x => !context.Characters.Any(y => y.AccountServerId == account.ServerId && y.UniqueId == x.Id))
            .Select(x => {
                return new CharacterDBServer()
                {
                    UniqueId = x.Id,
                    StarGrade = starGrade == 0 ? x.DefaultStarGrade : starGrade,
                    Level = starGrade == 0 ? 1 : characterLevelExcel.Count,
                    Exp = 0,
                    ExSkillLevel = useOptions ? (starGrade == 0 ? 1 : 5) : 1,
                    PublicSkillLevel = useOptions ? (starGrade == 0 ? 1 : 10) : 1,
                    PassiveSkillLevel = useOptions ? (starGrade == 0 ? 1 : 10) : 1,
                    ExtraPassiveSkillLevel = useOptions ? (starGrade == 0 ? 1 : 10) : 1,
                    LeaderSkillLevel = 1,
                    FavorRank = useOptions ? favorRank : 1,
                    PotentialStats = breakLimit ? 
                        new Dictionary<int, int> { { 1, 25 }, { 2, 25 }, { 3, 25 } } :
                        new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } },
                    EquipmentServerIds = [0, 0, 0]
                };
            }).ToList();

            var existingCharacters = await context.GetAccountCharacters(connection.AccountServerId).ToListAsync();
            foreach (var character in existingCharacters.Where(x => characterExcel.Any(y => y.Id == x.UniqueId)))
            {
                var chData = characterExcel.FirstOrDefault(y => y.Id == character.UniqueId);
                var updateCharacter = character;
                updateCharacter.StarGrade = starGrade == 0 ? chData.DefaultStarGrade : starGrade;
                updateCharacter.Level = starGrade == 0 ? 1 : characterLevelExcel.Count;
                updateCharacter.Exp = 0;
                updateCharacter.ExSkillLevel = useOptions ? (starGrade == 0 ? 1 : 5) : 1;
                updateCharacter.PublicSkillLevel = useOptions ? (starGrade == 0 ? 1 : 10) : 1;
                updateCharacter.PassiveSkillLevel = useOptions ? (starGrade == 0 ? 1 : 10) : 1;
                updateCharacter.ExtraPassiveSkillLevel = useOptions ? (starGrade == 0 ? 1 : 10) : 1;
                updateCharacter.LeaderSkillLevel = 1;
                updateCharacter.FavorRank = useOptions ? favorRank : 1;
                updateCharacter.PotentialStats = breakLimit ?
                    new Dictionary<int, int> { { 1, 25 }, { 2, 25 }, { 3, 25 } } :
                    new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } };
                context.Characters.Update(updateCharacter);
            }

            context.AddCharacters(account.ServerId, [.. allCharacters]);
            await context.SaveChangesAsync();

            await connection.SendChatMessage("Added all Characters!");
        }

        public static async Task AddAllEquipment(IClientConnection connection, string addOption)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            bool useEquipment = false;
            switch (addOption)
            {
                case "basic":
                case "ue30":
                case "ue50":
                case "max":
                    useEquipment = true;
                    break;
            }

            if(!useEquipment)
            {
                context.Equipments.RemoveRange(context.Equipments.Where(x => x.AccountServerId == connection.AccountServerId && x.BoundCharacterServerId != 0));
                foreach (var character in context.GetAccountCharacters(connection.AccountServerId))
                {
                    for (int i = 0; i < 3 && i < character.EquipmentServerIds.Count; i++)
                    {
                        character.EquipmentServerIds[i] = 0;
                    }
                    context.Characters.Update(character);
                }
                await context.SaveChangesAsync();
                return;
            }

            var equipmentExcel = connection.ExcelTableService.GetTable<EquipmentExcelT>();
            var characterExcel = connection.ExcelTableService.GetTable<CharacterExcelT>();

            var allCharacterEquipment = characterExcel.FindAll(x => context.Characters.Any(y => y.AccountServerId == account.ServerId && y.UniqueId == x.Id)).ToList();
            foreach (var characterEquipmentData in allCharacterEquipment)
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
                        BoundCharacterServerId = context.Characters.FirstOrDefault(y => y.AccountServerId == account.ServerId && y.UniqueId == characterEquipmentData.Id).ServerId
                    };
                }).ToList();
                context.AddEquipment(account.ServerId, [.. characterEquipment]);
                await context.SaveChangesAsync();    
                context.Characters.FirstOrDefault(x =>
                    x.AccountServerId == connection.AccountServerId && x.UniqueId == characterEquipmentData.Id).EquipmentServerIds.Clear();
                context.Characters.FirstOrDefault(x =>
                    x.AccountServerId == connection.AccountServerId && x.UniqueId == characterEquipmentData.Id).EquipmentServerIds.AddRange(characterEquipment.Select(x => x.ServerId));
            }
            await context.SaveChangesAsync();

            await connection.SendChatMessage("Added all Equipment!");
        }

        public static async Task AddAllWeapons(IClientConnection connection, string addOption)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            int weaponStar = 0;
            int weaponLevel = 0;
            switch (addOption)
            {
                case "ue30":
                    weaponStar = 1;
                    weaponLevel = 30;
                    break;
                case "ue50":
                    weaponStar = 3;
                    weaponLevel = 50;
                    break;
                case "max":
                    weaponStar = 4;
                    weaponLevel = 60;
                    break;
            }

            if(weaponLevel == 0)
            {
                context.Weapons.RemoveRange(context.Weapons.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();
                return;
            }

            var weaponExcel = connection.ExcelTableService.GetTable<CharacterWeaponExcelT>();
            var allWeapons = context.GetAccountCharacters(connection.AccountServerId).ToList().Select(x =>
            {
                return new WeaponDBServer()
                {
                    UniqueId = x.UniqueId,
                    BoundCharacterServerId = x.ServerId,
                    StarGrade = weaponStar,
                    Level = weaponLevel
                };
            });

            context.AddWeapons(account.ServerId, [.. allWeapons]);
            await context.SaveChangesAsync();
            
            await connection.SendChatMessage("Added all Weapons!");
        }

        public static async Task AddAllGears(IClientConnection connection, string addOption)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            bool useGear = false;
            switch (addOption)
            {
                case "ue30":
                case "ue50":
                case "max":
                    useGear = true;
                    break;
            }

            if(!useGear)
            {
                var characterToUpdate = context.GetAccountCharacters(connection.AccountServerId);
                foreach (var character in characterToUpdate)
                {
                    if (character.EquipmentServerIds != null && character.EquipmentServerIds.Count > 3)
                        character.EquipmentServerIds[3] = 0;
                    context.Characters.Update(character);
                }
                var existingGears = context.Gears.Where(g => g.AccountServerId == connection.AccountServerId);
                context.Gears.RemoveRange(existingGears);

                await context.SaveChangesAsync();
                return;
            }

            var uniqueGearExcel = connection.ExcelTableService.GetTable<CharacterGearExcelT>();

            var uniqueGear = uniqueGearExcel.GetGearExcelByTier(useGear ? 2 : 1)
                .Where(x => context.Characters.Any(y => y.AccountServerId == account.ServerId && y.UniqueId == x.CharacterId))
                .Select(x => 
                new GearDBServer()
                {
                    UniqueId = x.Id,
                    Level = 1,
                    SlotIndex = 4,
                    BoundCharacterServerId = context.Characters.FirstOrDefault(z => z.AccountServerId == account.ServerId && z.UniqueId == x.CharacterId).ServerId,
                    Tier = (int)x.Tier,
                    Exp = 0
                }
            );

            context.AddGears(account.ServerId, [.. uniqueGear]);
            await context.SaveChangesAsync();

            await connection.SendChatMessage("Added all Gears!");
        }

        public static async Task AddAllItems(IClientConnection connection)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var itemExcel = connection.ExcelTableService.GetTable<ItemExcelT>();
            var equipmentExcel = connection.ExcelTableService.GetTable<EquipmentExcelT>();

            var allItems = itemExcel
            .Where(x => !context.Items.Any(y => y != null && y.AccountServerId == connection.AccountServerId && y.UniqueId == x.Id))
            .Select(x =>
            {
                return new ItemDBServer()
                {
                    AccountServerId = connection.AccountServerId,
                    UniqueId = x.Id,
                    StackCount = x.StackableMax - 100 <= 0 ? 1 : (long)Math.Floor((double)x.StackableMax / 2)
                };
            }).ToList();

            var allEquipment = equipmentExcel
            .GetItemEquipment()
            .Where(x => !context.Equipments.Any(y => y != null && y.AccountServerId == connection.AccountServerId && y.UniqueId == x.Id && y.BoundCharacterServerId == 0))
            .Select(x =>
            {
                return new EquipmentDBServer()
                {
                    AccountServerId = connection.AccountServerId,
                    UniqueId = x.Id,
                    StackCount = x.StackableMax - 100 <= 0 ? 1 : (long)Math.Floor((double)x.StackableMax / 2),
                    BoundCharacterServerId = 0
                };
            }).ToList();
            await context.Equipments.AddRangeAsync(allEquipment);
            await context.Items.AddRangeAsync(allItems);
            await context.SaveChangesAsync();

            var itemDBs = context.GetAccountItems(connection.AccountServerId).ToList();
            foreach (var item in itemDBs.Where(x => itemExcel.Any(y => y.Id == x.UniqueId)))
            {
                var excelItem = itemExcel.FirstOrDefault(y => y.Id == item.UniqueId);
                if (excelItem != null)
                {
                    long targetStackCount = excelItem.StackableMax - 100 <= 0 ? 1 : (long)Math.Floor((double)excelItem.StackableMax / 2);
                    if (item.StackCount != targetStackCount)
                    {
                        item.StackCount = targetStackCount;
                        context.Items.Update(item);
                    }
                }
            }

            var equipmentDBs = context.GetAccountEquipments(connection.AccountServerId).ToList();
            foreach (var item in equipmentDBs.Where(x => equipmentExcel.Any(y => y.Id == x.UniqueId) && x.BoundCharacterServerId == 0))
            {
                var excelEquipment = equipmentExcel.FirstOrDefault(y => y.Id == item.UniqueId && item.BoundCharacterServerId == 0);
                long targetStackCount = excelEquipment.StackableMax - 100 <= 0 ? 1 : (long)Math.Floor((double)excelEquipment.StackableMax / 2);
                if (item.StackCount != targetStackCount)
                {
                    item.StackCount = targetStackCount;
                    context.Equipments.Update(item);
                }
            }
            await context.SaveChangesAsync();

            await connection.SendChatMessage("Added all Items!");
        }

        public static async Task AddAllMemoryLobbies(IClientConnection connection)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var memoryLobbyExcel = connection.ExcelTableService.GetTable<MemoryLobbyExcelT>();
            var allMemoryLobbies = memoryLobbyExcel
            .Where(x => !context.MemoryLobbies.Any(y =>
                y != null && y.MemoryLobbyUniqueId == x.Id &&
                y.AccountServerId == connection.AccountServerId))
            .Select(x =>
            {
                return new MemoryLobbyDBServer()
                {
                    MemoryLobbyUniqueId = x.Id,
                };
            }).ToList();
            context.AddMemoryLobbies(account.ServerId, [.. allMemoryLobbies]);
            await context.SaveChangesAsync();

            await connection.SendChatMessage("Added all Memory Lobbies!");
        }

        public static async Task AddAllScenarios(IClientConnection connection)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var scenarioModeExcel = connection.ExcelTableService.GetTable<ScenarioModeExcelT>();
            var normalScenario = scenarioModeExcel
            .Where(x => !context.ScenarioHistories.Any(y =>
                y != null && y.ScenarioUniqueId == x.ModeId &&
                y.AccountServerId == connection.AccountServerId))
            .Select(x =>
            {
                return new ScenarioHistoryDBServer()
                {
                    ScenarioUniqueId = x.ModeId,
                    ClearDateTime = account.GameSettings.CurrentDateTime.Date
                };
            }).ToList();
            
            context.AddScenarios(account.ServerId, [.. normalScenario]);
            await context.SaveChangesAsync();

            await connection.SendChatMessage("Added all Scenarios!");
        }

        public static async Task AddAllFurnitures(IClientConnection connection)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var furnitureExcel = connection.ExcelTableService.GetTable<FurnitureExcelT>();
            var defaultFurnitureExcel = connection.ExcelTableService.GetTable<DefaultFurnitureExcelT>();
            
            var allFurnitures = furnitureExcel.Where(x => !context.Furnitures.Any(y =>
                y != null && y.AccountServerId == connection.AccountServerId &&
                y.UniqueId == x.Id && y.Location == FurnitureLocation.Inventory))
            .Select(x =>
            {
                return new FurnitureDBServer()
                {
                    Location = FurnitureLocation.Inventory,
                    UniqueId = x.Id,
                    StackCount = x.StackableMax
                };
            }).ToList();

            context.AddFurnitures(account.ServerId, [.. allFurnitures]);
            await context.SaveChangesAsync();

            foreach (var furniture in context.Furnitures.Where(f => f.AccountServerId == connection.AccountServerId && f.Location == FurnitureLocation.Inventory))
            {
                var updateFurniture = furniture;
                updateFurniture.StackCount = furnitureExcel.FirstOrDefault(y => y.Id == furniture.UniqueId).StackableMax;
                context.Furnitures.Update(updateFurniture);
            }
            await context.SaveChangesAsync();

            await connection.SendChatMessage("Added all Furnitures!");
        }

        public static async Task AddAllEmblems(IClientConnection connection)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var emblemExcel = connection.ExcelTableService.GetTable<EmblemExcelT>();

            var allEmblems = emblemExcel.Where(x => 
                !context.Emblems.Any(y => y != null && y.UniqueId == x.Id && y.AccountServerId == account.ServerId))
            .Select(x =>
            {
                return new EmblemDBServer()
                {
                    UniqueId = x.Id,
                    ReceiveDate = account.GameSettings.CurrentDateTime.Date
                };
            }).ToList();

            context.AddEmblems(account.ServerId, [.. allEmblems]);
            await context.SaveChangesAsync();

            await connection.SendChatMessage("Added all Emblems!");
        }

        public static async Task RemoveAllCharacters(IClientConnection connection)
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var characterExcel = connection.ExcelTableService.GetTable<CharacterExcelT>();
            var defaultCharacterExcel = connection.ExcelTableService.GetTable<DefaultCharacterExcelT>();
            var defaultCharacterIds = defaultCharacterExcel.Select(y => y.CharacterId).ToList();

            var removed = context.GetAccountCharacters(connection.AccountServerId).Where(x => !defaultCharacterIds.Contains(x.UniqueId));
            context.Characters.RemoveRange(removed);

            foreach (var character in context.GetAccountCharacters(connection.AccountServerId).Where(x => defaultCharacterIds.Contains(x.UniqueId)))
            {
                var defaultChar = character;
                defaultChar.StarGrade = characterExcel.FirstOrDefault(x => x.Id == character.UniqueId).DefaultStarGrade;
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
            }

            await context.SaveChangesAsync();
        }
    }
}
