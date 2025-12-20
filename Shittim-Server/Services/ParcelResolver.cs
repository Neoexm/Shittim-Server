using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.Core.Math;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Shittim_Server.Services;
using Shittim.Services;
using Serilog;

namespace Shittim_Server.Services;

public class ParcelResolver
{
    private SchaleDataContext Context { get; set; }
    private AccountDBServer Account { get; set; }
    private IMapper Mapper { get; set; }
    private bool IsConsume { get; set; }

    public ParcelResultDB ParcelResult { get; set; }
    public List<ParcelInfo> ParcelInfos { get; set; } = [];

    private Dictionary<long, CharacterDBServer> _updatedCharacters = new();
    private List<ItemDBServer> _newItems = new();
    private List<EquipmentDBServer> _newEquipments = new();
    private List<WeaponDBServer> _newWeapons = new();
    private List<CostumeDBServer> _newCostumes = new();
    private List<IdCardBackgroundDBServer> _newIdCardBackgrounds = new();
    private List<FurnitureDBServer> _newFurnitures = new();
    private List<MemoryLobbyDBServer> _newMemoryLobbies = new();
    private List<EmblemDBServer> _newEmblems = new();
    private List<StickerDBServer> _newStickers = new();

    public ParcelResolver(
        SchaleDataContext context,
        AccountDBServer account,
        IMapper mapper,
        ParcelResultDB? parcelResult,
        bool isConsume)
    {
        Context = context;
        Account = account;
        Mapper = mapper;
        IsConsume = isConsume;
        ParcelResult = parcelResult ?? new ParcelResultDB
        {
            AccountDB = account.ToMap(mapper),
            AccountCurrencyDB = context.Currencies.FirstMapTo(x => x.AccountServerId == account.ServerId, mapper),
            AcademyLocationDBs = new List<AcademyLocationDB>(),
            CharacterDBs = new List<CharacterDB>(),
            CostumeDBs = new List<CostumeDB>(),
            DisplaySequence = new List<ParcelInfo>(),
            EmblemDBs = new List<EmblemDB>(),
            EquipmentDBs = new Dictionary<long, EquipmentDB>(),
            FurnitureDBs = new Dictionary<long, FurnitureDB>(),
            GachaResultCharacters = new List<long>(),
            ItemDBs = new Dictionary<long, ItemDB>(),
            IdCardBackgroundDBs = new Dictionary<long, IdCardBackgroundDB>(),
            MemoryLobbyDBs = new List<MemoryLobbyDB>(),
            ParcelForMission = new List<ParcelInfo>(),
            ParcelResultStepInfoList = new List<ParcelResultStepInfo>(),
            RemovedItemIds = new List<long>(),
            RemovedEquipmentIds = new List<long>(),
            RemovedFurnitureIds = new List<long>(),
            StickerDBs = new List<StickerDB>(),
            SecretStoneCharacterIdAndCounts = new Dictionary<long, int>(),
            TSSCharacterDBs = new List<CharacterDB>(),
            WeaponDBs = new List<WeaponDB>(),

            CharacterNewUniqueIds = new List<long>()
        };
    }

    public async Task UpdateCharacter(ParcelResult parcel, List<CharacterExcelT> characterExcels)
    {
        if (IsConsume) return;
        var character = Context.Characters.FirstOrDefault(x => x.AccountServerId == Account.ServerId && x.UniqueId == parcel.Id);

        if (character != null)
        {
            var characterExcel = characterExcels.GetCharacter(character.UniqueId);
            await UpdateItem(new ParcelResult(ParcelType.Item, characterExcel.CharacterPieceItemId, characterExcel.CharacterPieceItemAmount));
            return;
        }

        character = new CharacterDBServer(Account.ServerId, parcel.Id);
        Context.Characters.Add(character);

        _updatedCharacters[character.UniqueId] = character;

        ParcelResult.CharacterNewUniqueIds.Add(character.UniqueId);
        CreateParcelInfo(parcel);
    }

    public async Task UpdateAccountCurrency(ParcelResult parcel, bool isAccountLevelUp = false)
    {
        var type = (CurrencyTypes)parcel.Id;
        var amount = parcel.Amount;

        var dateTime = Account.GameSettings.ServerDateTime();
        var accountCurrencyDB = Context.Currencies.FirstOrDefault(x => x.AccountServerId == Account.ServerId);

        if (accountCurrencyDB == null)
        {
            accountCurrencyDB = new AccountCurrencyDBServer(Account.ServerId);
            Context.Currencies.Add(accountCurrencyDB);
        }

        accountCurrencyDB.UpdateAccountLevel(Account.Level);

        if (isAccountLevelUp && type == CurrencyTypes.ActionPoint &&
            amount > accountCurrencyDB.CurrencyDict[CurrencyTypes.ActionPoint])
            return;

        if (IsConsume && type == CurrencyTypes.Gem)
            accountCurrencyDB.SubtractGem(amount, dateTime);

        accountCurrencyDB.UpdateCurrency(type, amount, dateTime);
        accountCurrencyDB.UpdateGem(dateTime);

        accountCurrencyDB.UpdateAcademyLocationRankSum(Context.GetAccountAcademyLocations(Account.ServerId).ToList());

        Context.Currencies.Update(accountCurrencyDB);
        CreateParcelInfo(parcel);
    }

