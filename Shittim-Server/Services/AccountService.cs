using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Plana.FlatData;

namespace BlueArchiveAPI.Services
{
    /// <summary>
    /// Service for account initialization and management
    /// Based on Atrahasis AccountService but adapted for Shittim-Server architecture
    /// </summary>
    public static class AccountInitializationService
    {
        private static ExcelTableService? _excelService;
        private static IExcelSqlService? _excelSqlService;

        public static void Initialize(ExcelTableService excelService, IExcelSqlService excelSqlService = null)
        {
            _excelService = excelService;
            _excelSqlService = excelSqlService;
        }

        /// <summary>
        /// Creates a complete new account with all required starter data
        /// This ensures the game client accepts the account as valid
        /// </summary>
        public static async Task InitializeCompleteAccount(BAContext context, User user)
        {
            if (_excelService == null)
                throw new InvalidOperationException("AccountInitializationService not initialized. Call Initialize() first.");
            
            var now = DateTime.UtcNow;
            
            // 1. Currency initialization with transaction tracking
            await InitializeCurrency(context, user, now);
            
            // 2. Characters from Excel
            var existingChars = await InitializeCharacters(context, user);
            
            // 3. Items from Excel
            await InitializeItems(context, user);
            
            // 4. Echelon
            await InitializeEchelon(context, user, existingChars);
            
            // 5. Echelon presets
            await InitializeEchelonPresets(context, user);
            
            // 6. Cafés with visitors and furniture
            await InitializeCafes(context, user, existingChars);
            
            // 7. Academy
            await InitializeAcademy(context, user, existingChars);
            
            // 8. Account Attachment
            await InitializeAttachment(context, user);
            
            // 9. Sticker Book
            await InitializeStickerBook(context, user);
            
            // 10. Event Content Permanent
            await InitializeEventContent(context, user, now);
            
            // 11. Welcome Mail
            await InitializeWelcomeMail(context, user);
            
            // 12. Representative Character
            await SetRepresentativeCharacter(context, user, existingChars);
        }
        
        private static async Task InitializeCurrency(BAContext context, User user, DateTime now)
        {
            var currency = await context.AccountCurrencies.FirstOrDefaultAsync(c => c.AccountServerId == user.Id);
            if (currency != null) return;
            
            var currencyDict = BuildStartingCurrency();
            var updateTimeDict = new Dictionary<NetworkModels.CurrencyTypes, DateTime>();
            foreach (var key in currencyDict.Keys)
            {
                updateTimeDict[key] = now;
            }
            
            currency = new AccountCurrency
            {
                AccountServerId = user.Id,
                AccountLevel = 1,
                AcademyLocationRankSum = 1,
                CurrencyDict = JsonSerializer.Serialize(currencyDict),
                UpdateTimeDict = JsonSerializer.Serialize(updateTimeDict)
            };
            context.AccountCurrencies.Add(currency);
            await context.SaveChangesAsync();
            
            // Seed currency transactions
            foreach (var kvp in currencyDict)
            {
                context.CurrencyTransactions.Add(new Models.CurrencyTransaction
                {
                    AccountServerId = user.Id,
                    CurrencyType = kvp.Key,
                    TransactionTime = now,
                    AmountChange = kvp.Value,
                    NewBalance = kvp.Value,
                    Reason = "Account creation"
                });
            }
            await context.SaveChangesAsync();
        }
        
