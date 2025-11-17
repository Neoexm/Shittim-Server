using Microsoft.EntityFrameworkCore;
using AutoMapper;
using Schale.FlatData;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Services;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Core.Math;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Services;
using Protocol = Schale.MX.NetworkProtocol.Protocol;
using Character = Schale.Data.GameModel.CharacterDBServer;

namespace BlueArchiveAPI.Services
{
    public class AccountService
    {
        public static async Task CreateAccount(
            SchaleDataContext context, 
            AccountDBServer account, 
            ExcelTableService excelTableService, 
            ParcelHandler parcelHandler)
        {
            AccountCurrencyDBServer accountCurrency = new(account.ServerId);
            context.Currencies.Add(accountCurrency);
            await context.SaveChangesAsync();

            var defaultParcels = excelTableService.GetTable<DefaultParcelExcelT>();
            List<ParcelResult> parcelList = [];
            foreach (var parcel in defaultParcels)
            {
                parcelList.Add(new ParcelResult(parcel.ParcelType, parcel.ParcelId, parcel.ParcelAmount));
            }
            await parcelHandler.BuildParcel(context, account, parcelList);

            var defaultCharacters = excelTableService.GetTable<DefaultCharacterExcelT>();
            var characterExcel = excelTableService.GetTable<CharacterExcelT>()
                .Where(x => x.IsPlayable && x.IsPlayableCharacter)
                .ToList();
            
            var newCharacters = defaultCharacters
                .Select(x =>
                {
                    var charData = characterExcel.FirstOrDefault(y => y.Id == x.CharacterId);

                    return new CharacterDBServer()
                    {
                        AccountServerId = account.ServerId,
                        UniqueId = x.CharacterId,
                        StarGrade = x.StarGrade,
                        Level = x.Level,
                        Exp = x.Exp,
                        FavorRank = x.FavorRank,
                        FavorExp = x.FavorExp,
                        PublicSkillLevel = x.CommonSkillLevel,
                        ExSkillLevel = x.ExSkillLevel,
                        PassiveSkillLevel = x.PassiveSkillLevel,
                        ExtraPassiveSkillLevel = x.ExtraPassiveSkillLevel,
                        LeaderSkillLevel = x.LeaderSkillLevel,
                        IsFavorite = x.FavoriteCharacter,
                        EquipmentServerIds = charData != null
                            ? charData.EquipmentSlot.Select(_ => 0L).ToList()
                            : [0, 0, 0],
                        PotentialStats = new Dictionary<int, int> { { 1, 0 }, { 2, 0 }, { 3, 0 } }
                    };
                })
                .ToList();

            context.Characters.AddRange(newCharacters);
            await context.SaveChangesAsync();

            var defaultEchelons = excelTableService.GetTable<DefaultEchelonExcelT>().First();
            var leaderChar = context.Characters.First(x => x.AccountServerId == account.ServerId && x.UniqueId == defaultEchelons.LeaderId);
            var mainChars = defaultEchelons.MainId.Select(id => 
                context.Characters.First(y => y.AccountServerId == account.ServerId && y.UniqueId == id).ServerId
            ).ToList();
            var supportChars = defaultEchelons.SupportId.Select(id => 
                context.Characters.First(y => y.AccountServerId == account.ServerId && y.UniqueId == id).ServerId
            ).ToList();

            while (mainChars.Count < 4) mainChars.Add(0);
            while (supportChars.Count < 2) supportChars.Add(0);

            var echelon = new EchelonDBServer()
            {
                AccountServerId = account.ServerId,
                EchelonType = (EchelonType)defaultEchelons.EchlonId,
                EchelonNumber = 1,
                LeaderServerId = leaderChar.ServerId,
                MainSlotServerIds = mainChars,
                SupportSlotServerIds = supportChars,
                SkillCardMulliganCharacterIds = [],
                CombatStyleIndex = Enumerable.Repeat(0, 6).ToArray(),
            };
            context.Echelons.Add(echelon);
            await context.SaveChangesAsync();

            for (int echelonPresetGroupIndex = 0; echelonPresetGroupIndex < 4; echelonPresetGroupIndex++)
            {
                Dictionary<int, EchelonPresetDBServer> echelonPresets = [];
                for (int echelonPresetIndex = 0; echelonPresetIndex < 5; echelonPresetIndex++)
                {
                    var echelonPreset = new EchelonPresetDBServer()
                    {
                        AccountServerId = account.ServerId,
                        GroupIndex = echelonPresetGroupIndex,
                        ExtensionType = EchelonExtensionType.Base,
                        Label = $"Preset {echelonPresetIndex + 1}",
                        Index = echelonPresetIndex,
                        StrikerUniqueIds = Enumerable.Repeat(0L, 4).ToArray(),
                        SpecialUniqueIds = Enumerable.Repeat(0L, 2).ToArray(),
                        CombatStyleIndex = Enumerable.Repeat(0, 6).ToArray(),
                        MulliganUniqueIds = [],
                    };
                    echelonPresets.Add(echelonPresetIndex, echelonPreset);
                    context.EchelonPresets.Add(echelonPreset);
                }
                var echelonGroup = new EchelonPresetGroupDBServer()
                {
                    AccountServerId = account.ServerId,
                    GroupIndex = echelonPresetGroupIndex,
                    ExtensionType = EchelonExtensionType.Base,
                    GroupLabel = $"Group {echelonPresetGroupIndex + 1}",
                    PresetDBs = echelonPresets
                };
                context.EchelonPresetGroups.Add(echelonGroup);
            }
            await context.SaveChangesAsync();

            for (int echelonPresetGroupIndex = 0; echelonPresetGroupIndex < 4; echelonPresetGroupIndex++)
            {
                Dictionary<int, EchelonPresetDBServer> echelonPresets = [];
                for (int echelonPresetIndex = 0; echelonPresetIndex < 5; echelonPresetIndex++)
                {
                    var echelonPreset = new EchelonPresetDBServer()
                    {
                        AccountServerId = account.ServerId,
                        GroupIndex = echelonPresetGroupIndex,
                        ExtensionType = EchelonExtensionType.Extension,
                        Label = $"Preset {echelonPresetIndex + 1}",
                        Index = echelonPresetIndex,
                        StrikerUniqueIds = Enumerable.Repeat(0L, 6).ToArray(),
                        SpecialUniqueIds = Enumerable.Repeat(0L, 4).ToArray(),
                        CombatStyleIndex = Enumerable.Repeat(0, 10).ToArray(),
                        MulliganUniqueIds = [],
                    };
                    echelonPresets.Add(echelonPresetIndex, echelonPreset);
                    context.EchelonPresets.Add(echelonPreset);
                }
                var echelonGroup = new EchelonPresetGroupDBServer()
                {
                    AccountServerId = account.ServerId,
                    GroupIndex = echelonPresetGroupIndex,
                    ExtensionType = EchelonExtensionType.Extension,
                    GroupLabel = $"Group {echelonPresetGroupIndex + 1}",
                    PresetDBs = echelonPresets
                };
                context.EchelonPresetGroups.Add(echelonGroup);
            }
            await context.SaveChangesAsync();

            context.Cafes.Add(new CafeDBServer(account, 1));
            context.Cafes.Add(new CafeDBServer(account, 2));
            await context.SaveChangesAsync();

            foreach (var cafeDB in context.Cafes.Where(x => x.AccountServerId == account.ServerId))
                cafeDB.ProductionDB.CafeDBId = cafeDB.CafeDBId;
            await context.SaveChangesAsync();

            var defaultFurnitureCafe = excelTableService.GetTable<DefaultFurnitureExcelT>();
            var cafeFurnitures = defaultFurnitureCafe.GetRange(0, Math.Min(3, defaultFurnitureCafe.Count)).Select((x) =>
            {
                return new FurnitureDBServer()
                {
                    AccountServerId = account.ServerId,
                    CafeDBId = context.Cafes.FirstOrDefault(c => c.AccountServerId == account.ServerId && c.CafeId == 1)!.CafeDBId,
                    UniqueId = x.Id,
                    Location = x.Location,
                    PositionX = x.PositionX,
                    PositionY = x.PositionY,
                    Rotation = x.Rotation,
                    StackCount = 1
                };
            }).ToList();
            var secondCafeFurnitures = defaultFurnitureCafe.GetRange(0, Math.Min(3, defaultFurnitureCafe.Count)).Select((x) =>
            {
                return new FurnitureDBServer()
                {
                    AccountServerId = account.ServerId,
                    CafeDBId = context.Cafes.FirstOrDefault(c => c.AccountServerId == account.ServerId && c.CafeId == 2)!.CafeDBId,
                    UniqueId = x.Id,
                    Location = x.Location,
                    PositionX = x.PositionX,
                    PositionY = x.PositionY,
                    Rotation = x.Rotation,
                    StackCount = 1
                };
            }).ToList();

            var combinedFurnitures = cafeFurnitures.Concat(secondCafeFurnitures).ToList();
            context.Furnitures.AddRange(combinedFurnitures);
            await context.SaveChangesAsync();

            var furniture = context.Furnitures.Where(x => combinedFurnitures.Select(f => f.ServerId).Contains(x.ServerId)).ToList();
            furniture.ForEach(x => x.ItemDeploySequence = x.ServerId);
            await context.SaveChangesAsync();

            foreach (var cafe in context.Cafes.Where(c => c.AccountServerId == account.ServerId))
            {
                var furn = furniture.Where(x => x.CafeDBId == cafe.CafeDBId);
                cafe.FurnitureDBs = furn.Select(x => new FurnitureDBServer()
                {
                    ServerId = x.ServerId,
                    CafeDBId = x.CafeDBId,
                    UniqueId = x.UniqueId,
                    Location = x.Location,
                    PositionX = x.PositionX,
                    PositionY = x.PositionY,
                    Rotation = x.Rotation,
                    StackCount = 1,
                }).ToList();
            }
            await context.SaveChangesAsync();

            var characterList = context.Characters.Where(c => c.AccountServerId == account.ServerId).ToList();
            foreach (var cafe in context.Cafes.Where(c => c.AccountServerId == account.ServerId))
            {
                Dictionary<long, CafeDBServer.CafeCharacterDBServer> CafeVisitCharacterDBs = CafeService.CreateRandomVisitor(characterList, characterExcel);
                cafe.CafeVisitCharacterDBs = CafeVisitCharacterDBs;
            }
            await context.SaveChangesAsync();

            var academyLocations = excelTableService.GetTable<AcademyLocationExcelT>();
            var academyZones = excelTableService.GetTable<AcademyZoneExcelT>();
            List<AcademyLocationDBServer> locations = academyLocations.Select(x =>
                new AcademyLocationDBServer()
                {
                    AccountServerId = account.ServerId,
                    LocationId = x.Id,
                    Rank = 12,
                    Exp = 0
                }
            ).ToList();
            var characterExcelIds = MathService.GetRandomList([.. characterExcel])
                .Select(x => x.Id).ToList();
            var academyDB = new AcademyDBServer()
            {
                AccountServerId = account.ServerId,
                LastUpdate = account.GameSettings.ServerDateTime(),
                ZoneVisitCharacterDBs = AcademyService.CreateRandomVisitor(characterList, academyZones, characterExcelIds),
                ZoneScheduleGroupRecords = []
            };
            context.AcademyLocations.AddRange(locations);
            context.Academies.Add(academyDB);
            await context.SaveChangesAsync();

            var favCharacter = defaultCharacters.Find(x => x.FavoriteCharacter);
            if (favCharacter != null)
            {
                account.RepresentCharacterServerId = (int)
                    newCharacters.First(x => x.UniqueId == favCharacter.CharacterId).ServerId;
            }
            await context.SaveChangesAsync();

            var attachment = new AccountAttachmentDBServer()
            {
                AccountServerId = account.ServerId,
                AccountId = account.ServerId,
                EmblemUniqueId = 0
            };
            context.AccountAttachments.Add(attachment);
            await context.SaveChangesAsync();

            var stickerBook = new StickerBookDBServer()
            {
                AccountServerId = account.ServerId,
                UnusedStickerDBs = [],
                UsedStickerDBs = []
            };
            context.StickerBooks.Add(stickerBook);
            await context.SaveChangesAsync();

            var eventContentExcels = excelTableService.GetTable<EventContentSeasonExcelT>()
                .Where(x => x.IsReturn == true)
                .Select(x => x.EventContentId)
                .ToHashSet();
            var eventPermanentDBs = eventContentExcels
                .Select(x => new EventContentPermanentDBServer()
                {
                    AccountServerId = account.ServerId,
                    EventContentId = x
                })
                .ToList();
            context.EventContentPermanents.AddRange(eventPermanentDBs);
            await context.SaveChangesAsync();

            var defaultMails = excelTableService.GetTable<DefaultMailExcelT>();
            var localizeExcel = excelTableService.GetTable<LocalizeExcelT>();

            foreach (var defaultMail in defaultMails)
            {
                List<ParcelInfo> parcelInfos = new();

                for (int i = 0; i < defaultMail.RewardParcelType.Count; i++)
                {
                    parcelInfos.Add(new()
                    {
                        Key = new()
                        {
                            Type = defaultMail.RewardParcelType[i],
                            Id = defaultMail.RewardParcelId[i]
                        },
                        Amount = defaultMail.RewardParcelAmount[i],
                        Multiplier = BasisPoint.One,
                        Probability = BasisPoint.One
                    });
                }

                context.Mails.Add(new()
                {
                    AccountServerId = account.ServerId,
                    Type = defaultMail.MailType,
                    UniqueId = defaultMail.Id,
                    Sender = "Shittim Server",
                    Comment = localizeExcel.Where(x => x.Key == defaultMail.LocalizeCodeId).FirstOrDefault()?.En ?? "Welcome!",
                    SendDate = DateTime.Parse(defaultMail.MailSendPeriodFrom),
                    ExpireDate = DateTime.Parse(defaultMail.MailSendPeriodTo),
                    ParcelInfos = parcelInfos,
                    RemainParcelInfos = new()
                });
            }

            await context.SaveChangesAsync();
        }
    }

    public static class AccountInitializationService
    {
        private static ExcelTableService? _excelService;
        private static ParcelHandler? _parcelHandler;

        public static void Initialize(ExcelTableService excelService, ParcelHandler parcelHandler)
        {
            _excelService = excelService;
            _parcelHandler = parcelHandler;
        }

        public static async Task InitializeCompleteAccount(SchaleDataContext context, AccountDBServer account)
        {
            if (_excelService == null || _parcelHandler == null)
                throw new InvalidOperationException("AccountInitializationService not initialized. Call Initialize() first.");
            
            await AccountService.CreateAccount(context, account, _excelService, _parcelHandler);
        }
    }
}
