using System.Text.Json;
using Shittim.Services.Client;
using Shittim.Utils;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Serilog;

namespace Shittim.Commands
{
    [CommandHandler("accountdata", "Command to load account data from saved files", "!accountdata <list|load|export|help> <file_name>")]
    internal class AccountDataCommand : Command
    {
        public AccountDataCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^(list|load|export|help)$", "The operation to perform", ArgumentFlags.IgnoreCase)]
        public string Operation { get; set; } = string.Empty;

        // Optional so `list` and `help` can run; `load` and `export` check for it themselves.
        [Argument(1, @"^.*$", "The file name of the packet json saved or Operation",
                  ArgumentFlags.Optional | ArgumentFlags.IgnoreCase)]
        public string DataFileName { get; set; } = string.Empty;

        public static string accountDataDir = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "AccountData");
        public static JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
        public override async Task Execute()
        {
            if (!Directory.Exists(accountDataDir))
                Directory.CreateDirectory(accountDataDir);
            
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var operation = Operation.ToLower();
            if ((operation == "load" || operation == "export") && string.IsNullOrWhiteSpace(DataFileName))
            {
                await connection.SendChatMessage($"'{operation}' needs a file name.");
                await connection.SendChatMessage("Usage: !accountdata <list|load|export|help> <file_name>");
                return;
            }

            switch (operation)
            {
                case "list":
                    await ListData();
                    break;
                case "load":
                    await LoadData();
                    break;
                case "export":
                    await ExportData(context, account);
                    break;
                default:
                    await ShowHelp();
                    break;
            }
        }