        private static async Task<List<Character>> InitializeCharacters(BAContext context, User user)
        {
            var existing = await context.Characters.Where(c => c.AccountServerId == user.Id).ToListAsync();
            if (existing.Count > 0) return existing;
            
            var defaultCharacters = _excelService.GetTable<DefaultCharacterExcelT>();
            Console.WriteLine($"[AccountService] Loaded {defaultCharacters.Count} default characters from Excel");
            
            if (defaultCharacters.Count == 0)
            {
                Console.WriteLine("[AccountService] WARNING: No default characters in Excel! Using fallback.");
                defaultCharacters = new List<DefaultCharacterExcelT>
                {
                    new DefaultCharacterExcelT { CharacterId = 13010, StarGrade = 2, Level = 1, Exp = 0, FavorRank = 1, FavorExp = 0, CommonSkillLevel = 1, ExSkillLevel = 1, PassiveSkillLevel = 1, ExtraPassiveSkillLevel = 1, LeaderSkillLevel = 1, FavoriteCharacter = false },
                    new DefaultCharacterExcelT { CharacterId = 16003, StarGrade = 1, Level = 1, Exp = 0, FavorRank = 1, FavorExp = 0, CommonSkillLevel = 1, ExSkillLevel = 1, PassiveSkillLevel = 1, ExtraPassiveSkillLevel = 1, LeaderSkillLevel = 1, FavoriteCharacter = false },
                    new DefaultCharacterExcelT { CharacterId = 13003, StarGrade = 2, Level = 1, Exp = 0, FavorRank = 1, FavorExp = 0, CommonSkillLevel = 1, ExSkillLevel = 1, PassiveSkillLevel = 1, ExtraPassiveSkillLevel = 1, LeaderSkillLevel = 1, FavoriteCharacter = false },
                    new DefaultCharacterExcelT { CharacterId = 26000, StarGrade = 1, Level = 1, Exp = 0, FavorRank = 1, FavorExp = 0, CommonSkillLevel = 1, ExSkillLevel = 1, PassiveSkillLevel = 1, ExtraPassiveSkillLevel = 1, LeaderSkillLevel = 1, FavoriteCharacter = false }
                };
            }
            
            foreach (var dc in defaultCharacters)
            {
                context.Characters.Add(new Character
                {
                    AccountServerId = user.Id,
                    UniqueId = dc.CharacterId,
                    StarGrade = dc.StarGrade,
                    Level = dc.Level,
                    Exp = dc.Exp,
                    FavorRank = dc.FavorRank,
                    FavorExp = dc.FavorExp,
                    PublicSkillLevel = dc.CommonSkillLevel,
                    ExSkillLevel = dc.ExSkillLevel,
                    PassiveSkillLevel = dc.PassiveSkillLevel,
                    ExtraPassiveSkillLevel = dc.ExtraPassiveSkillLevel,
                    LeaderSkillLevel = dc.LeaderSkillLevel,
                    IsNew = true,
                    IsLocked = false,
                    IsFavorite = dc.FavoriteCharacter,
                    EquipmentServerIds = "[0,0,0]",
                    PotentialStats = "{\"1\":0,\"2\":0,\"3\":0}"
                });
            }
            await context.SaveChangesAsync();
            return await context.Characters.Where(c => c.AccountServerId == user.Id).ToListAsync();
        }
        
        private static async Task InitializeItems(BAContext context, User user)
        {
            var existing = await context.Items.Where(i => i.AccountServerId == user.Id).ToListAsync();
            if (existing.Count > 0) return;
            
            var defaultParcels = _excelService.GetTable<DefaultParcelExcelT>();
            foreach (var parcel in defaultParcels.Where(p => p.ParcelType == Plana.FlatData.ParcelType.Item))
            {
                context.Items.Add(new Item
                {
                    AccountServerId = user.Id,
                    UniqueId = parcel.ParcelId,
                    StackCount = parcel.ParcelAmount,
                    IsNew = false,
                    IsLocked = false
                });
            }
            await context.SaveChangesAsync();
        }
        
        private static async Task InitializeEchelon(BAContext context, User user, List<Character> chars)
        {
            var existing = await context.Echelons.FirstOrDefaultAsync(e => e.AccountServerId == user.Id && e.EchelonType == 1);
            if (existing != null || chars.Count == 0) return;
            
            var mainSlots = new long[4];
            for (int i = 0; i < Math.Min(chars.Count, 4); i++)
                mainSlots[i] = chars[i].ServerId;
            
            context.Echelons.Add(new Echelon
            {
                AccountServerId = user.Id,
                EchelonType = 1,
                EchelonNumber = 1,
                LeaderServerId = chars[0].ServerId,
                MainSlotServerIds = JsonSerializer.Serialize(mainSlots),
                SupportSlotServerIds = JsonSerializer.Serialize(new[] { 0L, 0L }),
                SkillCardMulliganCharacterIds = JsonSerializer.Serialize(new long[] { }),
                CombatStyleIndex = JsonSerializer.Serialize(new[] { 0, 0, 0, 0, 0, 0 })
            });
            await context.SaveChangesAsync();
        }
        