    public async Task UpdateEquipment(ParcelResult parcel)
    {
        var equipment = Context.Equipments.FirstOrDefault(x =>
            x.AccountServerId == Account.ServerId &&
            x.UniqueId == parcel.Id &&
            x.BoundCharacterServerId == 0);

        if (equipment == null && parcel.Amount > 0)
        {
            equipment = new EquipmentDBServer()
            {
                AccountServerId = Account.ServerId,
                UniqueId = parcel.Id,
                StackCount = parcel.Amount
            };
            Context.Equipments.Add(equipment);
            _newEquipments.Add(equipment);
            return;
        }
        else if (equipment != null)
        {
            if ((equipment.StackCount + parcel.Amount) <= 0)
            {
                Context.Equipments.Remove(equipment);
                ParcelResult.RemovedEquipmentIds.Add(equipment.ServerId);
            }
            else
                equipment.StackCount += parcel.Amount;

            if (ParcelResult.EquipmentDBs.TryGetValue(equipment.ServerId, out EquipmentDB? value))
                value.StackCount = equipment.StackCount;
            else
                ParcelResult.EquipmentDBs.Add(equipment.ServerId, equipment.ToMap(Mapper));
        }
        CreateParcelInfo(parcel);
    }

    public async Task UpdateItem(ParcelResult parcel)
    {
        var item = Context.Items.FirstOrDefault(x =>
            x.AccountServerId == Account.ServerId &&
            x.UniqueId == parcel.Id);

        if (item == null && parcel.Amount > 0)
        {
            item = new ItemDBServer()
            {
                AccountServerId = Account.ServerId,
                UniqueId = parcel.Id,
                StackCount = parcel.Amount
            };
            Context.Items.Add(item);
            _newItems.Add(item);
            return;
        }
        else if (item != null)
        {
            if ((item.StackCount + parcel.Amount) <= 0)
            {
                Context.Items.Remove(item);
                ParcelResult.RemovedItemIds.Add(item.ServerId);
            }
            else
                item.StackCount += parcel.Amount;

            if (ParcelResult.ItemDBs.TryGetValue(item.ServerId, out ItemDB? value))
                value.StackCount = item.StackCount;
            else
                ParcelResult.ItemDBs.Add(item.ServerId, item.ToMap(Mapper));
        }
        CreateParcelInfo(parcel);
    }

    public async Task UpdateMemoryLobby(ParcelResult parcel)
    {
        if (IsConsume) return;
        var memoryLobby = Context.MemoryLobbies.FirstOrDefault(x =>
            x.AccountServerId == Account.ServerId &&
            x.MemoryLobbyUniqueId == parcel.Id);

        if (memoryLobby != null) return;

        var memoryLobbyDB = new MemoryLobbyDBServer()
        {
            AccountServerId = Account.ServerId,
            MemoryLobbyUniqueId = parcel.Id
        };

        Context.MemoryLobbies.Add(memoryLobbyDB);
        _newMemoryLobbies.Add(memoryLobbyDB);
        CreateParcelInfo(parcel);
    }

    public async Task UpdateAccountExp(ParcelResult parcel, List<AccountLevelExcelT> accountLevelExcels)
    {
        if (parcel.Amount <= 0) return;

        var (level, exp, newbieExp, actionPointChargeMax) = MathService.CalculateAccountExp(Account.Level, Account.Exp, parcel.Amount, accountLevelExcels);
        if (Account.Level != level && actionPointChargeMax > 0)
            await UpdateAccountCurrency(new ParcelResult(ParcelType.Currency, (long)CurrencyTypes.ActionPoint, actionPointChargeMax));

        Account.Level = level;
        Account.Exp = exp;

        Context.Accounts.Update(Account);

        bool isLevelMaxed = Account.Level == accountLevelExcels.Count;

        ParcelResult.BaseAccountExp += isLevelMaxed ? 0 : parcel.Amount;
        ParcelResult.AdditionalAccountExp += isLevelMaxed ? 0 : newbieExp;
        ParcelResult.NewbieBoostAccountExp += isLevelMaxed ? 0 : newbieExp;
    }

