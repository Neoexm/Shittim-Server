using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Shop
    {
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
    }
}