        private static async Task InitializeEchelonPresets(BAContext context, User user)
        {
            var existing = await context.EchelonPresets.Where(p => p.AccountServerId == user.Id).ToListAsync();
            if (existing.Count > 0) return;
            
            for (int ext = 0; ext <= 1; ext++)
            {
                for (int groupIndex = 0; groupIndex < 4; groupIndex++)
                {
                    context.EchelonPresetGroups.Add(new EchelonPresetGroup
                    {
                        AccountServerId = user.Id,
                        GroupIndex = groupIndex,
                        ExtensionType = ext,
                        GroupLabel = $"Group {groupIndex + 1}"
                    });
                    
                    for (int presetIndex = 0; presetIndex < 5; presetIndex++)
                    {
                        context.EchelonPresets.Add(new EchelonPreset
                        {
                            AccountServerId = user.Id,
                            GroupIndex = groupIndex,
                            Index = presetIndex,
                            ExtensionType = ext,
                            Label = $"Preset {presetIndex + 1}",
                            StrikerUniqueIds = JsonSerializer.Serialize(ext == 0 ? new[] { 0L, 0L, 0L, 0L } : new[] { 0L, 0L, 0L, 0L, 0L, 0L }),
                            SpecialUniqueIds = JsonSerializer.Serialize(ext == 0 ? new[] { 0L, 0L } : new[] { 0L, 0L, 0L, 0L }),
                            CombatStyleIndex = JsonSerializer.Serialize(ext == 0 ? new[] { 0, 0, 0, 0, 0, 0 } : new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }),
                            MulliganUniqueIds = JsonSerializer.Serialize(new long[] { })
                        });
                    }
                }
            }
            await context.SaveChangesAsync();
        }
        
        private static async Task InitializeCafes(BAContext context, User user, List<Character> chars)
        {
            var existing = await context.Cafes.Where(c => c.AccountServerId == user.Id).ToListAsync();
            if (existing.Count > 0) return;
            
            var cafeService = new CafeService(_excelService);
            
            for (int cafeId = 1; cafeId <= 2; cafeId++)
            {
                var visitors = chars.Count > 0 
                    ? cafeService.SelectRandomVisitors(chars, user.Id, cafeId, DateTime.UtcNow)
                    : new List<long>();
                
                var visitorDict = new Dictionary<long, object>();
                foreach (var vid in visitors)
                {
                    visitorDict[vid] = new { UniqueId = vid, ServerId = 0L, IsSummon = false, LastInteractTime = DateTime.MinValue };
                }
                
                context.Cafes.Add(new Cafe
                {
                    AccountServerId = user.Id,
                    CafeId = cafeId,
                    CafeRank = 1,
                    LastUpdate = DateTime.UtcNow,
                    LastSummonDate = DateTime.UtcNow.AddDays(-30),
                    IsNew = false,
                    CafeVisitCharacterDBs = JsonSerializer.Serialize(visitorDict),
                    ProductionData = JsonSerializer.Serialize(new { }),
                    LastProductionCollectTime = DateTime.UtcNow
                });
            }
            await context.SaveChangesAsync();
            
            var cafes = await context.Cafes.Where(c => c.AccountServerId == user.Id).ToListAsync();
            var furnitureSetup = GetDefaultFurniture();
            
            foreach (var cafe in cafes)
            {
                foreach (var furn in furnitureSetup)
                {
                    context.Furnitures.Add(new Furniture
                    {
                        AccountServerId = user.Id,
                        CafeDBId = cafe.CafeDBId,
                        UniqueId = furn.UniqueId,
                        Location = furn.Location,
                        PositionX = furn.PositionX,
                        PositionY = furn.PositionY,
                        Rotation = furn.Rotation,
                        StackCount = 1,
                        ItemDeploySequence = 0
                    });
                }
            }
            await context.SaveChangesAsync();
        }
        
        private static async Task InitializeAcademy(BAContext context, User user, List<Character> chars)
        {
            var existing = await context.Academies.FirstOrDefaultAsync(a => a.AccountServerId == user.Id);
            if (existing != null) return;
            
            for (long locationId = 1; locationId <= 10; locationId++)
            {
                context.AcademyLocations.Add(new AcademyLocation
                {
                    AccountServerId = user.Id,
                    LocationId = locationId,
                    Rank = 12,
                    Exp = 0
                });
            }
            
            var zoneVisitors = new Dictionary<long, List<long>>();
            if (chars.Count > 0)
            {
                for (long zoneId = 1; zoneId <= 3; zoneId++)
                {
                    var visitorsForZone = new List<long>();
                    for (int i = 0; i < Math.Min(2, chars.Count); i++)
                    {
                        var charIndex = ((int)zoneId - 1 + i) % chars.Count;
                        visitorsForZone.Add(chars[charIndex].UniqueId);
                    }
                    zoneVisitors[zoneId] = visitorsForZone;
                }
            }
            
            context.Academies.Add(new Academy
            {
                AccountServerId = user.Id,
                LastUpdate = DateTime.UtcNow,
                ZoneVisitCharacterIds = JsonSerializer.Serialize(zoneVisitors),
                ZoneScheduleGroupRecords = JsonSerializer.Serialize(new Dictionary<long, long>())
            });
            await context.SaveChangesAsync();
        }
        