        public async Task LoadData()
        {
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            string dataFilePath = Path.Combine(accountDataDir, DataFileName);

            if (!File.Exists(dataFilePath))
            {
                Log.Debug(dataFilePath);
                await connection.SendChatMessage($"File {DataFileName} was not found! Be sure to include \".json\".");
                await connection.SendChatMessage($"Usage: !loaddata <file_name>");
                throw new FileNotFoundException("File not found!");
            }

            var accountData = JsonSerializer.Deserialize<List<AccountData>>(File.ReadAllBytes(dataFilePath));
            var accountAuthData = JsonSerializer.Deserialize<ImportAccountAuthResponse>(accountData[1].Payload.GetRawText());
            var accountLoginSyncData = JsonSerializer.Deserialize<ImportAccountLoginSyncResponse>(accountData[3].Payload.GetRawText());

            account.Nickname = accountAuthData.AccountDB.Nickname;
            account.State = accountAuthData.AccountDB.State;
            account.Level = accountAuthData.AccountDB.Level;
            account.Exp = accountAuthData.AccountDB.Exp;
            account.RepresentCharacterServerId = accountAuthData.AccountDB.RepresentCharacterServerId;
            account.Comment = accountAuthData.AccountDB.Comment;
            account.CallName = accountAuthData.AccountDB.CallName;

            // Assigned field by field rather than mapped, so the row keeps its own ServerId and AccountServerId.
            var incomingCurrency = accountLoginSyncData.AccountCurrencySyncResponse?.AccountCurrencyDB;
            if (incomingCurrency?.CurrencyDict != null)
            {
                // Time-charged currencies recharge as (now - UpdateTime) / interval, so timestamps from a server whose clock is ahead of this one make the recharge subtract; restamping resumes it from the import instead.
                var chargedAt = account.GameSettings.ServerDateTime();
                var updateTimes = (incomingCurrency.UpdateTimeDict ?? [])
                    .ToDictionary(entry => entry.Key, _ => chargedAt);

                var currency = context.GetAccountCurrencies(connection.AccountServerId).FirstOrDefault();
                if (currency != null)
                {
                    currency.CurrencyDict = incomingCurrency.CurrencyDict;
                    currency.UpdateTimeDict = updateTimes;
                    currency.AccountLevel = incomingCurrency.AccountLevel;
                    currency.AcademyLocationRankSum = incomingCurrency.AcademyLocationRankSum;
                }
                else
                {
                    context.Currencies.Add(new AccountCurrencyDBServer
                    {
                        AccountServerId = connection.AccountServerId,
                        CurrencyDict = incomingCurrency.CurrencyDict,
                        UpdateTimeDict = updateTimes,
                        AccountLevel = incomingCurrency.AccountLevel,
                        AcademyLocationRankSum = incomingCurrency.AcademyLocationRankSum,
                    });
                }
                await context.SaveChangesAsync();
            }

            context.Characters.RemoveRange(context.Characters.Where(x => x.AccountServerId == connection.AccountServerId));

            // ServerId keys a table shared by every account, so the save's own ids collide on a second import; zero them and let the database assign. The map must hold the inserted instances, since everything pointing at a character reads its new id back out of here after the save.
            var sourceCharacters = accountLoginSyncData.CharacterListResponse.CharacterDBs.ToList();
            var characterData = connection.Mapper.Map<List<CharacterDBServer>>(sourceCharacters);
            Dictionary<long, CharacterDBServer> oldToNewCharacterServerId = new();
            for (var i = 0; i < sourceCharacters.Count; i++)
            {
                oldToNewCharacterServerId[sourceCharacters[i].ServerId] = characterData[i];
                characterData[i].ServerId = 0;
            }

            var charactersAdded = context.AddCharacters(connection.AccountServerId, characterData.ToArray());
            await context.SaveChangesAsync();

            if (oldToNewCharacterServerId.TryGetValue(account.RepresentCharacterServerId, out var represent))
                account.RepresentCharacterServerId = represent.ServerId;

            context.Weapons.RemoveRange(context.Weapons.Where(x => x.AccountServerId == connection.AccountServerId));
            // AddWeapons, AddGears, AddItems and AddEquipment pick insert vs merge with a DB query, which still returns rows only marked Deleted, so without flushing first they merge onto a doomed row and the incoming one is lost.
            await context.SaveChangesAsync();

            foreach (var weapon in accountLoginSyncData.CharacterListResponse.WeaponDBs)
            {
                if (oldToNewCharacterServerId.ContainsKey(weapon.BoundCharacterServerId))
                {
                    weapon.BoundCharacterServerId = oldToNewCharacterServerId[weapon.BoundCharacterServerId].ServerId;
                }
            }

            var weaponData = connection.Mapper.Map<List<WeaponDBServer>>(accountLoginSyncData.CharacterListResponse.WeaponDBs);
            weaponData.ForEach(x => x.ServerId = 0);
            context.AddWeapons(connection.AccountServerId, weaponData.ToArray());
            await context.SaveChangesAsync();

            if (accountLoginSyncData.ItemListResponse == null)
            {
                try
                {
                    var seperateItemListPacket = JsonSerializer.Deserialize<ImportItemListResponse>(accountData[5].Payload.GetRawText());
                    accountLoginSyncData.ItemListResponse = seperateItemListPacket;
                }
                catch (Exception ex)
                {
                    await connection.SendChatMessage("Could not find any packet associated with item data.");
                }
            }

            // Outside the if/else above so a list recovered from the separate packet is written too, not only one taken from the login bundle.
            if (accountLoginSyncData.ItemListResponse?.ItemDBs != null)
            {
                context.Items.RemoveRange(context.Items.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();
                accountLoginSyncData.ItemListResponse.ItemDBs.ForEach(x => x.ServerId = 0);
                context.AddItems(connection.AccountServerId, accountLoginSyncData.ItemListResponse.ItemDBs.ToArray());
            }
            await context.SaveChangesAsync();

            context.Gears.RemoveRange(context.Gears.Where(x => x.AccountServerId == connection.AccountServerId));
            await context.SaveChangesAsync();

            foreach (var gear in accountLoginSyncData.CharacterGearListResponse.GearDBs)
            {
                if (oldToNewCharacterServerId.ContainsKey(gear.BoundCharacterServerId))
                {
                    gear.BoundCharacterServerId = oldToNewCharacterServerId[gear.BoundCharacterServerId].ServerId;
                }
            }

            var gearData = connection.Mapper.Map<List<GearDBServer>>(accountLoginSyncData.CharacterGearListResponse.GearDBs);
            gearData.ForEach(x => x.ServerId = 0);
            context.AddGears(connection.AccountServerId, gearData.ToArray());
            await context.SaveChangesAsync();

            context.Equipments.RemoveRange(context.GetAccountEquipments(connection.AccountServerId));
            await context.SaveChangesAsync();

            // As with characters: zero the key, and keep the inserted instances so the characters below can find what their gear became.
            var sourceEquipment = accountLoginSyncData.EquipmentItemListResponse.EquipmentDBs.ToList();
            var equipmentData = connection.Mapper.Map<List<EquipmentDBServer>>(sourceEquipment);
            Dictionary<long, EquipmentDBServer> oldToNewEquipmentServerId = new();
            for (var i = 0; i < sourceEquipment.Count; i++)
            {
                oldToNewEquipmentServerId[sourceEquipment[i].ServerId] = equipmentData[i];
                equipmentData[i].ServerId = 0;
            }

            context.AddEquipment(connection.AccountServerId, equipmentData.ToArray());
            await context.SaveChangesAsync();

            foreach (var character in charactersAdded)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (oldToNewEquipmentServerId.ContainsKey(character.EquipmentServerIds[i]))
                    {
                        character.EquipmentServerIds[i] = oldToNewEquipmentServerId[character.EquipmentServerIds[i]].ServerId;
                    }
                }
            }

