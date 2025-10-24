using BlueArchiveAPI.NetworkModels;
using BlueArchiveAPI.Services;
using Plana.FlatData;
using ParcelType = BlueArchiveAPI.NetworkModels.ParcelType;
using ShopCategoryType = BlueArchiveAPI.NetworkModels.ShopCategoryType;

namespace BlueArchiveAPI.Handlers
{
    public static class Shop
    {
        public class List : BaseHandler<ShopListRequest, ShopListResponse>
        {
            private readonly ExcelTableService _excelTableService;

            public List(ExcelTableService excelTableService)
            {
                _excelTableService = excelTableService;
            }

            protected override async Task<ShopListResponse> Handle(ShopListRequest request)
            {
                var shopExcels = _excelTableService.GetTable<ShopExcelT>();

                var shopInfos = new List<ShopInfoDB>();

                foreach (var categoryType in request.CategoryList)
                {
                    var shopProducts = new List<ShopProductDB>();

                    var products = shopExcels
                        .Where(s => (int)s.CategoryType == (int)categoryType)
                        .ToList();

                    foreach (var product in products)
                    {
                        shopProducts.Add(new ShopProductDB
                        {
                            ShopExcelId = product.Id,
                            Category = categoryType,
                            DisplayOrder = product.DisplayOrder,
                            PurchaseCountLimit = product.PurchaseCountLimit,
                            ProductType = ShopProductType.General
                        });
                    }

                    if (shopProducts.Count > 0)
                    {
                        shopInfos.Add(new ShopInfoDB
                        {
                            Category = categoryType,
                            ShopProductList = shopProducts
                        });
                    }
                }

                return new ShopListResponse
                {
                    ShopInfos = shopInfos,
                    ShopEligmaHistoryDBs = new List<ShopEligmaHistoryDB>()
                };
            }
        }

        public class BuyGacha2 : BaseHandler<ShopBuyGacha2Request, ShopBuyGacha2Response>
        {
            protected override async Task<ShopBuyGacha2Response> Handle(ShopBuyGacha2Request request)
            {
                var result = new List<GachaResult>();

                for (int i = 0; i < 10; ++i)
                {
                    var c = 26000 + i;
                    result.Add(new GachaResult
                    {
                        CharacterId = c,
                        Character = new CharacterDB
                        {
                            Type = ParcelType.Character,
                            ServerId = 89988413 + i,
                            UniqueId = c,
                            StarGrade = 3,
                            Level = 1,
                            FavorRank = 1,
                            PublicSkillLevel = 1,
                            ExSkillLevel = 1,
                            PassiveSkillLevel = 1,
                            ExtraPassiveSkillLevel = 1,
                            LeaderSkillLevel = 1,
                            IsNew = true,
                            IsLocked = true
                        }
                    }); ;
                }

                return new ShopBuyGacha2Response
                {
                    GemBonusRemain = 114514,
                    ConsumedItems = new List<ItemDB>(),
                    GachaResults = result,
                    AcquiredItems = new List<ItemDB>(),
                    ServerTimeTicks = DateTime.Now.Ticks,
                    MissionProgressDBs = new List<MissionProgressDB>()
                };
            }
        }
        
        public class BuyGacha3 : BaseHandler<ShopBuyGacha3Request, ShopBuyGacha3Response>
        {
            protected override async Task<ShopBuyGacha3Response> Handle(ShopBuyGacha3Request request)
            {
                var result = new List<GachaResult>();

                for (int i = 0; i < 10; ++i)
                {
                    var c = 20000 + i;
                    result.Add(new GachaResult
                    {
                        CharacterId = c,
                        Character = new CharacterDB
                        {
                            Type = ParcelType.Character,
                            ServerId = 89988413 + i,
                            UniqueId = c,
                            StarGrade = 3,
                            Level = 1,
                            FavorRank = 1,
                            PublicSkillLevel = 1,
                            ExSkillLevel = 1,
                            PassiveSkillLevel = 1,
                            ExtraPassiveSkillLevel = 1,
                            LeaderSkillLevel = 1,
                            IsNew = true,
                            IsLocked = true
                        }
                    }); ;
                }

                return new ShopBuyGacha3Response
                {
                    GemBonusRemain = 114514,
                    ConsumedItems = new List<ItemDB>(),
                    GachaResults = result,
                    AcquiredItems = new List<ItemDB>(),
                    ServerTimeTicks = DateTime.Now.Ticks,
                    MissionProgressDBs = new List<MissionProgressDB>()
                };
            }
        }

        public class BeforehandGachaGet : BaseHandler<ShopBeforehandGachaGetRequest, ShopBeforehandGachaGetResponse>
        {
            protected override async Task<ShopBeforehandGachaGetResponse> Handle(ShopBeforehandGachaGetRequest request)
            {
                return new ShopBeforehandGachaGetResponse
                {
                    AlreadyPicked = false
                };
            }
        }
    }
}
