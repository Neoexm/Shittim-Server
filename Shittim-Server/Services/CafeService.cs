using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using Plana.FlatData;
using System.Text.Json;

namespace BlueArchiveAPI.Services
{
    /// <summary>
    /// Service for café-related calculations (comfort, production, visitors)
    /// </summary>
    public class CafeService
    {
        private readonly ExcelTableService _excelService;
        
        public CafeService(ExcelTableService excelService)
        {
            _excelService = excelService;
        }
        
        /// <summary>
        /// Calculates total comfort value from placed furniture
        /// </summary>
        public long CalculateComfort(BAContext context, long cafeDBId)
        {
            try
            {
                var furnitureExcel = _excelService.GetTable<FurnitureExcelT>();
                var placedFurniture = context.Furnitures
                    .Where(f => f.CafeDBId == cafeDBId && f.Location != 1) // Location 1 is inventory
                    .ToList();
                
                long totalComfort = 0;
                foreach (var furniture in placedFurniture)
                {
                    var excelData = furnitureExcel.FirstOrDefault(x => x.Id == furniture.UniqueId);
                    if (excelData != null)
                    {
                        totalComfort += excelData.ComfortBonus;
                    }
                    else
                    {
                        Console.WriteLine($"[CafeService] WARNING: Furniture UniqueId {furniture.UniqueId} not found in FurnitureExcel");
                    }
                }
                
                Console.WriteLine($"[Cafe] Comfort {cafeDBId}={totalComfort} from {placedFurniture.Count} items");
                return totalComfort;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CafeService] ERROR calculating comfort: {ex.Message}. Fallback to 60.");
                return 60; // Fallback
            }
        }
        
        /// <summary>
        /// Calculates idle production (AP and Gold) based on time elapsed
        /// CafeProductionExcel has separate rows per ParcelType/Id (Gold vs AP)
        /// </summary>
        public List<CafeProductionParcelInfo> CalculateProduction(Cafe cafe, long comfortValue, DateTime serverTime)
        {
            try
            {
                var cafeProductionExcel = _excelService.GetTable<CafeProductionExcelT>();
                
                var elapsedMinutes = Math.Max(0, (serverTime - cafe.LastProductionCollectTime).TotalMinutes);
                
                // Get production data for this café rank
                // Excel has separate rows for Gold (ParcelId=1) and AP (ParcelId=5)
                var goldRow = cafeProductionExcel.FirstOrDefault(p =>
                    p.CafeId == cafe.CafeId && p.Rank == cafe.CafeRank &&
                    p.CafeProductionParcelType == Plana.FlatData.ParcelType.Currency && p.CafeProductionParcelId == 1);
                var apRow = cafeProductionExcel.FirstOrDefault(p =>
                    p.CafeId == cafe.CafeId && p.Rank == cafe.CafeRank &&
                    p.CafeProductionParcelType == Plana.FlatData.ParcelType.Currency && p.CafeProductionParcelId == 5);
                
                // Calculate production: (Coefficient * Comfort + CorrectionValue) / 10000 per minute
                var goldProduction = goldRow != null
                    ? (long)Math.Floor((goldRow.ParcelProductionCoefficient * comfortValue + goldRow.ParcelProductionCorrectionValue) / 10000.0 * elapsedMinutes)
                    : 0;
                var apProduction = apRow != null
                    ? (long)Math.Floor((apRow.ParcelProductionCoefficient * comfortValue + apRow.ParcelProductionCorrectionValue) / 10000.0 * elapsedMinutes)
                    : 0;
                
                Console.WriteLine($"[Cafe] Production {cafe.CafeDBId}: AP={apProduction} Gold={goldProduction} over {elapsedMinutes:F1}m");
                
                return new List<CafeProductionParcelInfo>
                {
                    new CafeProductionParcelInfo
                    {
                        Key = new ParcelKeyPair { Type = NetworkModels.ParcelType.Currency, Id = 5 },  // ActionPoint
                        Amount = apProduction
                    },
                    new CafeProductionParcelInfo
                    {
                        Key = new ParcelKeyPair { Type = NetworkModels.ParcelType.Currency, Id = 1 },  // Gold
                        Amount = goldProduction
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CafeService] ERROR calculating production: {ex.Message}. Fallback to 0.");
                return new List<CafeProductionParcelInfo>
                {
                    new CafeProductionParcelInfo { Key = new ParcelKeyPair { Type = NetworkModels.ParcelType.Currency, Id = 5 }, Amount = 0 },
                    new CafeProductionParcelInfo { Key = new ParcelKeyPair { Type = NetworkModels.ParcelType.Currency, Id = 1 }, Amount = 0 }
                };
            }
        }
        
        /// <summary>
        /// Selects café visitors using weighted random selection with stable seeding
        /// </summary>
        public List<long> SelectRandomVisitors(List<Character> availableCharacters, long accountId, long cafeId, DateTime serverDate)
        {
            try
            {
                if (availableCharacters.Count == 0)
                    return new List<long>();
                
                // Stable seed based on account, café, and day (changes daily)
                var daysSinceEpoch = (serverDate.Date - new DateTime(2020, 1, 1)).Days;
                var seed = (int)((accountId ^ cafeId ^ daysSinceEpoch) & 0x7FFFFFFF);
                var rng = new Random(seed);
                
                // Weighted selection (currently equal weights, can extend with Excel affinity data)
                var selectedCount = Math.Min(3, availableCharacters.Count);
                var selected = new List<long>();
                var available = new List<Character>(availableCharacters);
                
                for (int i = 0; i < selectedCount; i++)
                {
                    var index = rng.Next(available.Count);
                    selected.Add(available[index].UniqueId);
                    available.RemoveAt(index); // No duplicates
                }
                
                return selected;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CafeService] ERROR selecting visitors: {ex.Message}. Fallback to deterministic.");
                // Fallback to deterministic selection
                return availableCharacters.Take(3).Select(c => c.UniqueId).ToList();
            }
        }
    }
    
    public static class CafeServiceExtensions
    {
        public static void AddCafeService(this IServiceCollection services)
        {
            services.AddSingleton<CafeService>();
        }
    }
}