            context.MemoryLobbies.RemoveRange(context.MemoryLobbies.Where(x => x.AccountServerId == connection.AccountServerId));
            var memoryLobbyData = connection.Mapper.Map<List<MemoryLobbyDBServer>>(accountLoginSyncData.MemoryLobbyListResponse.MemoryLobbyDBs);
            memoryLobbyData.ForEach(x => x.ServerId = 0);
            context.AddMemoryLobbies(connection.AccountServerId, memoryLobbyData.ToArray());

            Dictionary<long, long> oldCafeDbIdToCafeId = new();

            context.Cafes.RemoveRange(context.Cafes.Where(x => x.AccountServerId == connection.AccountServerId));

            foreach (var cafe in accountLoginSyncData.CafeGetInfoResponse.CafeDBs)
            {
                if (!oldCafeDbIdToCafeId.ContainsKey(cafe.CafeDBId))
                    oldCafeDbIdToCafeId.Add(cafe.CafeDBId, cafe.CafeId);
            }

            var cafeData = connection.Mapper.Map<List<CafeDBServer>>(accountLoginSyncData.CafeGetInfoResponse.CafeDBs);
            cafeData.ForEach(x => x.CafeDBId = 0);
            context.AddCafes(connection.AccountServerId, cafeData.ToArray());
            await context.SaveChangesAsync();

            var cafeIdToNewCafeDbId = context.Cafes
                .Where(x => x.AccountServerId == connection.AccountServerId)
                .ToDictionary(x => x.CafeId, x => x.CafeDBId);

            var defaultCafeDbId = cafeIdToNewCafeDbId
                .OrderBy(x => x.Key)
                .Select(x => x.Value)
                .FirstOrDefault();

            context.Furnitures.RemoveRange(context.Furnitures.Where(x => x.AccountServerId == connection.AccountServerId));

            foreach (var furniture in accountLoginSyncData.CafeGetInfoResponse.FurnitureDBs)
            {
                if (oldCafeDbIdToCafeId.TryGetValue(furniture.CafeDBId, out var cafeId) &&
                    cafeIdToNewCafeDbId.TryGetValue(cafeId, out var newCafeDbId))
                {
                    furniture.CafeDBId = newCafeDbId;
                }
                else if (defaultCafeDbId > 0)
                {
                    furniture.CafeDBId = defaultCafeDbId;
                }
            }

            var furnitureData = connection.Mapper.Map<List<FurnitureDBServer>>(accountLoginSyncData.CafeGetInfoResponse.FurnitureDBs);
            furnitureData.ForEach(x => x.ServerId = 0);
            context.AddFurnitures(connection.AccountServerId, furnitureData.ToArray());
            await context.SaveChangesAsync();

            context.Echelons.RemoveRange(context.Echelons.Where(x => x.AccountServerId == connection.AccountServerId));

