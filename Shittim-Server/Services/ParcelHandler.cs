using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore.Storage;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Schale.Data.ModelMapping;
using BlueArchiveAPI.Services;

namespace Shittim_Server.Services;

public class ParcelHandler
{
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;

    private readonly List<ExpLevelData> _characterExpData;
    private readonly List<ExpLevelData> _favorExpData;
    private readonly List<ExpLevelData> _academyLocationExpData;
    private readonly List<ExpLevelData> _equipmentExpData;

    private readonly List<CharacterExcelT> _characterExcels;
    private readonly List<AccountLevelExcelT> _accountLevelExcels;
    private readonly List<GachaElementExcelT> _gachaElementExcels;
    private readonly List<CostumeExcelT> _costumeExcels;

    public ParcelHandler(ExcelTableService excelService, IMapper mapper)
    {
        _excelService = excelService;
        _mapper = mapper;

        var characterLevelExcel = _excelService.GetTable<CharacterLevelExcelT>();
        var favorLevelExcel = _excelService.GetTable<FavorLevelExcelT>();
        var academyLocationLevelExcel = _excelService.GetTable<AcademyLocationRankExcelT>();
        var equipmentLevelExcel = _excelService.GetTable<EquipmentLevelExcelT>();

        _characterExcels = _excelService.GetTable<CharacterExcelT>();
        _accountLevelExcels = _excelService.GetTable<AccountLevelExcelT>();
        _gachaElementExcels = _excelService.GetTable<GachaElementExcelT>();
        _costumeExcels = _excelService.GetTable<CostumeExcelT>();

        _characterExpData = new ExpLevelData().ConvertExpLevelData(characterLevelExcel);
        _favorExpData = new ExpLevelData().ConvertExpLevelData(favorLevelExcel);
        _academyLocationExpData = new ExpLevelData().ConvertExpLevelData(academyLocationLevelExcel);
        _equipmentExpData = new ExpLevelData().ConvertExpLevelData(equipmentLevelExcel);
    }

    public async Task<ParcelResolver> BuildParcel(
        SchaleDataContext context,
        AccountDBServer account,
        ParcelResult parcelResult,
        ParcelResultDB? parcelResultDB = null,
        bool isConsume = false)
        => await BuildParcel(context, account, [parcelResult], parcelResultDB, isConsume);

    public async Task<ParcelResolver> BuildParcel(
        SchaleDataContext context,
        AccountDBServer account,
        List<ParcelInfo> parcelInfoList,
        ParcelResultDB? parcelResultDB = null,
        bool isConsume = false)
    {
        var parcelResultList = ParcelResult.ConvertParcelResult(parcelInfoList);
        return await BuildParcel(context, account, parcelResultList, parcelResultDB, isConsume);
    }

