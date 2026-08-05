using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Microsoft.EntityFrameworkCore;

namespace Shittim_Server.Services;

public class CafeManager
{
    private readonly ExcelTableService _excelTableService;
    private readonly ConsumeHandler _consumeHandler;
    private readonly ParcelHandler _parcelHandler;
    private readonly IMapper _mapper;

    private readonly List<ItemExcelT> _itemExcels;
    private readonly List<CharacterExcelT> _characterExcel;
    private readonly List<FurnitureExcelT> _furnitureExcel;
    private readonly List<FurnitureTemplateElementExcelT> _furnitureTemplateElementExcel;
    private readonly List<CafeProductionExcelT> _cafeProductionExcel;
    private readonly List<DefaultFurnitureExcelT> _defaultFurnitureExcel;

    private readonly List<FurnitureSubCategory> _nonInteriorSubCategories =
    [
        FurnitureSubCategory.Floor,
        FurnitureSubCategory.Wallpaper,
        FurnitureSubCategory.Background
    ];

    public CafeManager(ExcelTableService excelTableService, ParcelHandler parcelHandler, ConsumeHandler consumeHandler, IMapper mapper)
    {
        _excelTableService = excelTableService;
        _consumeHandler = consumeHandler;
        _parcelHandler = parcelHandler;
        _mapper = mapper;

        _itemExcels = excelTableService.GetTable<ItemExcelT>();
        _characterExcel = excelTableService.GetTable<CharacterExcelT>()
            .GetReleaseCharacters().ToList();
        _furnitureExcel = excelTableService.GetTable<FurnitureExcelT>();
        _furnitureTemplateElementExcel = excelTableService.GetTable<FurnitureTemplateElementExcelT>();
        _cafeProductionExcel = excelTableService.GetTable<CafeProductionExcelT>();
        _defaultFurnitureExcel = excelTableService.GetTable<DefaultFurnitureExcelT>();
    }

    public async Task UpdateCafeData(SchaleDataContext context, AccountDBServer account, CafeDBServer cafeDb)
    {
        cafeDb.LastUpdate = account.GameSettings.ServerDateTime();
        cafeDb.FurnitureDBs = context.Furnitures.GetCafeDeployedFurniture(account.ServerId, cafeDb.CafeDBId).ToList();
        cafeDb.ProductionDB.ComfortValue = cafeDb.FurnitureDBs
            .Sum(furniture => _furnitureExcel.FirstOrDefault(x => x.Id == furniture.UniqueId)?.ComfortBonus ?? 0);

        await context.SaveChangesAsync();
    }

    public async Task<CafeDBServer> CafeAck(
        SchaleDataContext context, AccountDBServer account, long cafeDBId)
    {
        var cafe = context.Cafes.GetCafeByCafeDBId(account.ServerId, cafeDBId);
        await RefreshVisitors(context, account, [cafe]);
        return cafe;
    }

    // visitors turn over on the 04:00/16:00 resets like live. the client mostly never asks for cafe data outside login sync, so this has to run wherever cafes go out on the wire.
    public async Task RefreshVisitors(SchaleDataContext context, AccountDBServer account, List<CafeDBServer> cafes)
    {
        var now = account.GameSettings.ServerDateTime();
        var boundary = now.Hour >= 16 ? now.Date.AddHours(16)
            : now.Hour >= 4 ? now.Date.AddHours(4)
            : now.Date.AddHours(-8);

        var stale = cafes.Where(x => x.VisitUpdateTime < boundary).ToList();
        if (stale.Count == 0)
            return;

        var characters = context.GetAccountCharacters(account.ServerId).ToList();
        foreach (var cafe in stale)
        {
            cafe.CafeVisitCharacterDBs = CafeService.CreateRandomVisitor(characters, _characterExcel);
            cafe.VisitUpdateTime = now;
            cafe.IsNew = true;
            context.Cafes.Update(cafe);
        }

        await context.SaveChangesAsync();
    }