            foreach (var echelon in accountLoginSyncData.EchelonListResponse.EchelonDBs)
            {
                if (oldToNewCharacterServerId.ContainsKey(echelon.LeaderServerId))
                {
                    echelon.LeaderServerId = oldToNewCharacterServerId[echelon.LeaderServerId].ServerId;
                }

                for (int i = 0; i < echelon.MainSlotServerIds.Count; i++)
                {
                    long targetId = echelon.MainSlotServerIds[i];

                    if (oldToNewCharacterServerId.ContainsKey(targetId))
                    {
                        echelon.MainSlotServerIds[i] = oldToNewCharacterServerId[targetId].ServerId;
                    }

                }

                for (int i = 0; i < echelon.SupportSlotServerIds.Count; i++)
                {
                    long targetId = echelon.SupportSlotServerIds[i];

                    if (oldToNewCharacterServerId.ContainsKey(targetId))
                    {
                        echelon.SupportSlotServerIds[i] = oldToNewCharacterServerId[targetId].ServerId;
                    }
                }

                for (int i = 0; i < echelon.SkillCardMulliganCharacterIds.Count; i++)
                {
                    long targetId = echelon.SkillCardMulliganCharacterIds[i];

                    if (oldToNewCharacterServerId.ContainsKey(targetId))
                    {
                        echelon.SkillCardMulliganCharacterIds[i] = oldToNewCharacterServerId[targetId].ServerId;
                    }
                }
            }

            var echelonData = connection.Mapper.Map<List<EchelonDBServer>>(accountLoginSyncData.EchelonListResponse.EchelonDBs);
            echelonData.ForEach(x => x.ServerId = 0);
            context.AddEchelons(connection.AccountServerId, echelonData.ToArray());

            await context.SaveChangesAsync();