    public async Task<ParcelResolver> BuildParcel(
        SchaleDataContext context,
        AccountDBServer account,
        List<ParcelResult> parcelResultList,
        ParcelResultDB? parcelResultDB = null,
        bool isConsume = false)
    {
        var parcelResolver = new ParcelResolver(context, account, _mapper, parcelResultDB, isConsume);
        if (parcelResultList.Count == 0)
            return parcelResolver;

        var filteredParcels = parcelResultList
            .GenerateGachaGroup(_gachaElementExcels)
            .Aggregate()
            .ConvertToConsume(isConsume);

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            foreach (var parcel in filteredParcels)
            {
                switch (parcel.Type)
                {
                    case ParcelType.Character:
                        await parcelResolver.UpdateCharacter(parcel, _characterExcels);
                        break;
                    case ParcelType.Currency:
                        await parcelResolver.UpdateAccountCurrency(parcel);
                        break;
                    case ParcelType.Equipment:
                        await parcelResolver.UpdateEquipment(parcel);
                        break;
                    case ParcelType.Item:
                        await parcelResolver.UpdateItem(parcel);
                        break;
                    case ParcelType.GachaGroup:
                        break;
                    case ParcelType.MemoryLobby:
                        await parcelResolver.UpdateMemoryLobby(parcel);
                        break;
                    case ParcelType.AccountExp:
                        await parcelResolver.UpdateAccountExp(parcel, _accountLevelExcels);
                        break;
                    case ParcelType.CharacterExp:
                        await parcelResolver.UpdateCharacterExp(parcel, _characterExpData);
                        break;
                    case ParcelType.FavorExp:
                        await parcelResolver.UpdateFavorCharacter(parcel, _favorExpData);
                        break;
                    case ParcelType.Furniture:
                        await parcelResolver.UpdateFurniture(parcel);
                        break;
                    case ParcelType.LocationExp:
                        await parcelResolver.UpdateLocationExp(parcel, _academyLocationExpData);
                        break;
                    case ParcelType.CharacterWeapon:
                        await parcelResolver.UpdateCharacterWeapon(parcel);
                        break;
                    case ParcelType.IdCardBackground:
                        await parcelResolver.UpdateIdCardBackground(parcel);
                        break;
                    case ParcelType.Emblem:
                        await parcelResolver.UpdateEmblem(parcel);
                        break;
                    case ParcelType.Sticker:
                        await parcelResolver.UpdateSticker(parcel);
                        break;
                    case ParcelType.Costume:
                        await parcelResolver.UpdateCostume(parcel, _costumeExcels);
                        break;
                    case ParcelType.CharacterGear:
                        await parcelResolver.UpdateCharacterGear(parcel);
                        break;
                    default:
                        break;
                }
            }

            await parcelResolver.FinalizeUpdates(transaction);

            if (!isConsume)
            {
                parcelResolver.ParcelResult.DisplaySequence = parcelResolver.ParcelInfos;
                parcelResolver.ParcelResult.ParcelForMission = parcelResolver.ParcelInfos;
            }

            return parcelResolver;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

public class ParcelResult
{
    public ParcelType Type { get; set; }
    public long Id { get; set; }
    public long Amount { get; set; }

    public ParcelResult() { }

    public ParcelResult(ParcelType type, long id, long amount)
    {
        Type = type;
        Id = id;
        Amount = amount;
    }

    public static List<ParcelResult> ConvertParcelResult(List<ParcelType> parcelTypes, List<long> parcelIds, List<long> parcelAmounts)
    {
        var parcelResultList = new List<ParcelResult>();
        for (int i = 0; i < parcelTypes.Count; i++)
        {
            parcelResultList.Add(new ParcelResult(parcelTypes[i], parcelIds[i], parcelAmounts[i]));
        }
        return parcelResultList;
    }

    public static List<ParcelResult> ConvertParcelResult(List<ParcelInfo> parcelInfos)
    {
        var parcelResultList = new List<ParcelResult>();
        foreach (var parcelInfo in parcelInfos)
        {
            parcelResultList.Add(new ParcelResult(parcelInfo.Key.Type, parcelInfo.Key.Id, parcelInfo.Amount));
        }
        return parcelResultList;
    }
}

public static class ParcelResultExtensions
{
    public static List<ParcelResult> Aggregate(this List<ParcelResult> parcelResultList)
    {
        var aggregatedParcels = parcelResultList
            .GroupBy(pr => new { pr.Type, pr.Id })
            .Select(group => new ParcelResult(group.Key.Type, group.Key.Id, group.Sum(pr => pr.Amount)))
            .ToList();

        return aggregatedParcels;
    }

    public static List<ParcelResult> GenerateGachaGroup(this List<ParcelResult> parcelResultList, List<GachaElementExcelT> gachaElementExcels)
    {
        var gachaParcels = parcelResultList.Where(pr => pr.Type == ParcelType.GachaGroup).ToList();
        parcelResultList.RemoveAll(pr => pr.Type == ParcelType.GachaGroup);

        var gachaGroupHandler = new GachaGroupHandler(gachaElementExcels);
        var generatedParcels = gachaGroupHandler.CreateGachaGroupParcel(gachaParcels);

        parcelResultList.AddRange(generatedParcels);

        return parcelResultList;
    }

    public static List<ParcelResult> ConvertToConsume(this List<ParcelResult> parcelResultList, bool isConsume)
    {
        return parcelResultList.Select(pr => new ParcelResult(pr.Type, pr.Id, pr.Amount * (isConsume ? -1 : 1))).ToList();
    }

    public static ParcelResult ConvertToAccountExp(this List<ParcelResult> parcelResultList)
    {
        var actionPointParcel = parcelResultList.FirstOrDefault(pr => pr.Type == ParcelType.Currency && pr.Id == (long)CurrencyTypes.ActionPoint);
        if (actionPointParcel == null) return new ParcelResult(ParcelType.AccountExp, 0, 0);
        actionPointParcel.Type = ParcelType.AccountExp;
        return actionPointParcel;
    }
}
