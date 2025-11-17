using AutoMapper;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Services;

public class GachaService
{
    public static long GetGachaAmountByItem(List<ItemExcelT> itemExcels, long itemId)
    {
        var item = itemExcels.GetItemExcelById(itemId);
        if (item == null)
            return 10;

        return item.GachaTicket switch
        {
            GachaTicketType.Normal => 10,
            GachaTicketType.NormalOnce => 1,
            GachaTicketType.SelectRecruit => 1,
            _ => 10,
        };
    }

    public static (long, long, long, long, long, bool) InitializeGachaRates(ShopCategoryType characterBannerType)
    {
        var (rateUpSSRRate, fesSSRRate, limitedSSRRate, permanentSSRRate, SRRate, isSelector) = (0, 0, 0, 0, 0, false);
        switch (characterBannerType)
        {
            case ShopCategoryType.NormalGacha:
                permanentSSRRate = 30;
                SRRate = 185;
                break;
            case ShopCategoryType.FesGacha:
                rateUpSSRRate = 7;
                fesSSRRate = 9;
                permanentSSRRate = 44;
                SRRate = 185;
                break;
            case ShopCategoryType.TicketGacha:
                isSelector = true;
                break;
            case ShopCategoryType.LimitedGacha:
            case ShopCategoryType.PickupGacha:
            default:
                rateUpSSRRate = 7;
                permanentSSRRate = 23;
                SRRate = 185;
                break;
        }

        return (rateUpSSRRate, fesSSRRate, limitedSSRRate, permanentSSRRate, SRRate, isSelector);
    }

    public static async Task AddGachaResult(
        SchaleDataContext context, AccountDBServer account, IMapper mapper,
        CharacterExcelT characterExcel, List<GachaResult> gachaList, List<ItemDBServer> items)
    {
        var accountChSet = context.GetAccountCharacters(account.ServerId).Select(x => x.UniqueId).ToHashSet();
        var isNew = accountChSet.Add(characterExcel.Id);

        var gachaResult = new GachaResult()
        {
            CharacterId = characterExcel.Id,
            Character = null,
            Stone = null
        };

        if (isNew)
        {
            context.Characters.Add(new CharacterDBServer()
            {
                AccountServerId = account.ServerId,
                UniqueId = characterExcel.Id,
                StarGrade = characterExcel.DefaultStarGrade
            });
            await context.SaveChangesAsync();
            var character = context.Characters.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == characterExcel.Id);
            gachaResult.Character = character.ToMap(mapper);
        }
        else
        {
            await HandleItem(context, gachaResult, items, account.ServerId, characterExcel.SecretStoneItemId, characterExcel.SecretStoneItemAmount);
            await HandleItem(context, new GachaResult(), items, account.ServerId, characterExcel.CharacterPieceItemId, characterExcel.CharacterPieceItemAmount);
        }

        gachaList.Add(gachaResult);
    }

    private static async Task HandleItem(
        SchaleDataContext context, GachaResult gachaResult, List<ItemDBServer> items,
        long accountServerId, long itemId, long amount)
    {
        var itemInDB = context.Items.FirstOrDefault(x => x.AccountServerId == accountServerId && x.UniqueId == itemId);

        if (itemInDB != null) itemInDB.StackCount += amount;
        else
        {
            context.Items.Add(new ItemDBServer()
            {
                AccountServerId = accountServerId,
                UniqueId = itemId,
                StackCount = amount
            });
            await context.SaveChangesAsync();
            itemInDB = context.Items.FirstOrDefault(x => x.AccountServerId == accountServerId && x.UniqueId == itemId);
        }

        var existingItemInList = items.FirstOrDefault(x => x.UniqueId == itemId);
        if (existingItemInList != null) existingItemInList.StackCount = itemInDB.StackCount;
        else items.Add(itemInDB);

        gachaResult.Stone = new ItemDB()
        {
            UniqueId = itemInDB.UniqueId,
            ServerId = itemInDB.ServerId,
            StackCount = amount,
        };
    }

    public static CharacterExcelT GetRandomCharacterId(IReadOnlyList<CharacterExcelT> characterList, long excludeId = -1, bool excludeCondition = false)
    {
        var pool = excludeCondition ? characterList.Where(x => x.Id != excludeId).ToList() : characterList;
        return pool[Random.Shared.Next(pool.Count)];
    }
}
