// File: Shittim-Server/Handlers/Equipment.cs
// Handlers: List, Equip, LevelUp, TierUp, BatchGrowth

using BlueArchiveAPI.NetworkModels;
using BlueArchiveAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlueArchiveAPI.Handlers
{
    public static class Equipment
    {
        /// <summary>
        /// Handler for Equipment_List protocol.
        /// Returns all equipment owned by the account.
        /// </summary>
        public class List : BaseHandler<EquipmentItemListRequest, EquipmentItemListResponse>
        {
            private readonly BAContext _dbContext;

            public List(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<EquipmentItemListResponse> Handle(EquipmentItemListRequest request)
            {
                var user = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                var equipments = await _dbContext.Equipments
                    .AsNoTracking()
                    .Where(e => e.AccountServerId == user.Id)
                    .ToListAsync();

                return new EquipmentItemListResponse
                {
                    EquipmentDBs = equipments.Select(e => new EquipmentDB
                    {
                        ServerId = e.ServerId,
                        UniqueId = e.UniqueId,
                        Level = e.Level,
                        Exp = e.Exp,
                        Tier = e.Tier,
                        StackCount = e.StackCount,
                        IsNew = e.IsNew,
                        IsLocked = e.IsLocked,
                        BoundCharacterServerId = e.BoundCharacterServerId
                    }).ToList()
                };
            }
        }

        /// <summary>
        /// Handler for Equipment_Equip protocol.
        /// Equips an equipment piece to a character in a specific slot.
        /// </summary>
        public class Equip : BaseHandler<EquipmentItemEquipRequest, EquipmentItemEquipResponse>
        {
            private readonly BAContext _dbContext;

            public Equip(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<EquipmentItemEquipResponse> Handle(EquipmentItemEquipRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                // Find the equipment stack
                var originalStack = await _dbContext.Equipments
                    .FirstOrDefaultAsync(e => e.ServerId == request.EquipmentServerId);

                if (originalStack == null)
                    throw new Exception("Equipment not found");

                // Decrease stack count
                originalStack.StackCount--;
                if (originalStack.StackCount <= 0)
                {
                    _dbContext.Equipments.Remove(originalStack);
                }
                else
                {
                    _dbContext.Equipments.Update(originalStack);
                }

                // Create new equipped instance bound to character
                var newEquipment = new Models.Equipment
                {
                    AccountServerId = user.Id,
                    UniqueId = originalStack.UniqueId,
                    Level = originalStack.Level,
                    Exp = originalStack.Exp,
                    Tier = originalStack.Tier,
                    StackCount = 1,
                    BoundCharacterServerId = request.CharacterServerId,
                    IsNew = false,
                    IsLocked = false
                };
                _dbContext.Equipments.Add(newEquipment);
                await _dbContext.SaveChangesAsync();

                // Update character's equipment slots
                var equippedCharacter = await _dbContext.Characters
                    .FirstOrDefaultAsync(c => c.ServerId == request.CharacterServerId);

                if (equippedCharacter != null)
                {
                    // Deserialize equipment server IDs
                    var equipmentServerIds = string.IsNullOrEmpty(equippedCharacter.EquipmentServerIds)
                        ? new List<long>()
                        : JsonSerializer.Deserialize<List<long>>(equippedCharacter.EquipmentServerIds) ?? new List<long>();

                    // Ensure we have 3 slots
                    while (equipmentServerIds.Count < 3)
                        equipmentServerIds.Add(0);

                    // Set the equipment in the specified slot
                    equipmentServerIds[request.SlotIndex] = newEquipment.ServerId;

                    // Serialize back
                    equippedCharacter.EquipmentServerIds = JsonSerializer.Serialize(equipmentServerIds);
                    _dbContext.Characters.Update(equippedCharacter);
                    await _dbContext.SaveChangesAsync();
                }

                return new EquipmentItemEquipResponse
                {
                    CharacterDB = equippedCharacter != null ? new CharacterDB
                    {
                        ServerId = equippedCharacter.ServerId,
                        UniqueId = equippedCharacter.UniqueId,
                        StarGrade = equippedCharacter.StarGrade,
                        Level = equippedCharacter.Level,
                        Exp = equippedCharacter.Exp,
                        FavorRank = equippedCharacter.FavorRank,
                        FavorExp = equippedCharacter.FavorExp,
                        PublicSkillLevel = equippedCharacter.PublicSkillLevel,
                        ExSkillLevel = equippedCharacter.ExSkillLevel,
                        PassiveSkillLevel = equippedCharacter.PassiveSkillLevel,
                        ExtraPassiveSkillLevel = equippedCharacter.ExtraPassiveSkillLevel,
                        LeaderSkillLevel = equippedCharacter.LeaderSkillLevel,
                        IsNew = equippedCharacter.IsNew,
                        IsLocked = equippedCharacter.IsLocked,
                        // Deserialize and map equipment/potential data
                        EquipmentServerIds = string.IsNullOrEmpty(equippedCharacter.EquipmentServerIds)
                            ? new List<long>()
                            : JsonSerializer.Deserialize<List<long>>(equippedCharacter.EquipmentServerIds) ?? new List<long>()
                    } : new CharacterDB(),
                    EquipmentDBs = new List<EquipmentDB>
                    {
                        new EquipmentDB
                        {
                            ServerId = newEquipment.ServerId,
                            UniqueId = newEquipment.UniqueId,
                            Level = newEquipment.Level,
                            Exp = newEquipment.Exp,
                            Tier = newEquipment.Tier,
                            StackCount = newEquipment.StackCount,
                            BoundCharacterServerId = newEquipment.BoundCharacterServerId,
                            IsNew = newEquipment.IsNew,
                            IsLocked = newEquipment.IsLocked
                        },
                        new EquipmentDB
                        {
                            ServerId = originalStack.ServerId,
                            UniqueId = originalStack.UniqueId,
                            Level = originalStack.Level,
                            Exp = originalStack.Exp,
                            Tier = originalStack.Tier,
                            StackCount = originalStack.StackCount,
                            BoundCharacterServerId = originalStack.BoundCharacterServerId,
                            IsNew = originalStack.IsNew,
                            IsLocked = originalStack.IsLocked
                        }
                    }
                };
            }
        }

        /// <summary>
        /// Handler for Equipment_LevelUp protocol.
        /// Levels up equipment using consumed materials.
        /// </summary>
        public class LevelUp : BaseHandler<EquipmentItemLevelUpRequest, EquipmentItemLevelUpResponse>
        {
            private readonly BAContext _dbContext;

            public LevelUp(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<EquipmentItemLevelUpResponse> Handle(EquipmentItemLevelUpRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                // TODO: Implement ConsumeHandler logic for consuming materials
                // For now, stub the consume result

                var targetEquipment = await _dbContext.Equipments
                    .FirstOrDefaultAsync(e => e.ServerId == request.TargetServerId);

                if (targetEquipment == null)
                    throw new Exception("Equipment not found");

                // TODO: Calculate exp gain from consumed items using EquipmentLevelExcel
                // TODO: Use MathService.CalculateLevelExpWithoutReset and EquipmentService.CalculateEquipmentExpLevel
                // For now, add stub exp
                long expGain = 1000; // Placeholder

                targetEquipment.Exp += expGain;

                // TODO: Level up logic based on Excel data
                // Placeholder: Simple level up every 1000 exp
                int expPerLevel = 1000;
                while (targetEquipment.Exp >= expPerLevel)
                {
                    targetEquipment.Level++;
                    targetEquipment.Exp -= expPerLevel;
                }

                targetEquipment.StackCount = 1; // Equipment being leveled becomes individual
                _dbContext.Equipments.Update(targetEquipment);
                await _dbContext.SaveChangesAsync();

                return new EquipmentItemLevelUpResponse
                {
                    EquipmentDB = new EquipmentDB
                    {
                        ServerId = targetEquipment.ServerId,
                        UniqueId = targetEquipment.UniqueId,
                        Level = targetEquipment.Level,
                        Exp = targetEquipment.Exp,
                        Tier = targetEquipment.Tier,
                        StackCount = targetEquipment.StackCount,
                        BoundCharacterServerId = targetEquipment.BoundCharacterServerId,
                        IsNew = targetEquipment.IsNew,
                        IsLocked = targetEquipment.IsLocked
                    },
                    ConsumeResultDB = new ConsumeResultDB
                    {
                        // TODO: Populate with consumed items/currencies
                    }
                };
            }
        }

        /// <summary>
        /// Handler for Equipment_TierUp protocol.
        /// Upgrades equipment to the next tier using recipes.
        /// </summary>
        public class TierUp : BaseHandler<EquipmentItemTierUpRequest, EquipmentItemTierUpResponse>
        {
            private readonly BAContext _dbContext;

            public TierUp(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<EquipmentItemTierUpResponse> Handle(EquipmentItemTierUpRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                var targetEquipment = await _dbContext.Equipments
                    .FirstOrDefaultAsync(e => e.ServerId == request.TargetEquipmentServerId);

                if (targetEquipment == null)
                    throw new Exception("Equipment not found");

                // TODO: Load EquipmentExcel and RecipeIngredientExcel
                // TODO: Get current equipment data and find next tier equipment
                // TODO: Consume recipe ingredients using ParcelHandler
                
                // Placeholder: Simple tier up
                long nextTierEquipmentId = targetEquipment.UniqueId + 1000; // Placeholder logic
                int nextTier = targetEquipment.Tier + 1;

                targetEquipment.UniqueId = nextTierEquipmentId;
                targetEquipment.Tier = nextTier;
                
                _dbContext.Equipments.Update(targetEquipment);
                await _dbContext.SaveChangesAsync();

                return new EquipmentItemTierUpResponse
                {
                    EquipmentDB = new EquipmentDB
                    {
                        ServerId = targetEquipment.ServerId,
                        UniqueId = targetEquipment.UniqueId,
                        Level = targetEquipment.Level,
                        Exp = targetEquipment.Exp,
                        Tier = targetEquipment.Tier,
                        StackCount = targetEquipment.StackCount,
                        BoundCharacterServerId = targetEquipment.BoundCharacterServerId,
                        IsNew = targetEquipment.IsNew,
                        IsLocked = targetEquipment.IsLocked
                    },
                    ParcelResultDB = new ParcelResultDB
                    {
                        // TODO: Populate with consumed recipe items
                    }
                };
            }
        }

        /// <summary>
        /// Handler for Equipment_BatchGrowth protocol.
        /// Performs batch tier-up and level-up for multiple equipment pieces and gear.
        /// </summary>
        public class BatchGrowth : BaseHandler<EquipmentBatchGrowthRequest, EquipmentBatchGrowthResponse>
        {
            private readonly BAContext _dbContext;

            public BatchGrowth(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<EquipmentBatchGrowthResponse> Handle(EquipmentBatchGrowthRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                List<EquipmentDB> equipmentDBs = new();
                GearDB? gearDB = null;
                ConsumeResultDB consumeResultDB = new();
                ParcelResultDB parcelResultDB = new();

                // Process equipment batch growth
                if (request.EquipmentBatchGrowthRequestDBs != null && request.EquipmentBatchGrowthRequestDBs.Count > 0)
                {
                    foreach (var batchGrowthDB in request.EquipmentBatchGrowthRequestDBs)
                    {
                        var targetEquipment = await _dbContext.Equipments
                            .FirstOrDefaultAsync(e => e.ServerId == batchGrowthDB.TargetServerId);

                        if (targetEquipment == null)
                            continue;

                        // TODO: Load EquipmentExcel and RecipeIngredientExcel
                        
                        // Handle TierUp if requested
                        if (batchGrowthDB.AfterTier > targetEquipment.Tier)
                        {
                            // TODO: Get equipment tier upgrade path from Excel
                            // TODO: Create recipes using RecipeIngredientExcel
                            // Placeholder: Simple tier increment
                            long nextTierEquipmentId = targetEquipment.UniqueId + (1000 * (batchGrowthDB.AfterTier - targetEquipment.Tier));
                            targetEquipment.UniqueId = nextTierEquipmentId;
                            targetEquipment.Tier = (int)batchGrowthDB.AfterTier;
                        }

                        // Handle LevelUp
                        // TODO: Process ConsumeRequestDBs using ConsumeHandler
                        // TODO: Calculate exp and level using MathService and EquipmentService
                        // Placeholder: Add some exp and level
                        if (batchGrowthDB.ConsumeRequestDBs != null && batchGrowthDB.ConsumeRequestDBs.Count > 0)
                        {
                            long expGain = 2000; // Placeholder
                            targetEquipment.Exp += expGain;

                            int expPerLevel = 1000;
                            while (targetEquipment.Exp >= expPerLevel)
                            {
                                targetEquipment.Level++;
                                targetEquipment.Exp -= expPerLevel;
                            }
                        }

                        _dbContext.Equipments.Update(targetEquipment);
                        equipmentDBs.Add(new EquipmentDB
                        {
                            ServerId = targetEquipment.ServerId,
                            UniqueId = targetEquipment.UniqueId,
                            Level = targetEquipment.Level,
                            Exp = targetEquipment.Exp,
                            Tier = targetEquipment.Tier,
                            StackCount = targetEquipment.StackCount,
                            BoundCharacterServerId = targetEquipment.BoundCharacterServerId,
                            IsNew = targetEquipment.IsNew,
                            IsLocked = targetEquipment.IsLocked
                        });
                    }
                }

                // Process gear tier up if requested (Property doesn't exist in EquipmentBatchGrowthRequest yet)
                // TODO: Add GearTierUpRequestDB property to EquipmentBatchGrowthRequest
                /*
                if (request.GearTierUpRequestDB != null)
                {
                    var targetGear = await _dbContext.Gears
                        .FirstOrDefaultAsync(g => g.ServerId == request.GearTierUpRequestDB.TargetServerId);

                    if (targetGear != null)
                    {
                        var targetCharacter = await _dbContext.Characters
                            .FirstOrDefaultAsync(c => c.ServerId == targetGear.BoundCharacterServerId);

                        // TODO: Load CharacterGearExcel for the character
                        // TODO: Get next tier gear data
                        // TODO: Consume recipe using RecipeIngredientExcel
                        
                        // Placeholder: Simple tier up
                        long nextTierGearId = targetGear.UniqueId + 100;
                        int nextTier = targetGear.Tier + 1;

                        targetGear.UniqueId = nextTierGearId;
                        targetGear.Tier = nextTier;
                        _dbContext.Gears.Update(targetGear);

                        gearDB = new GearDB
                        {
                            ServerId = targetGear.ServerId,
                            UniqueId = targetGear.UniqueId,
                            Level = targetGear.Level,
                            Exp = targetGear.Exp,
                            Tier = targetGear.Tier,
                            BoundCharacterServerId = targetGear.BoundCharacterServerId
                        };
                    }
                }
                */

                await _dbContext.SaveChangesAsync();

                return new EquipmentBatchGrowthResponse
                {
                    EquipmentDBs = equipmentDBs.Count > 0 ? equipmentDBs : null,
                    // GearDB = gearDB, // EquipmentBatchGrowthResponse doesn't have GearDB property
                    ConsumeResultDB = consumeResultDB,
                    ParcelResultDB = parcelResultDB
                };
            }
        }
    }
}