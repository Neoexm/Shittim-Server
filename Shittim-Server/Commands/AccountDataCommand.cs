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
    [CommandHandler("accountdata", "Command to load account data from saved files", "/accountdata <list|load|export|help> <file_name>")]
    internal class AccountDataCommand : Command
    {
        public AccountDataCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^(list|load|export|help)$", "The operation to perform", ArgumentFlags.IgnoreCase)]
        public string Operation { get; set; } = string.Empty;

        [Argument(1, @"^.*$", "The file name of the packet json saved or Operation", ArgumentFlags.IgnoreCase)]
        public string DataFileName { get; set; } = string.Empty;

        public static string accountDataDir = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory), "AccountData");
        public static JsonSerializerOptions jsonOptions = new() { WriteIndented = true };
        public override async Task Execute()
        {
            if (!Directory.Exists(accountDataDir))
                Directory.CreateDirectory(accountDataDir);
            
            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            switch (Operation.ToLower())
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
                await connection.SendChatMessage($"Usage: /loaddata <file_name>");
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

            Dictionary<long, CharacterDBServer> oldToNewCharacterServerId = new();

            foreach (var character in accountLoginSyncData.CharacterListResponse.CharacterDBs)
            {
                oldToNewCharacterServerId.Add(character.ServerId, connection.Mapper.Map<CharacterDBServer>(character));
            }

            context.Characters.RemoveRange(context.Characters.Where(x => x.AccountServerId == connection.AccountServerId));
            var characterData = connection.Mapper.Map<List<CharacterDBServer>>(accountLoginSyncData.CharacterListResponse.CharacterDBs);
            var charactersAdded = context.AddCharacters(connection.AccountServerId, characterData.ToArray());
            await context.SaveChangesAsync();

            context.Weapons.RemoveRange(context.Weapons.Where(x => x.AccountServerId == connection.AccountServerId));

            foreach (var weapon in accountLoginSyncData.CharacterListResponse.WeaponDBs)
            {
                if (oldToNewCharacterServerId.ContainsKey(weapon.BoundCharacterServerId))
                {
                    weapon.BoundCharacterServerId = oldToNewCharacterServerId[weapon.BoundCharacterServerId].ServerId;
                }
            }

            var weaponData = connection.Mapper.Map<List<WeaponDBServer>>(accountLoginSyncData.CharacterListResponse.WeaponDBs);
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
            else
            {
                context.Items.RemoveRange(context.Items.Where(x => x.AccountServerId == connection.AccountServerId));
                context.AddItems(connection.AccountServerId, accountLoginSyncData.ItemListResponse.ItemDBs.ToArray());
            }
            await context.SaveChangesAsync();

            context.Gears.RemoveRange(context.Gears.Where(x => x.AccountServerId == connection.AccountServerId));

            foreach (var gear in accountLoginSyncData.CharacterGearListResponse.GearDBs)
            {
                if (oldToNewCharacterServerId.ContainsKey(gear.BoundCharacterServerId))
                {
                    gear.BoundCharacterServerId = oldToNewCharacterServerId[gear.BoundCharacterServerId].ServerId;
                }
            }

            var gearData = connection.Mapper.Map<List<GearDBServer>>(accountLoginSyncData.CharacterGearListResponse.GearDBs);
            context.AddGears(connection.AccountServerId, gearData.ToArray());
            await context.SaveChangesAsync();

            Dictionary<long, EquipmentDB> oldToNewEquipmentServerId = new Dictionary<long, EquipmentDB>();

            context.Equipments.RemoveRange(context.GetAccountEquipments(connection.AccountServerId));

            foreach (var equipment in accountLoginSyncData.EquipmentItemListResponse.EquipmentDBs)
            {
                oldToNewEquipmentServerId.Add(equipment.ServerId, equipment);
            }

            var equipmentData = connection.Mapper.Map<List<EquipmentDBServer>>(accountLoginSyncData.EquipmentItemListResponse.EquipmentDBs);
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
            context.AddMemoryLobbies(connection.AccountServerId, memoryLobbyData.ToArray());

            Dictionary<long, CafeDB> oldToNewCafeServerId = new Dictionary<long, CafeDB>();

            context.Cafes.RemoveRange(context.Cafes.Where(x => x.AccountServerId == connection.AccountServerId));

            foreach (var cafe in accountLoginSyncData.CafeGetInfoResponse.CafeDBs)
            {
                oldToNewCafeServerId.Add(cafe.CafeDBId, cafe);
            }

            var cafeData = connection.Mapper.Map<List<CafeDBServer>>(accountLoginSyncData.CafeGetInfoResponse.CafeDBs);
            context.AddCafes(connection.AccountServerId, cafeData.ToArray());
            await context.SaveChangesAsync();

            context.Furnitures.RemoveRange(context.Furnitures.Where(x => x.AccountServerId == connection.AccountServerId));

            foreach (var furniture in accountLoginSyncData.CafeGetInfoResponse.FurnitureDBs)
            {
                if (oldToNewCafeServerId.ContainsKey(furniture.CafeDBId))
                {
                    furniture.CafeDBId = oldToNewCafeServerId[furniture.CafeDBId].CafeDBId;
                }
            }

            var furnitureData = connection.Mapper.Map<List<FurnitureDBServer>>(accountLoginSyncData.CafeGetInfoResponse.FurnitureDBs);
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
            context.AddEchelons(connection.AccountServerId, echelonData.ToArray());

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
            await connection.SendChatMessage("/accountdata - Command to load or export account data");
            await connection.SendChatMessage("Usage: /accountdata <list|load|export|help> <file_name>");
        }
    }
}