        private static async Task InitializeAttachment(BAContext context, User user)
        {
            var existing = await context.AccountAttachments.FirstOrDefaultAsync(a => a.AccountServerId == user.Id);
            if (existing != null) return;
            
            context.AccountAttachments.Add(new AccountAttachment
            {
                AccountServerId = user.Id,
                AccountId = user.Id,
                EmblemUniqueId = 0
            });
            await context.SaveChangesAsync();
        }
        
        private static async Task InitializeStickerBook(BAContext context, User user)
        {
            var existing = await context.StickerBooks.FirstOrDefaultAsync(s => s.AccountServerId == user.Id);
            if (existing != null) return;
            
            context.StickerBooks.Add(new StickerBook
            {
                AccountServerId = user.Id,
                UnusedStickerIds = JsonSerializer.Serialize(new List<long>()),
                UsedStickerIds = JsonSerializer.Serialize(new List<long>())
            });
            await context.SaveChangesAsync();
        }
        
        private static async Task InitializeEventContent(BAContext context, User user, DateTime now)
        {
            var existing = await context.EventContentPermanents.Where(e => e.AccountServerId == user.Id).ToListAsync();
            if (existing.Count > 0) return;
            
            var eventIds = GetPermanentEventIds();
            foreach (var eventId in eventIds)
            {
                context.EventContentPermanents.Add(new EventContentPermanent
                {
                    AccountServerId = user.Id,
                    EventContentId = eventId,
                    IsStageAllClear = false,
                    IsReceivedCharacterReward = false
                });
            }
            await context.SaveChangesAsync();
            Console.WriteLine($"[Events] Initialized {eventIds.Count} permanent events");
        }
        
        private static async Task InitializeWelcomeMail(BAContext context, User user)
        {
            var count = await context.Mails.Where(m => m.AccountServerId == user.Id).CountAsync();
            if (count > 0) return;
            
            var parcelInfos = new List<object>
            {
                new { Type = 1, Id = 1, Amount = 10000 },
                new { Type = 1, Id = 2, Amount = 300 },
                new { Type = 2, Id = 12, Amount = 10 },
                new { Type = 2, Id = 11, Amount = 10 }
            };
            
            context.Mails.Add(new Mail
            {
                AccountServerId = user.Id,
                Type = 1,
                UniqueId = 1,
                Sender = "Shittim Server",
                Comment = "Welcome to Blue Archive!",
                SendDate = DateTime.UtcNow,
                ExpireDate = DateTime.UtcNow.AddDays(30),
                IsRead = false,
                IsReceived = false,
                ParcelInfos = JsonSerializer.Serialize(parcelInfos),
                RemainParcelInfos = JsonSerializer.Serialize(parcelInfos)
            });
            await context.SaveChangesAsync();
        }
        
        private static async Task SetRepresentativeCharacter(BAContext context, User user, List<Character> chars)
        {
            if (user.RepresentCharacterServerId != 0 || chars.Count == 0) return;
            
            var defaultCharacters = _excelService.GetTable<DefaultCharacterExcelT>();
            var favCharacter = defaultCharacters.FirstOrDefault(x => x.FavoriteCharacter);
            
            if (favCharacter != null)
            {
                var favCharInDb = chars.FirstOrDefault(c => c.UniqueId == favCharacter.CharacterId);
                user.RepresentCharacterServerId = favCharInDb != null ? (int)favCharInDb.ServerId : (int)chars[0].ServerId;
            }
            else
            {
                user.RepresentCharacterServerId = (int)chars[0].ServerId;
            }
            await context.SaveChangesAsync();
        }
        