    // Cafe_ApplyTemplate wrote deployed rows with CafeDBId 0 for a while, swallowing stacks out of inventory and leaving cafes without their background, wallpaper and floor. Rows like that are still sitting in databases, so put them back on login and reseed any interior slot that ended up empty.
    public async Task RepairFurniture(SchaleDataContext context, AccountDBServer account, List<CafeDBServer> cafes)
    {
        var cafeDbIds = cafes.Select(x => x.CafeDBId).ToHashSet();
        var orphans = context.GetAccountFurnitures(account.ServerId).AsEnumerable()
            .Where(x => x.ItemDeploySequence != 0 && !cafeDbIds.Contains(x.CafeDBId))
            .ToList();

        if (orphans.Count > 0)
        {
            context.Furnitures.RemoveRange(orphans);
            CafeService.AddToInventory(context, account.ServerId, orphans);
            await context.SaveChangesAsync();
        }

        foreach (var cafe in cafes)
        {
            var deployedSubCategories = context.Furnitures.GetCafeDeployedFurniture(account.ServerId, cafe.CafeDBId).AsEnumerable()
                .Select(x => _furnitureExcel.FirstOrDefault(e => e.Id == x.UniqueId)?.SubCategory)
                .ToHashSet();

            var reseeded = new List<FurnitureDBServer>();
            foreach (var subCategory in _nonInteriorSubCategories.Where(x => !deployedSubCategories.Contains(x)))
            {
                var defaultRow = _defaultFurnitureExcel.FirstOrDefault(x =>
                    _furnitureExcel.FirstOrDefault(e => e.Id == x.Id)?.SubCategory == subCategory);
                if (defaultRow == null)
                    continue;

                reseeded.Add(new FurnitureDBServer
                {
                    AccountServerId = account.ServerId,
                    CafeDBId = cafe.CafeDBId,
                    UniqueId = defaultRow.Id,
                    Location = defaultRow.Location,
                    PositionX = defaultRow.PositionX,
                    PositionY = defaultRow.PositionY,
                    Rotation = defaultRow.Rotation,
                    StackCount = 1
                });
            }

            if (reseeded.Count > 0)
            {
                context.Furnitures.AddRange(reseeded);
                await context.SaveChangesAsync();
                reseeded.ForEach(x => x.ItemDeploySequence = x.ServerId);
                await context.SaveChangesAsync();
            }
        }
    }

    public async Task<(CafeDBServer, FurnitureDBServer, FurnitureDBServer)> CafeDeployFurniture(
        SchaleDataContext context, AccountDBServer account, CafeDeployFurnitureRequest req)
    {
        var cafeDb = context.Cafes.GetCafeByCafeDBId(account.ServerId, req.CafeDBId);

        var inventoryFurniture = context.Furnitures.FirstOrDefault(x =>
            x.AccountServerId == account.ServerId && x.Location == FurnitureLocation.Inventory &&
            x.UniqueId == req.FurnitureDB.UniqueId && x.ItemDeploySequence == 0);
        inventoryFurniture.StackCount--;
        if (inventoryFurniture.StackCount <= 0)
            context.Furnitures.Remove(inventoryFurniture);

        var newFurniture = CafeService.DeployFurniture(req.CafeDBId, account.ServerId, req.FurnitureDB);
        var furnitureExcelData = _furnitureExcel.FirstOrDefault(x => x.Id == newFurniture.UniqueId);

        if (furnitureExcelData != null && furnitureExcelData.Category == FurnitureCategory.Interiors)
        {
            var removedFurniture = context.Furnitures
                .FirstOrDefault(x =>
                    x.AccountServerId == account.ServerId &&
                    x.CafeDBId == req.CafeDBId &&
                    _furnitureExcel.FirstOrDefault(e => e.Id == x.UniqueId).SubCategory == furnitureExcelData.SubCategory
                );
            if (removedFurniture != null)
                context.Furnitures.Remove(removedFurniture);
        }

        context.Furnitures.Add(newFurniture);
        await context.SaveChangesAsync();

        newFurniture.ItemDeploySequence = newFurniture.ServerId;

        await context.SaveChangesAsync();
        await UpdateCafeData(context, account, cafeDb);

        return (cafeDb, newFurniture, inventoryFurniture);
    }

