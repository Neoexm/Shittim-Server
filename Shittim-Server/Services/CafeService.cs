using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Microsoft.Extensions.Logging;

namespace BlueArchiveAPI.Services
{
    public class CafeService
    {
        private readonly ILogger<CafeService> _logger;
        private static readonly Random _rng = new();

        public CafeService(ILogger<CafeService> logger)
        {
            _logger = logger;
        }

        public static Dictionary<long, CafeDBServer.CafeCharacterDBServer> CreateRandomVisitor(
            List<CharacterDBServer> characters, List<CharacterExcelT> characterExcels)
        {
            var cafeVisitCharacterDBs = new Dictionary<long, CafeDBServer.CafeCharacterDBServer>();
            var existingCharactersLookup = characters.ToDictionary(c => c.UniqueId);
            var numberOfCharacters = Random.Shared.Next(3, 6);
            var randomCharacters = SelectRandomCharacters(characterExcels, numberOfCharacters);

            foreach (var character in randomCharacters)
            {
                existingCharactersLookup.TryGetValue(character.Id, out var existingCharacter);

                cafeVisitCharacterDBs.Add(
                    character.Id,
                    new CafeDBServer.CafeCharacterDBServer
                    {
                        IsSummon = false,
                        UniqueId = character.Id,
                        ServerId = existingCharacter?.ServerId ?? 0
                    }
                );
            }

            return cafeVisitCharacterDBs;
        }

        public static List<FurnitureDBServer> AddToInventory(
            SchaleDataContext context, long accountId, List<FurnitureDBServer> furnituresToPickup)
        {
            List<FurnitureDBServer> inventoryUpdates = new List<FurnitureDBServer>();

            foreach (var furniture in furnituresToPickup)
            {
                var furnitureToRemove = context.Furnitures.FirstOrDefault(x =>
                    x.AccountServerId == accountId && x.UniqueId == furniture.UniqueId && x.ItemDeploySequence != 0);

                if (furnitureToRemove != null)
                    context.Furnitures.Remove(furnitureToRemove);

                var inventoryFurniture = inventoryUpdates.FirstOrDefault(x => x.UniqueId == furniture.UniqueId) ??
                    context.Furnitures.FirstOrDefault(x => x.AccountServerId == accountId && x.UniqueId == furniture.UniqueId && x.ItemDeploySequence == 0);

                if (inventoryFurniture == null)
                {
                    inventoryFurniture = new FurnitureDBServer()
                    {
                        AccountServerId = accountId,
                        UniqueId = furniture.UniqueId,
                        Location = FurnitureLocation.Inventory,
                        PositionX = 0,
                        PositionY = 0,
                        Rotation = 0,
                        StackCount = 1,
                        ItemDeploySequence = 0
                    };
                    context.Furnitures.Add(inventoryFurniture);
                    inventoryUpdates.Add(inventoryFurniture);
                }
                else
                {
                    inventoryFurniture.StackCount++;
                    if (!inventoryUpdates.Contains(inventoryFurniture))
                        inventoryUpdates.Add(inventoryFurniture);
                }
            }
            return inventoryUpdates;
        }

        public static List<FurnitureDBServer> RemoveFromInventory(
            SchaleDataContext context, long accountId, List<FurnitureDBServer> furnituresToPlace)
        {
            List<FurnitureDBServer> inventoryUpdates = new List<FurnitureDBServer>();

            foreach (var furniture in furnituresToPlace)
            {
                var inventoryFurniture = context.Furnitures.FirstOrDefault(x =>
                    x.AccountServerId == accountId && x.UniqueId == furniture.UniqueId && x.ItemDeploySequence == 0);

                if (inventoryFurniture != null)
                {
                    inventoryFurniture.StackCount--;

                    if (inventoryFurniture.StackCount <= 0)
                        context.Furnitures.Remove(inventoryFurniture);
                    else
                    {
                        if (!inventoryUpdates.Contains(inventoryFurniture))
                            inventoryUpdates.Add(inventoryFurniture);
                    }
                }
                else
                {
                    continue;
                }

                var newDeployedFurniture = new FurnitureDBServer()
                {
                    AccountServerId = accountId,
                    UniqueId = furniture.UniqueId,
                    Location = furniture.Location,
                    PositionX = furniture.PositionX,
                    PositionY = furniture.PositionY,
                    Rotation = furniture.Rotation,
                    StackCount = 1,
                    ItemDeploySequence = furniture.ItemDeploySequence
                };
                context.Furnitures.Add(newDeployedFurniture);
                inventoryUpdates.Add(newDeployedFurniture);
            }

            return inventoryUpdates;
        }

        public static FurnitureDBServer DeployFurniture(long cafeDBId, long accountId, FurnitureDB furniture)
        {
            return new FurnitureDBServer()
            {
                AccountServerId = accountId,
                CafeDBId = cafeDBId,
                UniqueId = furniture.UniqueId,
                Location = furniture.Location,
                PositionX = furniture.PositionX,
                PositionY = furniture.PositionY,
                Rotation = furniture.Rotation,
                StackCount = 1,
            };
        }