    public async Task UpdateCharacterExp(ParcelResult parcel, List<ExpLevelData> expLevelDatas)
    {
        if (IsConsume) return;
        var character = Context.Characters.FirstOrDefault(x =>
            x.AccountServerId == Account.ServerId &&
            x.UniqueId == parcel.Id);

        if (character == null)
        {
            Log.Warning("Failed to update character exp {characterUniqueId}", parcel.Id);
            return;
        }

        var (level, exp) = MathService.CalculateLevelExp(character.Level, character.Exp, parcel.Amount, expLevelDatas);
        character.Level = level;
        character.Exp = exp;

        Context.Characters.Update(character);
        _updatedCharacters[character.UniqueId] = character;
    }

    public async Task UpdateFavorCharacter(ParcelResult parcel, List<ExpLevelData> expLevelDatas)
    {
        var character = Context.Characters.FirstOrDefault(x =>
            x.AccountServerId == Account.ServerId &&
            x.UniqueId == parcel.Id);

        if (character == null)
        {
            Log.Warning("Failed to update favor character {characterUniqueId}", parcel.Id);
            return;
        }

        var (level, exp) = MathService.CalculateLevelExp(character.FavorRank, character.FavorExp, parcel.Amount, expLevelDatas);
        character.FavorRank = level;
        character.FavorExp = exp;

        Context.Characters.Update(character);
        _updatedCharacters[character.UniqueId] = character;
    }

    public async Task UpdateFurniture(ParcelResult parcel)
    {
        var furniture = Context.Furnitures.FirstOrDefault(x =>
            x.AccountServerId == Account.ServerId &&
            x.UniqueId == parcel.Id &&
            x.Location == FurnitureLocation.Inventory);

        if (furniture == null && parcel.Amount > 0)
        {
            furniture = new FurnitureDBServer()
            {
                AccountServerId = Account.ServerId,
                UniqueId = parcel.Id,
                StackCount = parcel.Amount
            };
            Context.Furnitures.Add(furniture);
            _newFurnitures.Add(furniture);
            return;
        }
        else if (furniture != null)
        {
            if ((furniture.StackCount + parcel.Amount) <= 0)
            {
                Context.Furnitures.Remove(furniture);
                ParcelResult.RemovedFurnitureIds.Add(furniture.ServerId);
            }
            else
                furniture.StackCount += parcel.Amount;

            if (ParcelResult.FurnitureDBs.TryGetValue(furniture.ServerId, out FurnitureDB? value))
                value.StackCount = furniture.StackCount;
            else
                ParcelResult.FurnitureDBs.Add(furniture.ServerId, furniture.ToMap(Mapper));
        }
        CreateParcelInfo(parcel);
    }

    public async Task UpdateLocationExp(ParcelResult parcel, List<ExpLevelData> expLevelDatas)
    {
        if (IsConsume) return;
        var academyLocation = Context.AcademyLocations.FirstOrDefault(x =>
            x.AccountServerId == Account.ServerId &&
            x.LocationId == parcel.Id);

        if (academyLocation == null)
        {
            Log.Warning("Failed to update location exp {locationUniqueId}", parcel.Id);
            return;
        }

        var (level, exp) = MathService.CalculateLevelExp((int)academyLocation.Rank, academyLocation.Exp, parcel.Amount, expLevelDatas);
        academyLocation.Rank = level;
        academyLocation.Exp = exp;

        Context.AcademyLocations.Update(academyLocation);
        ParcelResult.AcademyLocationDBs.Add(academyLocation.ToMap(Mapper));
    }

    public async Task UpdateCharacterWeapon(ParcelResult parcel)
    {
        if (IsConsume) return;
        var character = Context.Characters.FirstOrDefault(x => x.AccountServerId == Account.ServerId && x.UniqueId == parcel.Id);
        var weapon = Context.Weapons.FirstOrDefault(x => x.AccountServerId == Account.ServerId && x.UniqueId == parcel.Id);

        if (character == null) return;
        if (weapon != null) return;

        weapon = new WeaponDBServer()
        {
            AccountServerId = Account.ServerId,
            UniqueId = parcel.Id,
            BoundCharacterServerId = character.ServerId
        };

        Context.Weapons.Add(weapon);
        _newWeapons.Add(weapon);
        CreateParcelInfo(parcel);
    }

    public async Task UpdateIdCardBackground(ParcelResult parcel)
    {
        if (IsConsume) return;
        var idCardBackground = Context.IdCardBackgrounds.FirstOrDefault(x =>
            x.AccountServerId == Account.ServerId &&
            x.UniqueId == parcel.Id);

        if (idCardBackground != null) return;

        idCardBackground = new IdCardBackgroundDBServer()
        {
            AccountServerId = Account.ServerId,
            UniqueId = parcel.Id
        };

        Context.IdCardBackgrounds.Add(idCardBackground);
        _newIdCardBackgrounds.Add(idCardBackground);
        CreateParcelInfo(parcel);
    }

