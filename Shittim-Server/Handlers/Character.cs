// Models needed: Character (already exists), Weapon (already exists)
// BAContext additions: DbSet<Weapon> (already added)
// Migrations: Included in AddAccountGameData migration

using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlueArchiveAPI.Handlers
{
    public static class Character
    {
        /// <summary>
        /// Sets favorite status for characters
        /// Protocol: Character_SetFavorites
        /// </summary>
        public class SetFavorites : BaseHandler<CharacterSetFavoritesRequest, CharacterSetFavoritesResponse>
        {
            private readonly BAContext _dbContext;

            public SetFavorites(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CharacterSetFavoritesResponse> Handle(CharacterSetFavoritesRequest request)
            {
                var characterServerIds = request.ActivateByServerIds.Keys.ToList();
                var characters = await _dbContext.Characters
                    .Where(c => c.AccountServerId == request.SessionKey.AccountServerId 
                             && characterServerIds.Contains(c.ServerId))
                    .ToListAsync();

                foreach (var character in characters)
                {
                    character.IsFavorite = request.ActivateByServerIds[character.ServerId];
                }

                await _dbContext.SaveChangesAsync();

                // Map to network model
                var characterDBs = characters.Select(c => new CharacterDB
                {
                    ServerId = c.ServerId,
                    UniqueId = c.UniqueId,
                    StarGrade = c.StarGrade,
                    Level = c.Level,
                    Exp = c.Exp,
                    FavorRank = c.FavorRank,
                    FavorExp = c.FavorExp,
                    PublicSkillLevel = c.PublicSkillLevel,
                    ExSkillLevel = c.ExSkillLevel,
                    PassiveSkillLevel = c.PassiveSkillLevel,
                    ExtraPassiveSkillLevel = c.ExtraPassiveSkillLevel,
                    LeaderSkillLevel = c.LeaderSkillLevel,
                    IsNew = c.IsNew,
                    IsLocked = c.IsLocked,
                    IsFavorite = c.IsFavorite,
                    EquipmentServerIds = !string.IsNullOrEmpty(c.EquipmentServerIds)
                        ? JsonSerializer.Deserialize<List<long>>(c.EquipmentServerIds) ?? new List<long>()
                        : new List<long>()
                }).ToList();

                return new CharacterSetFavoritesResponse
                {
                    CharacterDBs = characterDBs
                };
            }
        }

        /// <summary>
        /// Levels up character experience using consumed items
        /// Protocol: Character_ExpGrowth
        /// </summary>
        public class ExpGrowth : BaseHandler<CharacterExpGrowthRequest, CharacterExpGrowthResponse>
        {
            private readonly BAContext _dbContext;

            public ExpGrowth(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CharacterExpGrowthResponse> Handle(CharacterExpGrowthRequest request)
            {
                var character = await _dbContext.Characters
                    .FirstOrDefaultAsync(c => c.ServerId == request.TargetCharacterServerId);

                if (character == null)
                    throw new Exception("Character not found");

                var currency = await _dbContext.AccountCurrencies
                    .FirstOrDefaultAsync(c => c.AccountServerId == request.SessionKey.AccountServerId);

                if (currency == null)
                    throw new Exception("Currency not found");

                // TODO: Load exp items from ConsumeRequestDB and calculate total exp
                // For now, use a simple exp calculation
                long addExp = 1000; // Placeholder - should calculate from consumed items
                
                // TODO: Load character level exp table from Excel
                // For now, use simple level up logic
                long expRequired = 1000; // Placeholder
                
                character.Exp += addExp;
                while (character.Exp >= expRequired && character.Level < 90)
                {
                    character.Level++;
                    character.Exp -= expRequired;
                    expRequired = character.Level * 1000; // Simple formula
                }

                // Calculate credit cost (gold)
                long creditCost = addExp / 10; // Simple cost calculation
                
                var currencyDict = !string.IsNullOrEmpty(currency.CurrencyDict)
                    ? JsonSerializer.Deserialize<Dictionary<CurrencyTypes, long>>(currency.CurrencyDict) ?? new Dictionary<CurrencyTypes, long>()
                    : new Dictionary<CurrencyTypes, long>();
                
                if (currencyDict.ContainsKey(CurrencyTypes.Gold))
                    currencyDict[CurrencyTypes.Gold] -= creditCost;
                
                currency.CurrencyDict = JsonSerializer.Serialize(currencyDict);

                await _dbContext.SaveChangesAsync();

                return new CharacterExpGrowthResponse
                {
                    CharacterDB = new CharacterDB
                    {
                        ServerId = character.ServerId,
                        UniqueId = character.UniqueId,
                        StarGrade = character.StarGrade,
                        Level = character.Level,
                        Exp = character.Exp,
                        FavorRank = character.FavorRank,
                        FavorExp = character.FavorExp,
                        PublicSkillLevel = character.PublicSkillLevel,
                        ExSkillLevel = character.ExSkillLevel,
                        PassiveSkillLevel = character.PassiveSkillLevel,
                        ExtraPassiveSkillLevel = character.ExtraPassiveSkillLevel,
                        LeaderSkillLevel = character.LeaderSkillLevel,
                        IsNew = character.IsNew,
                        IsLocked = character.IsLocked,
                        IsFavorite = character.IsFavorite,
                        EquipmentServerIds = !string.IsNullOrEmpty(character.EquipmentServerIds)
                            ? JsonSerializer.Deserialize<List<long>>(character.EquipmentServerIds) ?? new List<long>()
                            : new List<long>()
                    },
                    AccountCurrencyDB = new AccountCurrencyDB
                    {
                        AccountLevel = currency.AccountLevel,
                        AcademyLocationRankSum = currency.AcademyLocationRankSum,
                        CurrencyDict = currencyDict,
                        UpdateTimeDict = new Dictionary<CurrencyTypes, DateTime>()
                    },
                    ConsumeResultDB = new ConsumeResultDB
                    {
                        // TODO: Build from consumed items
                        RemovedServerIds = new List<long>(),
                        UsedServerIdAndRemainingCounts = new Dictionary<long, long>()
                    }
                };
            }
        }

        /// <summary>
        /// Increases character favor (affection) using items
        /// Protocol: Character_FavorGrowth
        /// </summary>
        public class FavorGrowth : BaseHandler<CharacterFavorGrowthRequest, CharacterFavorGrowthResponse>
        {
            private readonly BAContext _dbContext;

            public FavorGrowth(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CharacterFavorGrowthResponse> Handle(CharacterFavorGrowthRequest request)
            {
                var character = await _dbContext.Characters
                    .FirstOrDefaultAsync(c => c.ServerId == request.TargetCharacterDBId);

                if (character == null)
                    throw new Exception("Character not found");

                // TODO: Calculate favor exp from consumed items
                long addFavorExp = 1000; // Placeholder
                
                // TODO: Load favor rank requirements from Excel
                long favorExpRequired = 10000; // Placeholder
                
                character.FavorExp += addFavorExp;
                while (character.FavorExp >= favorExpRequired && character.FavorRank < 20)
                {
                    character.FavorRank++;
                    character.FavorExp -= favorExpRequired;
                    favorExpRequired = character.FavorRank * 10000; // Simple formula
                }

                await _dbContext.SaveChangesAsync();

                return new CharacterFavorGrowthResponse
                {
                    CharacterDB = new CharacterDB
                    {
                        ServerId = character.ServerId,
                        UniqueId = character.UniqueId,
                        StarGrade = character.StarGrade,
                        Level = character.Level,
                        Exp = character.Exp,
                        FavorRank = character.FavorRank,
                        FavorExp = character.FavorExp,
                        PublicSkillLevel = character.PublicSkillLevel,
                        ExSkillLevel = character.ExSkillLevel,
                        PassiveSkillLevel = character.PassiveSkillLevel,
                        ExtraPassiveSkillLevel = character.ExtraPassiveSkillLevel,
                        LeaderSkillLevel = character.LeaderSkillLevel,
                        IsNew = character.IsNew,
                        IsLocked = character.IsLocked,
                        IsFavorite = character.IsFavorite,
                        EquipmentServerIds = !string.IsNullOrEmpty(character.EquipmentServerIds)
                            ? JsonSerializer.Deserialize<List<long>>(character.EquipmentServerIds) ?? new List<long>()
                            : new List<long>()
                    },
                    ConsumeStackableItemDBResult = new List<ItemDB>(), // TODO: Track consumed items
                    ParcelResultDB = new ParcelResultDB() // TODO: Build parcel rewards if favor rank increased
                };
            }
        }

        /// <summary>
        /// Transcends (upgrades star grade) a character
        /// Protocol: Character_Transcendence
        /// </summary>
        public class Transcendence : BaseHandler<CharacterTranscendenceRequest, CharacterTranscendenceResponse>
        {
            private readonly BAContext _dbContext;

            public Transcendence(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CharacterTranscendenceResponse> Handle(CharacterTranscendenceRequest request)
            {
                var character = await _dbContext.Characters
                    .FirstOrDefaultAsync(c => c.ServerId == request.TargetCharacterServerId);

                if (character == null)
                    throw new Exception("Character not found");

                // TODO: Load recipe ingredients from CharacterTranscendenceExcel
                // TODO: Consume required items (eligma, etc.)
                
                // Increase star grade
                if (character.StarGrade < 5)
                {
                    character.StarGrade++;
                }

                await _dbContext.SaveChangesAsync();

                return new CharacterTranscendenceResponse
                {
                    CharacterDB = new CharacterDB
                    {
                        ServerId = character.ServerId,
                        UniqueId = character.UniqueId,
                        StarGrade = character.StarGrade,
                        Level = character.Level,
                        Exp = character.Exp,
                        FavorRank = character.FavorRank,
                        FavorExp = character.FavorExp,
                        PublicSkillLevel = character.PublicSkillLevel,
                        ExSkillLevel = character.ExSkillLevel,
                        PassiveSkillLevel = character.PassiveSkillLevel,
                        ExtraPassiveSkillLevel = character.ExtraPassiveSkillLevel,
                        LeaderSkillLevel = character.LeaderSkillLevel,
                        IsNew = character.IsNew,
                        IsLocked = character.IsLocked,
                        IsFavorite = character.IsFavorite,
                        EquipmentServerIds = !string.IsNullOrEmpty(character.EquipmentServerIds)
                            ? JsonSerializer.Deserialize<List<long>>(character.EquipmentServerIds) ?? new List<long>()
                            : new List<long>()
                    },
                    ParcelResultDB = new ParcelResultDB() // TODO: Build parcel result for consumed items
                };
            }
        }

        /// <summary>
        /// Unlocks a character's weapon
        /// Protocol: Character_UnlockWeapon
        /// </summary>
        public class UnlockWeapon : BaseHandler<CharacterUnlockWeaponRequest, CharacterUnlockWeaponResponse>
        {
            private readonly BAContext _dbContext;

            public UnlockWeapon(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CharacterUnlockWeaponResponse> Handle(CharacterUnlockWeaponRequest request)
            {
                var character = await _dbContext.Characters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ServerId == request.TargetCharacterServerId);

                if (character == null)
                    throw new Exception("Character not found");

                // Check if weapon already exists
                var weapon = await _dbContext.Weapons
                    .FirstOrDefaultAsync(w => w.BoundCharacterServerId == request.TargetCharacterServerId);

                if (weapon == null)
                {
                    // TODO: Load weapon UniqueId from CharacterWeaponExcel based on character.UniqueId
                    // For now, use character.UniqueId as weapon ID (simplified)
                    weapon = new Weapon
                    {
                        AccountServerId = request.SessionKey.AccountServerId,
                        UniqueId = character.UniqueId,
                        BoundCharacterServerId = request.TargetCharacterServerId,
                        StarGrade = 1,
                        Level = 1,
                        Exp = 0,
                        IsLocked = false
                    };
                    _dbContext.Weapons.Add(weapon);
                    await _dbContext.SaveChangesAsync();
                }

                return new CharacterUnlockWeaponResponse
                {
                    WeaponDB = new WeaponDB
                    {
                        UniqueId = weapon.UniqueId,
                        Level = weapon.Level,
                        Exp = weapon.Exp,
                        StarGrade = weapon.StarGrade,
                        BoundCharacterServerId = weapon.BoundCharacterServerId,
                        IsLocked = weapon.IsLocked
                    }
                };
            }
        }

        /// <summary>
        /// Increases weapon experience using consumed equipment
        /// Protocol: Character_WeaponExpGrowth
        /// </summary>
        public class WeaponExpGrowth : BaseHandler<CharacterWeaponExpGrowthRequest, CharacterWeaponExpGrowthResponse>
        {
            private readonly BAContext _dbContext;

            public WeaponExpGrowth(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CharacterWeaponExpGrowthResponse> Handle(CharacterWeaponExpGrowthRequest request)
            {
                var character = await _dbContext.Characters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ServerId == request.TargetCharacterServerId);

                if (character == null)
                    throw new Exception("Character not found");

                var weapon = await _dbContext.Weapons
                    .FirstOrDefaultAsync(w => w.BoundCharacterServerId == request.TargetCharacterServerId);

                if (weapon == null)
                    throw new Exception("Weapon not found - weapon must be unlocked first");

                // TODO: Calculate exp from consumed equipment in request.ConsumeUniqueIdAndCounts
                // TODO: Apply weapon type exp bonuses from CharacterWeaponExpBonusExcel
                long addExp = 5000; // Placeholder
                
                // TODO: Load weapon exp requirements from CharacterWeaponLevelExcel
                long expRequired = 10000; // Placeholder
                
                weapon.Exp += addExp;
                while (weapon.Exp >= expRequired && weapon.Level < 50)
                {
                    weapon.Level++;
                    weapon.Exp -= expRequired;
                    expRequired = weapon.Level * 10000; // Simple formula
                }

                // TODO: Calculate and consume gold cost
                // TODO: Remove consumed equipment from database

                await _dbContext.SaveChangesAsync();

                return new CharacterWeaponExpGrowthResponse
                {
                    ParcelResultDB = new ParcelResultDB
                    {
                        // TODO: Include updated weapon in WeaponDBs
                        WeaponDBs = new List<WeaponDB>
                        {
                            new WeaponDB
                            {
                                UniqueId = weapon.UniqueId,
                                Level = weapon.Level,
                                Exp = weapon.Exp,
                                StarGrade = weapon.StarGrade,
                                BoundCharacterServerId = weapon.BoundCharacterServerId,
                                IsLocked = weapon.IsLocked
                            }
                        }
                    }
                };
            }
        }

        /// <summary>
        /// Transcends (upgrades star grade) a weapon
        /// Protocol: Character_WeaponTranscendence
        /// </summary>
        public class WeaponTranscendence : BaseHandler<CharacterWeaponTranscendenceRequest, CharacterWeaponTranscendenceResponse>
        {
            private readonly BAContext _dbContext;

            public WeaponTranscendence(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CharacterWeaponTranscendenceResponse> Handle(CharacterWeaponTranscendenceRequest request)
            {
                var character = await _dbContext.Characters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ServerId == request.TargetCharacterServerId);

                if (character == null)
                    throw new Exception("Character not found");

                var weapon = await _dbContext.Weapons
                    .FirstOrDefaultAsync(w => w.BoundCharacterServerId == request.TargetCharacterServerId);

                if (weapon == null)
                    throw new Exception("Weapon not found");

                // TODO: Load recipe from CharacterWeaponExcel based on current star grade
                // TODO: Consume required items
                
                if (weapon.StarGrade < 5)
                {
                    weapon.StarGrade++;
                }

                await _dbContext.SaveChangesAsync();

                return new CharacterWeaponTranscendenceResponse
                {
                    ParcelResultDB = new ParcelResultDB
                    {
                        WeaponDBs = new List<WeaponDB>
                        {
                            new WeaponDB
                            {
                                UniqueId = weapon.UniqueId,
                                Level = weapon.Level,
                                Exp = weapon.Exp,
                                StarGrade = weapon.StarGrade,
                                BoundCharacterServerId = weapon.BoundCharacterServerId,
                                IsLocked = weapon.IsLocked
                            }
                        }
                    }
                };
            }
        }

        /// <summary>
        /// Lists all characters for an account
        /// Protocol: Character_List
        /// </summary>
        public class List : BaseHandler<CharacterListRequest, CharacterListResponse>
        {
            private readonly BAContext _dbContext;

            public List(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<CharacterListResponse> Handle(CharacterListRequest request)
            {
                var characters = await _dbContext.Characters
                    .AsNoTracking()
                    .Where(c => c.AccountServerId == request.SessionKey.AccountServerId)
                    .ToListAsync();

                var weapons = await _dbContext.Weapons
                    .AsNoTracking()
                    .Where(w => w.AccountServerId == request.SessionKey.AccountServerId)
                    .ToListAsync();

                var characterDBs = characters.Select(c => new CharacterDB
                {
                    ServerId = c.ServerId,
                    UniqueId = c.UniqueId,
                    StarGrade = c.StarGrade,
                    Level = c.Level,
                    Exp = c.Exp,
                    FavorRank = c.FavorRank,
                    FavorExp = c.FavorExp,
                    PublicSkillLevel = c.PublicSkillLevel,
                    ExSkillLevel = c.ExSkillLevel,
                    PassiveSkillLevel = c.PassiveSkillLevel,
                    ExtraPassiveSkillLevel = c.ExtraPassiveSkillLevel,
                    LeaderSkillLevel = c.LeaderSkillLevel,
                    IsNew = c.IsNew,
                    IsLocked = c.IsLocked,
                    IsFavorite = c.IsFavorite,
                    EquipmentServerIds = !string.IsNullOrEmpty(c.EquipmentServerIds)
                        ? JsonSerializer.Deserialize<List<long>>(c.EquipmentServerIds) ?? new List<long>()
                        : new List<long>()
                }).ToList();

                var weaponDBs = weapons.Select(w => new WeaponDB
                {
                    UniqueId = w.UniqueId,
                    Level = w.Level,
                    Exp = w.Exp,
                    StarGrade = w.StarGrade,
                    BoundCharacterServerId = w.BoundCharacterServerId,
                    IsLocked = w.IsLocked
                }).ToList();

                return new CharacterListResponse
                {
                    CharacterDBs = characterDBs,
                    TSSCharacterDBs = new List<CharacterDB>(), // TSS = Tactical Support System characters
                    WeaponDBs = weaponDBs
                };
            }
        }
    }
}