        private static Dictionary<NetworkModels.CurrencyTypes, long> BuildStartingCurrency()
        {
            var currencyDict = new Dictionary<NetworkModels.CurrencyTypes, long>();
            
            // Match Atrahasis: Initialize all to 0 first
            foreach (var type in Enum.GetValues(typeof(NetworkModels.CurrencyTypes)).Cast<NetworkModels.CurrencyTypes>())
            {
                if (type == NetworkModels.CurrencyTypes.Invalid) continue;
                currencyDict[type] = 0;
            }
            
            // Try to load overrides from ExcelDB.db via SQL if available
            if (_excelSqlService != null)
            {
                try
                {
                    var constants = _excelSqlService.GetConstantsAsync().Result;
                    if (constants.Count > 0)
                    {
                        Console.WriteLine($"[Currency] Found {constants.Count} constants in ExcelDB.db");
                        
                        // Map known keys to currency types
                        if (constants.TryGetValue("DefaultGold", out var gold) && long.TryParse(gold, out var goldVal))
                        {
                            currencyDict[NetworkModels.CurrencyTypes.Gold] = goldVal;
                            Console.WriteLine($"[Currency] DefaultGold = {goldVal} (from ExcelDB)");
                        }
                        if (constants.TryGetValue("DefaultActionPoint", out var ap) && long.TryParse(ap, out var apVal))
                        {
                            currencyDict[NetworkModels.CurrencyTypes.ActionPoint] = apVal;
                            Console.WriteLine($"[Currency] DefaultActionPoint = {apVal} (from ExcelDB)");
                        }
                        if (constants.TryGetValue("DefaultGem", out var gem) && long.TryParse(gem, out var gemVal))
                        {
                            currencyDict[NetworkModels.CurrencyTypes.Gem] = gemVal;
                            Console.WriteLine($"[Currency] DefaultGem = {gemVal} (from ExcelDB)");
                        }
                        // Can add more mappings as needed
                    }
                    else
                    {
                        Console.WriteLine("[Currency] WARNING: No constants found in ExcelDB.db");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Currency] WARNING: Failed to query ExcelDB.db: {ex.Message}");
                }
            }
            
            Console.WriteLine($"[Currency] Initialized {currencyDict.Count} currency types");
            Console.WriteLine("[Currency] NOTE: DefaultParcelExcel items will add/override balances");
            
            return currencyDict;
        }
        
        private static List<(long UniqueId, int Location, float PositionX, float PositionY, float Rotation)> GetDefaultFurniture()
        {
            try
            {
                var defaultFurniture = _excelService.GetTable<DefaultFurnitureExcelT>();
                
                if (defaultFurniture != null && defaultFurniture.Count > 0)
                {
                    Console.WriteLine($"[Furniture] Loading from DefaultFurnitureExcel ({defaultFurniture.Count} items)");
                    return defaultFurniture.Take(3).Select(f => (f.Id, (int)f.Location, f.PositionX, f.PositionY, f.Rotation)).ToList();
                }
            }
            catch { }
            
            Console.WriteLine("[Furniture] Using test IDs (DefaultFurnitureExcel empty/unavailable)");
            return new List<(long, int, float, float, float)>
            {
                (1L, 2, 0.0f, 0.0f, 0.0f),
                (2L, 4, 0.0f, 0.0f, 0.0f),
                (3L, 3, 0.0f, 0.0f, 0.0f)
            };
        }
        
        private static List<long> GetPermanentEventIds()
        {
            try
            {
                // Match Atrahasis: EventContentSeasonExcel where IsReturn == true
                var eventContentSeasons = _excelService.GetTable<EventContentSeasonExcelT>();
                
                if (eventContentSeasons != null && eventContentSeasons.Count > 0)
                {
                    var permanentEvents = eventContentSeasons
                        .Where(x => x.IsReturn == true)
                        .Select(x => x.EventContentId)
                        .Distinct()
                        .ToList();
                    
                    Console.WriteLine($"[Events] Loaded {permanentEvents.Count} permanent events from EventContentSeasonExcel (IsReturn=true)");
                    return permanentEvents;
                }
                else
                {
                    Console.WriteLine("[Events] WARNING: EventContentSeasonExcel empty");
                    throw new Exception();
                }
            }
            catch
            {
                Console.WriteLine("[Events] Fallback: Using Oct 2025 snapshot");
                return new List<long>
                {
                    10801, 10802, 10803, 10804, 10805, 10806, 10808, 10810, 10811, 10812,
                    10809, 10813, 10816, 10814, 10815, 10817, 10818, 10819, 10820, 10826,
                    10825, 10827, 10829, 10828, 10833, 10830, 10832, 10834, 10835,
                    900801, 900802, 900803, 900804, 900805, 900806, 900808, 900809, 900810,
                    900811, 900812, 900813, 900814, 900815, 900816, 900817, 900818, 900819,
                    900820, 900825, 900826, 900701
                };
            }
        }
    }
}
