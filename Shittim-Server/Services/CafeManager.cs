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

        var currentDate = account.GameSettings.CurrentDateTime;
        var updateDateAfter = cafe.LastUpdate.AddHours(12);
        var updateDateBefore = cafe.LastUpdate.AddHours(-12);

        if (updateDateBefore < currentDate || updateDateAfter > currentDate)
        {
            var characters = context.GetAccountCharacters(account.ServerId).ToList();
            cafe.CafeVisitCharacterDBs = CafeService.CreateRandomVisitor(characters, _characterExcel);
            cafe.LastUpdate = currentDate.Date;
            cafe.IsNew = true;
            context.Cafes.Update(cafe);
            await context.SaveChangesAsync();
        }

        return cafe;
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

        cafeDb.LastUpdate = account.GameSettings.ServerDateTime();
        if (!account.GameSettings.BypassCafeSummon)
            cafeDb.LastSummonDate = account.GameSettings.ServerDateTime();

        cafeDb.CafeVisitCharacterDBs.Add(characterData.UniqueId,
        new CafeDBServer.CafeCharacterDBServer()
        {
            IsSummon = true,
            UniqueId = characterData.UniqueId,
            ServerId = characterData.ServerId
        }
        );
        await context.SaveChangesAsync();

        return cafeDb;
    }

    public async Task<(CafeDBServer, CharacterDB, ParcelResultDB)> CafeInteractWithCharacter(
        SchaleDataContext context, AccountDBServer account, CafeInteractWithCharacterRequest req)
    {
        var dateTime = account.GameSettings.ServerDateTime();
        var cafeDb = context.Cafes.GetCafeByCafeDBId(account.ServerId, req.CafeDBId);

        var visitEntry = cafeDb.CafeVisitCharacterDBs.Values.FirstOrDefault(x => x.UniqueId == req.CharacterId);

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
        CafeService.RemoveFromInventory(context, account.ServerId, furnitureData);
        await context.SaveChangesAsync();
        await UpdateCafeData(context, account, cafeDb);

        var allCafes = context.GetAccountCafes(account.ServerId).ToList();
        var allFurnitures = context.GetAccountFurnitures(account.ServerId).ToList();

        return (allCafes, allFurnitures);
    }
}
