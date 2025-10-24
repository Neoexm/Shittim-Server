// File: Shittim-Server/Handlers/Item.cs
// Handlers: List (Item_List), SelectTicket (Item_SelectTicket), AutoSynth (Item_AutoSynth)

using BlueArchiveAPI.NetworkModels;
using BlueArchiveAPI.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlueArchiveAPI.Handlers
{
    public static class Item
    {
        /// <summary>
        /// Handler for Item_List protocol.
        /// Returns all items owned by the account.
        /// </summary>
        public class List : BaseHandler<ItemListRequest, ItemListResponse>
        {
            private readonly BAContext _dbContext;

            public List(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<ItemListResponse> Handle(ItemListRequest request)
            {
                var user = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                var items = await _dbContext.Items
                    .AsNoTracking()
                    .Where(i => i.AccountServerId == user.Id)
                    .ToListAsync();

                return new ItemListResponse
                {
                    ItemDBs = items.Select(i => new ItemDB
                    {
                        ServerId = i.ServerId,
                        UniqueId = i.UniqueId,
                        StackCount = i.StackCount,
                        Type = i.UniqueId == 2 ? ParcelType.Item : ParcelType.None,
                        CanConsume = i.UniqueId == 2
                        // IsNew/IsLocked are JsonIgnored - won't be serialized
                    }).ToList(),
                    ExpiryItemDBs = new List<ItemDB>()
                };
            }
        }

        /// <summary>
        /// Handler for Item_SelectTicket protocol.
        /// Allows player to consume a ticket item to select a specific character/item.
        /// </summary>
        public class SelectTicket : BaseHandler<ItemSelectTicketRequest, ItemSelectTicketResponse>
        {
            private readonly BAContext _dbContext;

            public SelectTicket(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<ItemSelectTicketResponse> Handle(ItemSelectTicketRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                // Find the ticket item
                var ticketItem = await _dbContext.Items
                    .FirstOrDefaultAsync(i => i.AccountServerId == user.Id && i.ServerId == request.TicketItemServerId);

                if (ticketItem == null)
                    throw new Exception("Ticket item not found");

                // TODO: Implement ParcelHandler logic for consuming ticket and adding selected item
                // For now, stub with basic logic
                
                // Consume the ticket
                ticketItem.StackCount -= request.ConsumeCount;
                if (ticketItem.StackCount <= 0)
                {
                    _dbContext.Items.Remove(ticketItem);
                }
                else
                {
                    _dbContext.Items.Update(ticketItem);
                }

                // TODO: Add the selected item/character based on request.SelectItemUniqueId
                // This would normally use ParcelHandler to determine what type of parcel to add
                // For characters: Add to Characters table
                // For items: Add to Items table
                // For equipment: Add to Equipments table

                await _dbContext.SaveChangesAsync();

                // Return stub response
                return new ItemSelectTicketResponse
                {
                    ParcelResultDB = new ParcelResultDB
                    {
                        // TODO: Populate with actual parcel results
                        // AccountDB, ItemDBs, CharacterDBs, etc.
                    }
                };
            }
        }

        /// <summary>
        /// Handler for Item_AutoSynth protocol.
        /// Automatically synthesizes lower tier items/equipment into higher tier versions.
        /// Uses ParcelAutoSynthExcel data to determine conversion ratios.
        /// </summary>
        public class AutoSynth : BaseHandler<ItemAutoSynthRequest, ItemAutoSynthResponse>
        {
            private readonly BAContext _dbContext;

            public AutoSynth(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<ItemAutoSynthResponse> Handle(ItemAutoSynthRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                var itemDBs = await _dbContext.Items
                    .Where(i => i.AccountServerId == user.Id)
                    .ToListAsync();

                var equipmentDBs = await _dbContext.Equipments
                    .Where(e => e.AccountServerId == user.Id)
                    .ToListAsync();

                Dictionary<long, ItemDB> itemDBParcel = new();
                Dictionary<long, EquipmentDB> equipmentDBParcel = new();

                // TODO: Load ParcelAutoSynthExcel table to get conversion rules
                // For now, stub with hardcoded example logic
                
                foreach (var parcel in request.TargetParcels)
                {
                    // TODO: Look up parcelAutoSynthExcel data based on parcel.Type and parcel.Id
                    // var parcelData = parcelAutoSynthExcels.FirstOrDefault(x => x.RequireParcelType == parcel.Type && x.RequireParcelId == parcel.Id);
                    
                    // Example hardcoded values (replace with Excel lookup):
                    // RequireParcelType, RequireParcelId, SynthStartAmount, SynthEndAmount, RequireParcelAmount, ResultParcelId
                    
                    if (parcel.Key.Type == ParcelType.Item)
                    {
                        var itemParcel = itemDBs.FirstOrDefault(x => x.UniqueId == parcel.Key.Id);
                        if (itemParcel != null)
                        {
                            // TODO: Replace with actual Excel data
                            int synthStartAmount = 100; // Minimum stack to start synth
                            int synthEndAmount = 50;    // Stack remaining after synth
                            int requireParcelAmount = 10; // Items needed per synth
                            long resultParcelId = parcel.Key.Id + 1; // Next tier item ID

                            if (itemParcel.StackCount > synthStartAmount)
                            {
                                double totalItemRemoved = itemParcel.StackCount - synthEndAmount;
                                int totalSynthAdded = (int)Math.Floor(totalItemRemoved / requireParcelAmount);

                                // Reduce source item
                                var reducedItem = itemDBs.FirstOrDefault(x => x.UniqueId == parcel.Key.Id);
                                if (reducedItem != null)
                                {
                                    reducedItem.StackCount -= (int)totalItemRemoved;
                                    
                                    // Add or update result item
                                    var synthItem = itemDBs.FirstOrDefault(x => x.UniqueId == resultParcelId);
                                    if (synthItem == null)
                                    {
                                        synthItem = new Models.Item
                                        {
                                            AccountServerId = user.Id,
                                            UniqueId = resultParcelId,
                                            StackCount = totalSynthAdded,
                                            IsNew = true
                                        };
                                        _dbContext.Items.Add(synthItem);
                                    }
                                    else
                                    {
                                        synthItem.StackCount += totalSynthAdded;
                                    }

                                    itemDBParcel[reducedItem.UniqueId] = new ItemDB
                                    {
                                        ServerId = reducedItem.ServerId,
                                        UniqueId = reducedItem.UniqueId,
                                        StackCount = reducedItem.StackCount,
                                        IsNew = reducedItem.IsNew,
                                        IsLocked = reducedItem.IsLocked
                                    };

                                    itemDBParcel[synthItem.UniqueId] = new ItemDB
                                    {
                                        ServerId = synthItem.ServerId,
                                        UniqueId = synthItem.UniqueId,
                                        StackCount = synthItem.StackCount,
                                        IsNew = synthItem.IsNew,
                                        IsLocked = synthItem.IsLocked
                                    };
                                }
                            }
                        }
                    }
                    else if (parcel.Key.Type == ParcelType.Equipment)
                    {
                        var equipmentParcel = equipmentDBs.FirstOrDefault(x => x.UniqueId == parcel.Key.Id);
                        if (equipmentParcel != null)
                        {
                            // TODO: Replace with actual Excel data
                            int synthStartAmount = 100;
                            int synthEndAmount = 50;
                            int requireParcelAmount = 10;
                            long resultParcelId = parcel.Key.Id + 1;

                            if (equipmentParcel.StackCount > synthStartAmount)
                            {
                                double totalEquipmentRemoved = equipmentParcel.StackCount - synthEndAmount;
                                int totalSynthAdded = (int)Math.Floor(totalEquipmentRemoved / requireParcelAmount);

                                var reducedEq = equipmentDBs.FirstOrDefault(x => x.UniqueId == parcel.Key.Id);
                                if (reducedEq != null)
                                {
                                    reducedEq.StackCount -= (int)totalEquipmentRemoved;

                                    var synthEq = equipmentDBs.FirstOrDefault(x => x.UniqueId == resultParcelId);
                                    if (synthEq == null)
                                    {
                                        synthEq = new Models.Equipment
                                        {
                                            AccountServerId = user.Id,
                                            UniqueId = resultParcelId,
                                            StackCount = totalSynthAdded,
                                            Level = 1,
                                            Tier = 1,
                                            IsNew = true
                                        };
                                        _dbContext.Equipments.Add(synthEq);
                                    }
                                    else
                                    {
                                        synthEq.StackCount += totalSynthAdded;
                                    }

                                    equipmentDBParcel[reducedEq.UniqueId] = new EquipmentDB
                                    {
                                        ServerId = reducedEq.ServerId,
                                        UniqueId = reducedEq.UniqueId,
                                        Level = reducedEq.Level,
                                        Exp = reducedEq.Exp,
                                        Tier = reducedEq.Tier,
                                        StackCount = reducedEq.StackCount,
                                        IsNew = reducedEq.IsNew,
                                        IsLocked = reducedEq.IsLocked,
                                        BoundCharacterServerId = reducedEq.BoundCharacterServerId
                                    };

                                    equipmentDBParcel[synthEq.UniqueId] = new EquipmentDB
                                    {
                                        ServerId = synthEq.ServerId,
                                        UniqueId = synthEq.UniqueId,
                                        Level = synthEq.Level,
                                        Exp = synthEq.Exp,
                                        Tier = synthEq.Tier,
                                        StackCount = synthEq.StackCount,
                                        IsNew = synthEq.IsNew,
                                        IsLocked = synthEq.IsLocked,
                                        BoundCharacterServerId = synthEq.BoundCharacterServerId
                                    };
                                }
                            }
                        }
                    }
                }

                await _dbContext.SaveChangesAsync();

                // Get updated currency
                var currency = await _dbContext.AccountCurrencies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.AccountServerId == user.Id);

                return new ItemAutoSynthResponse
                {
                    ParcelResultDB = new ParcelResultDB
                    {
                        AccountDB = new AccountDB
                        {
                            ServerId = user.Id,
                            Nickname = user.Nickname,
                            Level = user.Level,
                            Exp = 0, // TODO: Add Exp to User model
                            Comment = "", // TODO: Add Comment to User model
                            CallName = user.CallName,
                            RepresentCharacterServerId = user.RepresentCharacterServerId,
                            // Add other AccountDB properties as needed
                        },
                        AccountCurrencyDB = currency != null ? new AccountCurrencyDB
                        {
                            AcademyLocationRankSum = currency.AcademyLocationRankSum,
                            // Deserialize currency dictionary
                            CurrencyDict = string.IsNullOrEmpty(currency.CurrencyDict)
                                ? new Dictionary<CurrencyTypes, long>()
                                : JsonSerializer.Deserialize<Dictionary<CurrencyTypes, long>>(currency.CurrencyDict) ?? new Dictionary<CurrencyTypes, long>()
                        } : new AccountCurrencyDB(),
                        ItemDBs = itemDBParcel,
                        EquipmentDBs = equipmentDBParcel
                    }
                };
            }
        }
    }
}