    public async Task UpdateEmblem(ParcelResult parcel)
    {
        if (IsConsume) return;
        var emblem = Context.Emblems.FirstOrDefault(x =>
            x.AccountServerId == Account.ServerId &&
            x.UniqueId == parcel.Id);

        if (emblem != null) return;

        var emblemDB = new EmblemDBServer()
        {
            AccountServerId = Account.ServerId,
            UniqueId = parcel.Id,
            ReceiveDate = Account.GameSettings.ServerDateTime()
        };

        Context.Emblems.Add(emblemDB);
        _newEmblems.Add(emblemDB);
        CreateParcelInfo(parcel);
    }

    public async Task UpdateSticker(ParcelResult parcel)
    {
        if (IsConsume) return;
        var sticker = Context.Stickers.FirstOrDefault(x =>
            x.AccountServerId == Account.ServerId &&
            x.StickerUniqueId == parcel.Id);

        if (sticker != null) return;

        var stickerDB = new StickerDBServer()
        {
            AccountServerId = Account.ServerId,
            StickerUniqueId = parcel.Id
        };

        Context.Stickers.Add(stickerDB);
        _newStickers.Add(stickerDB);
        CreateParcelInfo(parcel);
    }

    public async Task UpdateCostume(ParcelResult parcel, List<CostumeExcelT> costumeExcels)
    {
        if (IsConsume) return;
        var costumeExcel = costumeExcels.FirstOrDefault(x => x.CostumeUniqueId == parcel.Id);
        if (costumeExcel == null) return;

        var character = Context.Characters.FirstOrDefault(x => x.AccountServerId == Account.ServerId && x.UniqueId == costumeExcel.CostumeGroupId);
        var costume = Context.Costumes.FirstOrDefault(x => x.AccountServerId == Account.ServerId && x.UniqueId == parcel.Id);

        if (character == null) return;
        if (costume != null) return;

        var costumeDB = new CostumeDBServer()
        {
            AccountServerId = Account.ServerId,
            UniqueId = parcel.Id,
            BoundCharacterServerId = character.ServerId
        };

        Context.Costumes.Add(costumeDB);
        _newCostumes.Add(costumeDB);
        CreateParcelInfo(parcel);
    }

    public void CreateParcelInfo(ParcelResult parcel)
    {
        ParcelInfos.Add(new ParcelInfo
        {
            Key = new ParcelKeyPair
            {
                Type = parcel.Type,
                Id = parcel.Id
            },
            Amount = parcel.Amount,
            Multiplier = BasisPoint.One,
            Probability = BasisPoint.One
        });
    }

    public async Task FinalizeUpdates(IDbContextTransaction transaction)
    {
        var accountCurrencyDB = Context.Currencies.First(x => x.AccountServerId == Account.ServerId);
        if (Account.Level != accountCurrencyDB.AccountLevel)
            accountCurrencyDB.UpdateAccountLevel(Account.Level);

        ParcelResult.AccountDB = Account.ToMap(Mapper);
        ParcelResult.AccountCurrencyDB = accountCurrencyDB.ToMap(Mapper);

        await Context.SaveChangesAsync();
        await transaction.CommitAsync();

        foreach (var character in _updatedCharacters.Values)
        {
            ParcelResult.CharacterDBs.Add(character.ToMap(Mapper));
        }

        foreach (var item in _newItems)
        {
            ParcelResult.ItemDBs.Add(item.ServerId, item.ToMap(Mapper));
        }

        foreach (var equipment in _newEquipments)
        {
            ParcelResult.EquipmentDBs.Add(equipment.ServerId, equipment.ToMap(Mapper));
        }

        foreach (var weapon in _newWeapons)
        {
            ParcelResult.WeaponDBs.Add(weapon.ToMap(Mapper));
        }

        foreach (var costume in _newCostumes)
        {
            ParcelResult.CostumeDBs.Add(costume.ToMap(Mapper));
        }

        foreach (var furniture in _newFurnitures)
        {
            ParcelResult.FurnitureDBs.Add(furniture.ServerId, furniture.ToMap(Mapper));
        }

        foreach (var idCardBackground in _newIdCardBackgrounds)
        {
            ParcelResult.IdCardBackgroundDBs.Add(idCardBackground.ServerId, idCardBackground.ToMap(Mapper));
        }

        foreach (var memoryLobby in _newMemoryLobbies)
        {
            ParcelResult.MemoryLobbyDBs.Add(memoryLobby.ToMap(Mapper));
        }

        foreach (var emblem in _newEmblems)
        {
            ParcelResult.EmblemDBs.Add(emblem.ToMap(Mapper));
        }

        foreach (var sticker in _newStickers)
        {
            ParcelResult.StickerDBs.Add(sticker.ToMap(Mapper));
        }
    }
}