    public async Task<(CafeDBServer, FurnitureDBServer)> CafeRelocateFurniture(
        SchaleDataContext context, AccountDBServer account, CafeRelocateFurnitureRequest req)
    {
        var cafeDb = context.Cafes.GetCafeByCafeDBId(account.ServerId, req.CafeDBId);

        var furniture = context.Furnitures.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.ItemDeploySequence == req.FurnitureDB.ServerId);
        furniture.PositionX = req.FurnitureDB.PositionX;
        furniture.PositionY = req.FurnitureDB.PositionY;
        furniture.Rotation = req.FurnitureDB.Rotation;
        context.Furnitures.Update(furniture);

        cafeDb.LastUpdate = account.GameSettings.ServerDateTime();
        await context.SaveChangesAsync();

        return (cafeDb, furniture);
    }

    public async Task<(CafeDBServer, List<FurnitureDBServer>)> CafeRemoveFurniture(
        SchaleDataContext context, AccountDBServer account, CafeRemoveFurnitureRequest req)
    {
        var cafeDb = context.Cafes.GetCafeByCafeDBId(account.ServerId, req.CafeDBId);

        var removedFurnitures = context.GetAccountFurnitures(account.ServerId)
            .Where(x => req.FurnitureServerIds.ToHashSet().Contains(x.ServerId))
            .ToList();
        if (removedFurnitures.Count > 0)
            context.Furnitures.RemoveRange(removedFurnitures);

        var inventoryFurnitures = CafeService.AddToInventory(context, account.ServerId, removedFurnitures);
        await context.SaveChangesAsync();
        await UpdateCafeData(context, account, cafeDb);

        var allFurnitures = inventoryFurnitures.Concat(removedFurnitures).ToList();

        return (cafeDb, allFurnitures);
    }

    public async Task<(CafeDBServer, List<FurnitureDBServer>)> CafeRemoveAllFurniture(
        SchaleDataContext context, AccountDBServer account, CafeRemoveAllFurnitureRequest req)
    {
        var cafeDb = context.Cafes.GetCafeByCafeDBId(account.ServerId, req.CafeDBId);

        var filteredFurniture = _furnitureExcel
            .Where(x => !_nonInteriorSubCategories.Contains(x.SubCategory))
            .Select(e => e.Id)
            .ToHashSet();

        var removedFurnitures = context.GetAccountFurnitures(account.ServerId)
            .Where(x =>
                x.CafeDBId == req.CafeDBId &&
                x.ItemDeploySequence != 0 &&
                filteredFurniture.Contains(x.UniqueId)
            ).ToList();

        if (removedFurnitures.Count > 0)
            context.RemoveRange(removedFurnitures);

        var inventoryFurnitures = CafeService.AddToInventory(context, account.ServerId, removedFurnitures);
        await context.SaveChangesAsync();

        var allFurnitures = context.GetAccountFurnitures(account.ServerId).ToList();
        await UpdateCafeData(context, account, cafeDb);

        return (cafeDb, allFurnitures);
    }

    public async Task<CafeDBServer> CafeSummonCharacter(
        SchaleDataContext context, AccountDBServer account, CafeSummonCharacterRequest req)
    {
        var cafeDb = context.Cafes.GetCafeByCafeDBId(account.ServerId, req.CafeDBId);
        var characterData = context.Characters.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.ServerId == req.CharacterServerId);

        // Summoning a student who is already in the cafe is an ordinary thing for the client to ask: they can already be there as a random visitor.
        // Dictionary.Add throws on that, and an unhandled handler exception becomes ErrorCode 500, which the client renders as "A request that cannot be processed has been received."
        // Upsert instead, the same shape CafeSummonCharacterTicketUse uses.
        if (characterData != null)
        {
            cafeDb.LastUpdate = account.GameSettings.ServerDateTime();
            if (!account.GameSettings.BypassCafeSummon)
                cafeDb.LastSummonDate = account.GameSettings.ServerDateTime();

            // A re-summon keeps the existing visit's LastInteractTime: interaction is once per visit, and resetting it here would let a summon be used to farm the favor grant.
            cafeDb.CafeVisitCharacterDBs.TryGetValue(characterData.UniqueId, out var existing);
            cafeDb.CafeVisitCharacterDBs[characterData.UniqueId] = new CafeDBServer.CafeCharacterDBServer
            {
                IsSummon = true,
                UniqueId = characterData.UniqueId,
                ServerId = characterData.ServerId,
                LastInteractTime = existing?.LastInteractTime ?? default
            };

            context.Cafes.Update(cafeDb);
            await context.SaveChangesAsync();
        }

        return cafeDb;
    }

    public async Task<(CafeDBServer CafeDb, List<CafeDBServer> CafeDbs, ParcelResultDB ParcelResultDb)> CafeSummonCharacterTicketUse(
        SchaleDataContext context,
        AccountDBServer account,
        CafeSummonCharacterTicketUseRequest req)
    {
        var consumeResult = await _consumeHandler.BuildConsumeResult(context, account, req.ConsumeRequestDB ?? new ConsumeRequestDB());

        var cafeDb = context.Cafes.GetCafeByCafeDBId(account.ServerId, req.CafeDBId);
        var characterData = context.Characters.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.ServerId == req.CharacterServerId);
        if (characterData != null)
        {
            cafeDb.LastUpdate = account.GameSettings.ServerDateTime();
            if (!account.GameSettings.BypassCafeSummon)
                cafeDb.LastSummonDate = account.GameSettings.ServerDateTime();

            cafeDb.CafeVisitCharacterDBs[characterData.UniqueId] = new CafeDBServer.CafeCharacterDBServer
            {
                IsSummon = true,
                UniqueId = characterData.UniqueId,
                ServerId = characterData.ServerId
            };

            context.Cafes.Update(cafeDb);
            await context.SaveChangesAsync();
        }

        var allCafes = context.GetAccountCafes(account.ServerId).ToList();
        return (cafeDb, allCafes, consumeResult.ParcelResult);
    }

    public async Task<(CafeDBServer, CharacterDB, ParcelResultDB)> CafeInteractWithCharacter(
        SchaleDataContext context, AccountDBServer account, CafeInteractWithCharacterRequest req)
    {
        var dateTime = account.GameSettings.ServerDateTime();
        var cafeDb = context.Cafes.GetCafeByCafeDBId(account.ServerId, req.CafeDBId);

        var visitEntry = cafeDb.CafeVisitCharacterDBs.Values.FirstOrDefault(x => x.UniqueId == req.CharacterId);

        // The client can send an interact for a student who is no longer in the cafe - a stale UI tap after the visit rotated, or a duplicate of an in-flight request.
        // Dereferencing null here surfaces as ErrorCode 500 ("A request that cannot be processed"); refuse it as a domain error so the client just declines the interaction.
        if (visitEntry == null)
            throw new WebAPIException(WebAPIErrorCode.CafeInteractionNotFound, $"Character {req.CharacterId} is not visiting cafe {req.CafeDBId}");

        visitEntry.LastInteractTime = dateTime;
        cafeDb.LastUpdate = dateTime;
        context.Cafes.Update(cafeDb);

        var parcelResult = new ParcelResult(ParcelType.FavorExp, visitEntry.UniqueId, 15);
        var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelResult);
        var character = parcelResolver.ParcelResult.CharacterDBs.FirstOrDefault();

        await context.SaveChangesAsync();
        return (cafeDb, character, parcelResolver.ParcelResult);
    }

    public async Task<(ConsumeResultDB, ParcelResultDB)> CafeGiveGift(
        SchaleDataContext context, AccountDBServer account, CafeGiveGiftRequest req)
    {
        var character = context.Characters.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == req.CharacterUniqueId);
        var consumeResultData = await _consumeHandler.BuildConsumeResult(context, account, req.ConsumeRequestDB);
        var favorParcel = new ParcelResult(ParcelType.FavorExp, character.UniqueId, consumeResultData.AccumulatedExp);
        var parcelResolver = await _parcelHandler.BuildParcel(context, account, favorParcel);

        return (consumeResultData.ConsumeResult, parcelResolver.ParcelResult);
    }

    public async Task<(List<CafeDBServer>, List<FurnitureDBServer>)> CafeApplyTemplate(
        SchaleDataContext context, AccountDBServer account, CafeApplyTemplateRequest req)
    {
        var cafeDb = context.Cafes.GetCafeByCafeDBId(account.ServerId, req.CafeDBId);

        var deployedFurnitures = context.GetAccountFurnitures(account.ServerId)
            .Where(x =>
                x.CafeDBId == req.CafeDBId &&
                x.ItemDeploySequence != 0 &&
                x.Location != FurnitureLocation.Inventory
            ).ToList();

        if (deployedFurnitures.Count > 0)
            context.Furnitures.RemoveRange(deployedFurnitures);

        CafeService.AddToInventory(context, account.ServerId, deployedFurnitures);
        await context.SaveChangesAsync();

        var furnitureData = _furnitureTemplateElementExcel
            .Where(x => x.FurnitureTemplateId == req.TemplateId)
            .Select(x => new FurnitureDBServer
            {
                CafeDBId = req.CafeDBId,
                UniqueId = x.FurnitureId,
                Location = x.Location,
                PositionX = x.PositionX,
                PositionY = x.PositionY,
                Rotation = x.Rotation,
                StackCount = 1
            })
            .ToList();

        context.AddFurnitures(account.ServerId, [.. furnitureData]);
        await context.SaveChangesAsync();

        foreach (var furniture in furnitureData)
        {
            furniture.ItemDeploySequence = furniture.ServerId;
        }
        // sequences have to hit the database before the stack lookup below - the fresh template rows sit at sequence 0 until then and the inventory query would eat them instead of the stacks
        await context.SaveChangesAsync();

        foreach (var furniture in furnitureData)
        {
            // the rows added above are already the deployed copies - only an owned stack needs consuming, anything the account lacks is simply granted
            var inventoryFurniture = context.Furnitures.FirstOrDefault(x =>
                x.AccountServerId == account.ServerId && x.UniqueId == furniture.UniqueId && x.ItemDeploySequence == 0);
            if (inventoryFurniture != null)
            {
                inventoryFurniture.StackCount--;
                if (inventoryFurniture.StackCount <= 0)
                    context.Furnitures.Remove(inventoryFurniture);
            }
        }
        await context.SaveChangesAsync();
        await UpdateCafeData(context, account, cafeDb);

        var allCafes = context.GetAccountCafes(account.ServerId).ToList();
        var allFurnitures = context.GetAccountFurnitures(account.ServerId).ToList();

        return (allCafes, allFurnitures);
    }

    public async Task<(CafeDBServer, List<CafeDBServer>, ParcelResultDB)> CafeReceiveCurrency(
        SchaleDataContext context,
        AccountDBServer account,
        long cafeDbId)
    {
        var now = account.GameSettings.ServerDateTime();
        var cafes = context.GetAccountCafes(account.ServerId).ToList();

        if (cafes.Count == 0)
            throw new InvalidOperationException($"No cafes found for account {account.ServerId}.");

        var targetCafe = cafeDbId > 0
            ? cafes.FirstOrDefault(x => x.CafeDBId == cafeDbId)
            : cafes.OrderBy(x => x.CafeId).FirstOrDefault();

        targetCafe ??= cafes.OrderBy(x => x.CafeId).First();

        // Make sure comfort is up-to-date before calculating production.
        foreach (var cafe in cafes)
        {
            await UpdateCafeData(context, account, cafe);
        }

        var parcelResults = new List<ParcelResult>();
        foreach (var cafe in cafes)
        {
            EnsureProductionParcels(cafe);

            if (cafe.ProductionDB?.ProductionParcelInfos == null || cafe.ProductionDB.ProductionParcelInfos.Count == 0)
                continue;

            var elapsedSeconds = Math.Max(0L, (long)(now - cafe.ProductionAppliedTime).TotalSeconds);
            if (elapsedSeconds <= 0)
                continue;

            var cafeRules = _cafeProductionExcel
                .Where(x => x.CafeId == cafe.CafeId && x.Rank <= cafe.CafeRank)
                .ToList();

            if (cafeRules.Count == 0)
                continue;

            foreach (var productionParcel in cafe.ProductionDB.ProductionParcelInfos)
            {
                if (productionParcel.Key == null)
                    continue;

                var rule = cafeRules
                    .Where(x => x.CafeProductionParcelType == productionParcel.Key.Type && x.CafeProductionParcelId == productionParcel.Key.Id)
                    .OrderByDescending(x => x.Rank)
                    .FirstOrDefault();

                if (rule == null)
                    continue;

                // Production scales with comfort and elapsed time.
                // Coefficients are table-driven and not hardcoded per user.
                var productionPerSecondRaw = (cafe.ProductionDB.ComfortValue * rule.ParcelProductionCoefficient) + rule.ParcelProductionCorrectionValue;
                var producedAmount = Math.Max(0L, (productionPerSecondRaw * elapsedSeconds) / 10000L);
                var storableAmount = Math.Min(rule.ParcelStorageMax, productionParcel.Amount + producedAmount);

                if (storableAmount > 0)
                {
                    parcelResults.Add(new ParcelResult(productionParcel.Key.Type, productionParcel.Key.Id, storableAmount));
                }

                productionParcel.Amount = 0;
            }

            cafe.ProductionAppliedTime = now;
            cafe.ProductionDB.AppliedDate = now;
        }

        var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelResults);
        await context.SaveChangesAsync();

        return (targetCafe, cafes, parcelResolver.ParcelResult);
    }

    private void EnsureProductionParcels(CafeDBServer cafe)
    {
        if (cafe.ProductionDB == null)
        {
            cafe.ProductionDB = new CafeProductionDBServer
            {
                CafeDBId = cafe.CafeDBId,
                AppliedDate = cafe.ProductionAppliedTime,
                ComfortValue = 0,
                ProductionParcelInfos = []
            };
        }

        var rules = _cafeProductionExcel
            .Where(x => x.CafeId == cafe.CafeId && x.Rank <= cafe.CafeRank)
            .GroupBy(x => new { x.CafeProductionParcelType, x.CafeProductionParcelId })
            .Select(x => x.Key)
            .ToList();

        if (rules.Count == 0)
            return;

        cafe.ProductionDB.ProductionParcelInfos ??= [];

        foreach (var rule in rules)
        {
            var exists = cafe.ProductionDB.ProductionParcelInfos.Any(x =>
                x.Key != null &&
                x.Key.Type == rule.CafeProductionParcelType &&
                x.Key.Id == rule.CafeProductionParcelId);

            if (!exists)
            {
                cafe.ProductionDB.ProductionParcelInfos.Add(new CafeProductionDBServer.CafeProductionParcelInfoServer
                {
                    Key = new ParcelKeyPair
                    {
                        Type = rule.CafeProductionParcelType,
                        Id = rule.CafeProductionParcelId
                    },
                    Amount = 0
                });
            }
        }
    }
}