            // Without story and campaign progress the account is max level with every content gate still shut. These rows carry no cross-references, so re-owning them is enough.
            var scenarioList = accountLoginSyncData.ScenarioListResponse;
            if (scenarioList != null)
            {
                context.ScenarioHistories.RemoveRange(context.ScenarioHistories.Where(x => x.AccountServerId == connection.AccountServerId));
                context.ScenarioGroupHistories.RemoveRange(context.ScenarioGroupHistories.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var scenarioHistories = connection.Mapper.Map<List<ScenarioHistoryDBServer>>(scenarioList.ScenarioHistoryDBs ?? []);
                foreach (var row in scenarioHistories) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.ScenarioHistories.AddRange(scenarioHistories);

                var scenarioGroups = connection.Mapper.Map<List<ScenarioGroupHistoryDBServer>>(scenarioList.ScenarioGroupHistoryDBs ?? []);
                foreach (var row in scenarioGroups) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.ScenarioGroupHistories.AddRange(scenarioGroups);

                await context.SaveChangesAsync();
            }

            var campaignList = accountLoginSyncData.CampaignListResponse;
            if (campaignList != null)
            {
                context.CampaignStageHistories.RemoveRange(context.CampaignStageHistories.Where(x => x.AccountServerId == connection.AccountServerId));
                context.CampaignChapterClearRewardHistories.RemoveRange(context.CampaignChapterClearRewardHistories.Where(x => x.AccountServerId == connection.AccountServerId));
                context.StrategyObjectHistories.RemoveRange(context.StrategyObjectHistories.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var stageHistories = connection.Mapper.Map<List<CampaignStageHistoryDBServer>>(campaignList.StageHistoryDBs ?? []);
                foreach (var row in stageHistories) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.CampaignStageHistories.AddRange(stageHistories);

                var chapterRewards = connection.Mapper.Map<List<CampaignChapterClearRewardHistoryDBServer>>(campaignList.CampaignChapterClearRewardHistoryDBs ?? []);
                foreach (var row in chapterRewards) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.CampaignChapterClearRewardHistories.AddRange(chapterRewards);

                var strategyObjects = connection.Mapper.Map<List<StrategyObjectHistoryDBServer>>(campaignList.StrategyObjecthistoryDBs ?? []);
                foreach (var row in strategyObjects) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.StrategyObjectHistories.AddRange(strategyObjects);

                await context.SaveChangesAsync();
            }

            var costumeList = accountLoginSyncData.CharacterListResponse?.CostumeDBs;
            if (costumeList != null)
            {
                context.Costumes.RemoveRange(context.Costumes.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var costumes = connection.Mapper.Map<List<CostumeDBServer>>(costumeList);
                foreach (var row in costumes)
                {
                    if (oldToNewCharacterServerId.TryGetValue(row.BoundCharacterServerId, out var owner))
                        row.BoundCharacterServerId = owner.ServerId;
                    row.ServerId = 0;
                    row.AccountServerId = connection.AccountServerId;
                }
                context.Costumes.AddRange(costumes);
                await context.SaveChangesAsync();
            }

            var emblemList = accountLoginSyncData.AttachmentEmblemListResponse?.EmblemDBs;
            if (emblemList != null)
            {
                context.Emblems.RemoveRange(context.Emblems.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var emblems = connection.Mapper.Map<List<EmblemDBServer>>(emblemList);
                foreach (var row in emblems) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.Emblems.AddRange(emblems);
                await context.SaveChangesAsync();
            }

            var momotalkList = accountLoginSyncData.MomotalkOutlineResponse?.MomoTalkOutLineDBs;
            if (momotalkList != null)
            {
                context.MomoTalkOutLines.RemoveRange(context.MomoTalkOutLines.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var momotalks = connection.Mapper.Map<List<MomoTalkOutLineDBServer>>(momotalkList);
                foreach (var row in momotalks)
                {
                    // CharacterDBId is a character ServerId, so it moves with the roster.
                    if (oldToNewCharacterServerId.TryGetValue(row.CharacterDBId, out var owner))
                        row.CharacterDBId = owner.ServerId;
                    row.ServerId = 0;
                    row.AccountServerId = connection.AccountServerId;
                }
                context.MomoTalkOutLines.AddRange(momotalks);
                await context.SaveChangesAsync();
            }

            var eventPermanentList = accountLoginSyncData.EventContentPermanentListResponse?.PermanentDBs;
            if (eventPermanentList != null)
            {
                context.EventContentPermanents.RemoveRange(context.EventContentPermanents.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var permanents = connection.Mapper.Map<List<EventContentPermanentDBServer>>(eventPermanentList);
                foreach (var row in permanents) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.EventContentPermanents.AddRange(permanents);
                await context.SaveChangesAsync();
            }

            var stickerBook = accountLoginSyncData.StickerListResponse?.StickerBookDB;
            if (stickerBook != null)
            {
                context.StickerBooks.RemoveRange(context.StickerBooks.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var book = connection.Mapper.Map<StickerBookDBServer>(stickerBook);
                book.ServerId = 0;
                book.AccountServerId = connection.AccountServerId;
                context.StickerBooks.Add(book);
                await context.SaveChangesAsync();
            }

            var multiFloorRaids = accountLoginSyncData.MultiFloorRaidSyncResponse?.MultiFloorRaidDBs;
            if (multiFloorRaids != null)
            {
                context.MultiFloorRaids.RemoveRange(context.MultiFloorRaids.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var rows = connection.Mapper.Map<List<MultiFloorRaidDBServer>>(multiFloorRaids);
                foreach (var row in rows) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.MultiFloorRaids.AddRange(rows);
                await context.SaveChangesAsync();
            }

            var freeRecruits = accountLoginSyncData.ShopGachaRecruitListResponse?.ShopFreeRecruitHistoryDBs;
            if (freeRecruits != null)
            {
                context.ShopFreeRecruitHistories.RemoveRange(context.ShopFreeRecruitHistories.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var rows = connection.Mapper.Map<List<ShopFreeRecruitHistoryDBServer>>(freeRecruits);
                foreach (var row in rows) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.ShopFreeRecruitHistories.AddRange(rows);
                await context.SaveChangesAsync();
            }

            // CraftPresetSlotDBs from the same response have no server table.
            var craftInfos = accountLoginSyncData.CraftInfoListResponse?.CraftInfos;
            if (craftInfos != null)
            {
                context.CraftInfos.RemoveRange(context.CraftInfos.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var rows = connection.Mapper.Map<List<CraftInfoDBServer>>(craftInfos);
                foreach (var row in rows) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.CraftInfos.AddRange(rows);
                await context.SaveChangesAsync();
            }

            var idCardBackgrounds = accountLoginSyncData.IdCardBackgroundDBs;
            if (idCardBackgrounds != null)
            {
                context.IdCardBackgrounds.RemoveRange(context.IdCardBackgrounds.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var backgrounds = connection.Mapper.Map<List<IdCardBackgroundDBServer>>(idCardBackgrounds);
                foreach (var row in backgrounds) { row.ServerId = 0; row.AccountServerId = connection.AccountServerId; }
                context.IdCardBackgrounds.AddRange(backgrounds);
                await context.SaveChangesAsync();
            }

            var idCard = accountLoginSyncData.FriendIdCardDB;
            if (idCard != null)
            {
                // Settings live on the account as JSON, the same shape FriendHandler writes. FriendCode, Level and LastConnectTime identify the account on this server, so they are not copied.
                var card = account.ContentInfo.IdCard;
                card.Comment = idCard.Comment;
                card.RepresentCharacterUniqueId = idCard.RepresentCharacterUniqueId;
                card.RepresentCharacterCostumeId = idCard.RepresentCharacterCostumeId;
                card.CardBackgroundId = idCard.CardBackgroundId;
                card.SearchPermission = idCard.SearchPermission;
                card.AutoAcceptFriendRequest = idCard.AutoAcceptFriendRequest;
                card.ShowAccountLevel = idCard.ShowAccountLevel;
                card.ShowFriendCode = idCard.ShowFriendCode;
                card.ShowRaidRanking = idCard.ShowRaidRanking;
                card.ShowArenaRanking = idCard.ShowArenaRanking;
                card.ShowEliminateRaidRanking = idCard.ShowEliminateRaidRanking;
                card.ShowMultiFloorRaidClearedDifficulty = idCard.ShowMultiFloorRaidClearedDifficulty;

                context.Entry(account).Property(x => x.ContentInfo).IsModified = true;
                await context.SaveChangesAsync();
            }

            var attachment = accountLoginSyncData.AttachmentGetResponse?.AccountAttachmentDB;
            if (attachment != null)
            {
                context.AccountAttachments.RemoveRange(context.AccountAttachments.Where(x => x.AccountServerId == connection.AccountServerId));
                await context.SaveChangesAsync();

                var row = connection.Mapper.Map<AccountAttachmentDBServer>(attachment);
                row.ServerId = 0;
                row.AccountServerId = connection.AccountServerId;
                context.AccountAttachments.Add(row);
                await context.SaveChangesAsync();
            }

            await context.SaveChangesAsync();
            await connection.SendChatMessage("Successfully Loaded All Data from the save file.");
        }

        public async Task ExportData(SchaleDataContext context, AccountDBServer account)
        {
            var file = Path.Combine(accountDataDir, DataFileName);
            if (!file.EndsWith(".json"))
                file += ".json";
            var mapper = connection.Mapper;
            var accountAuth = new AccountAuthResponse()
            {
                AccountDB = account.ToMap(mapper),
            };
            var accountLogin = new AccountLoginSyncResponse()
            {
                CafeGetInfoResponse = new CafeGetInfoResponse()
                {
                    CafeDBs = context.GetAccountCafes(account.ServerId).ToMapList(mapper),
                    FurnitureDBs = context.GetAccountFurnitures(account.ServerId).ToMapList(mapper)
                },
                AccountCurrencySyncResponse = new AccountCurrencySyncResponse()
                {
                    AccountCurrencyDB = context.GetAccountCurrencies(account.ServerId).FirstOrDefaultMapTo(mapper)
                },
                CharacterListResponse = new CharacterListResponse()
                {
                    CharacterDBs = context.GetAccountCharacters(account.ServerId).ToMapList(mapper),
                    TSSCharacterDBs = [],
                    WeaponDBs = context.GetAccountWeapons(account.ServerId).ToMapList(mapper),
                    CostumeDBs = context.GetAccountCostumes(account.ServerId).ToMapList(mapper)
                },
                ItemListResponse = new ItemListResponse()
                {
                    ItemDBs = context.GetAccountItems(account.ServerId).ToMapList(mapper)
                },
                EquipmentItemListResponse = new EquipmentItemListResponse()
                {
                    EquipmentDBs = context.GetAccountEquipments(account.ServerId).ToMapList(mapper)
                },
                CharacterGearListResponse = new CharacterGearListResponse()
                {
                    GearDBs = context.GetAccountGears(account.ServerId).ToMapList(mapper)
                },
                EchelonListResponse = new EchelonListResponse()
                {
                    EchelonDBs = context.GetAccountEchelons(account.ServerId).ToMapList(mapper)
                },
                MemoryLobbyListResponse = new MemoryLobbyListResponse()
                {
                    MemoryLobbyDBs = context.GetAccountMemoryLobbies(account.ServerId).ToMapList(mapper),
                },
                CampaignListResponse = new CampaignListResponse()
                {
                    CampaignChapterClearRewardHistoryDBs = context.GetAccountCampaignChapterClearRewardHistories(account.ServerId).ToMapList(mapper),
                    StageHistoryDBs = context.GetAccountCampaignStageHistories(account.ServerId).ToMapList(mapper),
                    StrategyObjecthistoryDBs = context.GetAccountStrategyObjectHistories(account.ServerId).ToMapList(mapper)
                },
                MomotalkOutlineResponse = new MomoTalkOutLineResponse()
                {
                    MomoTalkOutLineDBs = context.GetAccountMomoTalkOutLines(account.ServerId).ToMapList(mapper),
                },
                ScenarioListResponse = new ScenarioListResponse()
                {
                    ScenarioHistoryDBs = context.GetAccountScenarioHistories(account.ServerId).ToMapList(mapper),
                    ScenarioGroupHistoryDBs = context.GetAccountScenarioGroupHistories(account.ServerId).ToMapList(mapper)
                },
                EventContentPermanentListResponse = new EventContentPermanentListResponse()
                {
                    PermanentDBs = context.GetAccountEventContentPermanents(account.ServerId).ToMapList(mapper)
                },
                AttachmentGetResponse = new AttachmentGetResponse()
                {
                    AccountAttachmentDB = context.AccountAttachments.FirstOrDefault(x => x.AccountServerId == account.ServerId).ToMap(mapper)
                },
                AttachmentEmblemListResponse = new AttachmentEmblemListResponse()
                {
                    EmblemDBs = context.GetAccountEmblems(account.ServerId).ToMapList(mapper)
                },
                StickerListResponse = new StickerLoginResponse()
                {
                    StickerBookDB = context.StickerBooks.FirstOrDefault(x => x.AccountServerId == account.ServerId).ToMap(mapper)
                }
            };

            List<AccountData> data = [
                new AccountData()
                {
                    Payload = JsonDocument.Parse("{}").RootElement,
                    Type = "REQUEST"
                },
                new AccountData()
                {
                    Payload = JsonSerializer.SerializeToElement(accountAuth),
                    Type = "RESPONSE"
                },
                new AccountData()
                {
                    Payload = JsonDocument.Parse("{}").RootElement,
                    Type = "REQUEST"
                },
                new AccountData()
                {
                    Payload = JsonSerializer.SerializeToElement(accountLogin),
                    Type = "RESPONSE"
                },
            ];

            await File.WriteAllTextAsync(file, JsonSerializer.Serialize(data, jsonOptions));
            await connection.SendChatMessage($"Successfully Exported Data to {file}");
        }

        public async Task ListData()
        {
            await connection.SendChatMessage("Data Files:");
            string[] files = Directory.GetFiles(accountDataDir);
            foreach (string file in files)
            {
                await connection.SendChatMessage(Path.GetFileName(file));
            }
        }

        public async Task ShowHelp()
        {
            await connection.SendChatMessage("!accountdata - Command to load or export account data");
            await connection.SendChatMessage("Usage: !accountdata <list|load|export|help> <file_name>");
        }
    }
}
