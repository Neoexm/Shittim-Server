using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Services;

public class ShopManager
{
    private readonly ExcelTableService _excelTableService;
    private readonly SharedDataCacheService _sharedDataCacheService;
    private readonly ParcelHandler _parcelHandler;
    private readonly IMapper _mapper;

    private readonly List<ItemExcelT> _itemExcels;
    private readonly List<PickupDuplicateBonusExcelT> _pickupDuplicateBonusExcels;
    private readonly List<ShopRecruitExcelT> _shopRecruitmentExcels;
    private readonly List<CharacterExcelT> _characterExcels;

    public ShopManager(
        ExcelTableService excelTableService,
        SharedDataCacheService sharedDataCacheService,
        ParcelHandler parcelHandler,
        IMapper mapper)
    {
        _excelTableService = excelTableService;
        _sharedDataCacheService = sharedDataCacheService;
        _parcelHandler = parcelHandler;
        _mapper = mapper;

        _itemExcels = _excelTableService.GetTable<ItemExcelT>();
        _pickupDuplicateBonusExcels = _excelTableService.GetTable<PickupDuplicateBonusExcelT>();
        _shopRecruitmentExcels = _excelTableService.GetTable<ShopRecruitExcelT>();
        _characterExcels = _excelTableService.GetTable<CharacterExcelT>()
            .GetReleaseCharacters().ToList();
    }

    public async Task<(AccountCurrencyDB, List<ItemDB>?, long)> ConsumeCurrency(
        SchaleDataContext context, AccountDBServer account, ShopBuyGacha3Request req)
    {
        long gachaAmount = 10;
        List<ItemDB> consumedItems = [];

        var parcelConsume = ParcelResult.ConvertParcelResult(req.Cost.ParcelInfos);
        foreach (var parcel in parcelConsume)
        {
            gachaAmount = parcel.Type switch
            {
                ParcelType.Currency => parcel.Amount / 120,
                ParcelType.Item => GachaService.GetGachaAmountByItem(_itemExcels, parcel.Id),
                _ => 10,
            };
        }
        var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelConsume, isConsume: true);
        if (parcelResolver.ParcelResult.ItemDBs.Count > 0)
            consumedItems.AddRange(parcelResolver.ParcelResult.ItemDBs.Values);