        public Dictionary<long, CafeDBServer.CafeCharacterDBServer> GenerateRandomVisitors(
            List<CharacterDBServer> ownedCharacters,
            List<CharacterExcelT> characterData)
        {
            var visitors = new Dictionary<long, CafeDBServer.CafeCharacterDBServer>();
            
            var characterLookup = ownedCharacters.ToDictionary(c => c.UniqueId, c => c);
            
            var visitorCount = _rng.Next(3, 6);
            var selectedCharacters = SelectRandomCharacters(characterData, visitorCount);

            foreach (var charData in selectedCharacters)
            {
                var hasCharacter = characterLookup.TryGetValue(charData.Id, out var ownedChar);
                
                visitors[charData.Id] = new CafeDBServer.CafeCharacterDBServer
                {
                    UniqueId = charData.Id,
                    ServerId = hasCharacter ? ownedChar!.ServerId : 0,
                    IsSummon = false
                };
            }

            return visitors;
        }

        public List<FurnitureDBServer> MoveFurnitureToInventory(
            SchaleDataContext db,
            long accountId,
            List<FurnitureDBServer> furnitureToPickup)
        {
            var updatedItems = new List<FurnitureDBServer>();

            foreach (var item in furnitureToPickup)
            {
                var deployedItem = db.Furnitures
                    .FirstOrDefault(f => f.AccountServerId == accountId 
                                      && f.UniqueId == item.UniqueId 
                                      && f.ItemDeploySequence != 0);

                if (deployedItem != null)
                    db.Furnitures.Remove(deployedItem);

                var inventoryStack = updatedItems.FirstOrDefault(f => f.UniqueId == item.UniqueId)
                    ?? db.Furnitures.FirstOrDefault(f => f.AccountServerId == accountId 
                                                       && f.UniqueId == item.UniqueId 
                                                       && f.ItemDeploySequence == 0);

                if (inventoryStack == null)
                {
                    inventoryStack = CreateInventoryFurniture(accountId, item.UniqueId);
                    db.Furnitures.Add(inventoryStack);
                    updatedItems.Add(inventoryStack);
                }
                else
                {
                    inventoryStack.StackCount++;
                    if (!updatedItems.Contains(inventoryStack))
                        updatedItems.Add(inventoryStack);
                }
            }

            return updatedItems;
        }

        public List<FurnitureDBServer> DeployFurnitureFromInventory(
            SchaleDataContext db,
            long accountId,
            List<FurnitureDBServer> furnitureToDeploy)
        {
            var deployedItems = new List<FurnitureDBServer>();

            foreach (var deployRequest in furnitureToDeploy)
            {
                var inventoryItem = db.Furnitures
                    .FirstOrDefault(f => f.AccountServerId == accountId 
                                      && f.UniqueId == deployRequest.UniqueId 
                                      && f.ItemDeploySequence == 0);

                if (inventoryItem == null)
                {
                    _logger.LogWarning(
                        "Attempted to deploy furniture {FurnitureId} not in inventory for account {AccountId}",
                        deployRequest.UniqueId, accountId);
                    continue;
                }

                inventoryItem.StackCount--;
                if (inventoryItem.StackCount <= 0)
                {
                    db.Furnitures.Remove(inventoryItem);
                }
                else
                {
                    if (!deployedItems.Contains(inventoryItem))
                        deployedItems.Add(inventoryItem);
                }

                var deployedFurniture = new FurnitureDBServer
                {
                    AccountServerId = accountId,
                    UniqueId = deployRequest.UniqueId,
                    Location = deployRequest.Location,
                    PositionX = deployRequest.PositionX,
                    PositionY = deployRequest.PositionY,
                    Rotation = deployRequest.Rotation,
                    ItemDeploySequence = deployRequest.ItemDeploySequence,
                    StackCount = 1
                };

                db.Furnitures.Add(deployedFurniture);
                deployedItems.Add(deployedFurniture);
            }

            return deployedItems;
        }

        public FurnitureDBServer CreateDeployedFurniture(
            long cafeId,
            long accountId,
            FurnitureDB furnitureData)
        {
            return new FurnitureDBServer
            {
                AccountServerId = accountId,
                CafeDBId = cafeId,
                UniqueId = furnitureData.UniqueId,
                Location = furnitureData.Location,
                PositionX = furnitureData.PositionX,
                PositionY = furnitureData.PositionY,
                Rotation = furnitureData.Rotation,
                StackCount = 1
            };
        }

        private static FurnitureDBServer CreateInventoryFurniture(long accountId, long furnitureId)
        {
            return new FurnitureDBServer
            {
                AccountServerId = accountId,
                UniqueId = furnitureId,
                Location = FurnitureLocation.Inventory,
                PositionX = 0,
                PositionY = 0,
                Rotation = 0,
                ItemDeploySequence = 0,
                StackCount = 1
            };
        }

        private static List<T> SelectRandomCharacters<T>(List<T> source, int count)
        {
            if (source.Count <= count)
                return new List<T>(source);

            var result = new List<T>(count);
            var available = new List<T>(source);

            for (int i = 0; i < count && available.Count > 0; i++)
            {
                var index = Random.Shared.Next(available.Count);
                result.Add(available[index]);
                available.RemoveAt(index);
            }

            return result;
        }
    }
}