        return (parcelResolver.ParcelResult.AccountCurrencyDB, consumedItems, gachaAmount);
    }

    public async Task<(List<ItemDBServer>, List<GachaResult>)> CreateTenGacha(
        SchaleDataContext context, AccountDBServer account, ShopBuyGacha3Request req, long gachaAmount)
    {
        var dateTime = account.GameSettings.ServerDateTime();

        var ssrCharacterList = _sharedDataCacheService.CharaListSSRNormal;
        var srCharacterList = _sharedDataCacheService.CharaListSRNormal;
        var rCharacterList = _sharedDataCacheService.CharaListRNormal;
        var uniqCharacterList = _sharedDataCacheService.CharaListUnique;
        var festCharacterList = _sharedDataCacheService.CharaListFest;

        var characterBanner = _shopRecruitmentExcels.FirstOrDefault(x => x.Id == req.ShopUniqueId);
        if (characterBanner == null)
        {
            var availableIds = string.Join(", ", _shopRecruitmentExcels.Select(x => x.Id).OrderBy(x => x));
            Console.WriteLine($"[ShopManager] Shop ID {req.ShopUniqueId} not found. Available IDs: {availableIds}");
            
            characterBanner = _shopRecruitmentExcels.FirstOrDefault(x => x.CategoryType == ShopCategoryType.NormalGacha);
            if (characterBanner == null)
            {
                throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound, $"Shop recruitment with ID {req.ShopUniqueId} not found. Available shops: {availableIds}");
            }
            Console.WriteLine($"[ShopManager] Falling back to normal gacha banner ID: {characterBanner.Id}");
        }

        var rateUpPickUp = new PickupDuplicateBonusExcelT();
        var rateUpChar = new CharacterExcelT();
        var otherRateUpList = new List<CharacterExcelT>();

        if (characterBanner.CategoryType != ShopCategoryType.NormalGacha)
        {
            rateUpPickUp = _pickupDuplicateBonusExcels.GetPickupDuplicateBonusByShopId(req.ShopUniqueId);
            rateUpChar = _characterExcels.GetCharacter(rateUpPickUp.PickupCharacterId);
            var otherShopRecruitChars = _shopRecruitmentExcels
                .GetAssignedCharacterIdShopRecruit().GetAssignedSaleShopRecruit()
                .GetCategorizedShopRecruit(rateUpPickUp.ShopCategoryType).GetTimelinedShopRecruit(dateTime);
            var characterIds = otherShopRecruitChars.SelectMany(x => x.InfoCharacterId);
            foreach (long characterId in characterIds)
            {
                if (characterId == rateUpChar.Id) continue;
                var character = _characterExcels.GetCharacter(characterId);
                if (character != null) otherRateUpList.Add(character);
            }
        }

        List<ShopCategoryType> allowedGachaTypes =
        [
            ShopCategoryType.PickupGacha,
            ShopCategoryType.LimitedGacha
        ];
        const int guaranteedSRIndex = 9;
        List<GachaResult> gachaList = new((int)gachaAmount);
        List<ItemDBServer> itemList = [];
        bool shouldDoGuaranteedSR = true;
        bool hasSSR = false;

        var (rateUpSSRRate, fesSSRRate, limitedSSRRate, permanentSSRRate, SRRate, isSelector) = GachaService.InitializeGachaRates(characterBanner.CategoryType);

        var guaranteedCharIdOnce = Core.GachaCommand.GetGuaranteedCharacterId();
        bool guaranteedUsed = false;

        if (guaranteedCharIdOnce.HasValue)
        {
            Console.WriteLine($"[ShopManager] Guaranteed character active: {guaranteedCharIdOnce.Value}");
        }

        for (int i = 0; i < gachaAmount; i++)
        {
            if (guaranteedCharIdOnce.HasValue && !guaranteedUsed)
            {
                var guaranteedChar = _characterExcels.GetCharacter(guaranteedCharIdOnce.Value);
                if (guaranteedChar != null)
                {
                    Console.WriteLine($"[ShopManager] Granting guaranteed character {guaranteedCharIdOnce.Value} on pull {i + 1}");
                    await GachaService.AddGachaResult(context, account, _mapper, guaranteedChar, gachaList, itemList);
                    hasSSR = true;
                    guaranteedUsed = true;
                    continue;
                }
            }

            var randomNumber = Random.Shared.NextInt64(1000);
            var customRates = Core.GachaCommand.GetCustomRates();
            
            if (customRates.Count > 0)
            {
                double ssrThreshold = customRates.TryGetValue(3, out double ssrRate) ? ssrRate * 10 : 0;
                double srThreshold = ssrThreshold + (customRates.TryGetValue(2, out double srRate) ? srRate * 10 : 0);
                double rThreshold = srThreshold + (customRates.TryGetValue(1, out double rRate) ? rRate * 10 : 0);
                
                if (i == 0)
                {
                    Console.WriteLine($"[ShopManager] Using custom rates: SSR={ssrRate:F2}%, SR={srRate:F2}%, R={rRate:F2}%");
                }
                
                if (randomNumber < ssrThreshold)
                {
                    var currentSSRCharacterList = !characterBanner.CategoryType.Equals(ShopCategoryType.NormalGacha)
                        ? ssrCharacterList.Concat([rateUpChar]).Concat(otherRateUpList).DistinctBy(x => x.Id).ToList()
                        : ssrCharacterList;
                    await GachaService.AddGachaResult(context, account, _mapper, GachaService.GetRandomCharacterId(currentSSRCharacterList), gachaList, itemList);
                    hasSSR = true;
                }
                else if (randomNumber < srThreshold)
                {
                    await GachaService.AddGachaResult(context, account, _mapper, GachaService.GetRandomCharacterId(srCharacterList), gachaList, itemList);
                }
                else
                {
                    await GachaService.AddGachaResult(context, account, _mapper, GachaService.GetRandomCharacterId(rCharacterList), gachaList, itemList);
                }
                continue;
            }

            if (randomNumber < rateUpSSRRate && !characterBanner.CategoryType.Equals(ShopCategoryType.NormalGacha))
            {
                await GachaService.AddGachaResult(context, account, _mapper, rateUpChar, gachaList, itemList);
                hasSSR = true;
            }
            else if (randomNumber < rateUpSSRRate && allowedGachaTypes.Contains(characterBanner.CategoryType))
            {
                var currentFestCharacterList = festCharacterList.Concat(otherRateUpList).DistinctBy(x => x.Id).ToList();
                await GachaService.AddGachaResult(context, account, _mapper, GachaService.GetRandomCharacterId(currentFestCharacterList), gachaList, itemList);
                hasSSR = true;
            }
            else if (randomNumber < fesSSRRate && characterBanner.CategoryType.Equals(ShopCategoryType.FesGacha))
            {
                var currentFestCharacterList = festCharacterList.Concat(otherRateUpList).DistinctBy(x => x.Id).ToList();
                await GachaService.AddGachaResult(context, account, _mapper, GachaService.GetRandomCharacterId(currentFestCharacterList), gachaList, itemList);
                hasSSR = true;
            }
            else if (randomNumber < limitedSSRRate && limitedSSRRate > 0)
            {
                var currentUniqCharacterList = uniqCharacterList.Concat(otherRateUpList).DistinctBy(x => x.Id).ToList();
                await GachaService.AddGachaResult(context, account, _mapper, GachaService.GetRandomCharacterId(currentUniqCharacterList), gachaList, itemList);
                hasSSR = true;
            }
            else if (randomNumber < permanentSSRRate)
            {
                await GachaService.AddGachaResult(context, account, _mapper, GachaService.GetRandomCharacterId(ssrCharacterList, rateUpChar.Id, true), gachaList, itemList);
                hasSSR = true;
            }
            else if (randomNumber < SRRate || (i == guaranteedSRIndex && shouldDoGuaranteedSR))
                await GachaService.AddGachaResult(context, account, _mapper, GachaService.GetRandomCharacterId(srCharacterList), gachaList, itemList);
            else
                await GachaService.AddGachaResult(context, account, _mapper, GachaService.GetRandomCharacterId(rCharacterList), gachaList, itemList);
        }
        return (itemList, gachaList);
    }

    public async Task<List<ShopInfoDB>> GetShopList(AccountDBServer account, List<ShopExcelT> shopExcel, List<ShopCategoryType> reqCategoryList)
    {
        var dateTime = account.GameSettings.ServerDateTime();

        List<ShopInfoDB> CategoryList = [];
        foreach (ShopCategoryType shopCategoryType in reqCategoryList)
        {
            var ShopProductList = new List<ShopProductDB>();
            var nonSaleProduct = shopExcel.GetNonSaleShopExcel();
            var saleProduct = shopExcel.GetAssignedSaleShopExcel().GetTimelinedShopExcel(dateTime);
            var combinedProduct = nonSaleProduct.Concat(saleProduct).DistinctBy(x => x.Id).ToList();
            var products = combinedProduct.GetCategorizedShopExcel(shopCategoryType).ToList();

            foreach (var product in products)
            {
                ShopProductList.Add(new ShopProductDB
                {
                    ShopExcelId = product.Id,
                    Category = product.CategoryType,
                    DisplayOrder = product.DisplayOrder,
                    PurchaseCountLimit = product.PurchaseCountLimit,
                    ProductType = ShopProductType.General
                });
            }

            if (ShopProductList.Count != 0)
            {
                CategoryList.Add(new ShopInfoDB
                {
                    Category = shopCategoryType,
                    ShopProductList = ShopProductList
                });
            }
        }

        return CategoryList;
    }
}
