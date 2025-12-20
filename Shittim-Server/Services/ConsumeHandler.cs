using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore.Storage;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;

namespace Shittim_Server.Services;

public class ConsumeHandler
{
    private readonly ExcelTableService _excelTableService;
    private readonly IMapper _mapper;

    public ConsumeHandler(ExcelTableService excelTableService, IMapper mapper)
    {
        _excelTableService = excelTableService;
        _mapper = mapper;
    }

    public async Task<ConsumeResultDatas> BuildConsumeResult(
        SchaleDataContext context, AccountDBServer account, ConsumeRequestDB consumeRequests,
        ConsumeResultDB? consumeResultDB = null, ParcelResultDB? parcelResultDB = null)
    => await BuildConsumeResult(context, account, [consumeRequests], consumeResultDB, parcelResultDB);

    public async Task<ConsumeResultDatas> BuildConsumeResult(
        SchaleDataContext context, AccountDBServer account, List<ConsumeRequestDB> consumeRequests,
        ConsumeResultDB? consumeResultDB = null, ParcelResultDB? parcelResultDB = null)
    {
        var consumeResultDatas = new ConsumeResultDatas();
        if (consumeResultDB != null) consumeResultDatas.ConsumeResult = consumeResultDB;
        if (parcelResultDB != null) consumeResultDatas.ParcelResult = parcelResultDB;

        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var consumeResolver = new ConsumeResolver(context, account, _excelTableService, consumeResultDatas, _mapper);

            foreach (var consumeRequest in consumeRequests)
            {
                consumeResolver.HandleEquipmentConsumption(consumeRequest);
                consumeResolver.HandleFurnitureConsumption(consumeRequest);
                consumeResolver.HandleItemConsumption(consumeRequest);
            }

            await consumeResolver.FinalizeUpdates(transaction);

            return consumeResultDatas;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

public class ConsumeResultDatas
{
    public long AccumulatedExp { get; set; }
    public ParcelResultDB ParcelResult { get; set; } = new()
    {
        EquipmentDBs = new Dictionary<long, EquipmentDB>(),
        FurnitureDBs = new Dictionary<long, FurnitureDB>(),
        ItemDBs = new Dictionary<long, ItemDB>(),
    };

    public ConsumeResultDB ConsumeResult { get; set; } = new ConsumeResultDB()
    {
        UsedEquipmentServerIdAndRemainingCounts = new Dictionary<long, long>(),
        UsedFurnitureServerIdAndRemainingCounts = new Dictionary<long, long>(),
        UsedItemServerIdAndRemainingCounts = new Dictionary<long, long>(),

        RemovedEquipmentServerIds = new List<long>(),
        RemovedFurnitureServerIds = new List<long>(),
        RemovedItemServerIds = new List<long>()
    };
}

public class ConsumeResolver
{
    private SchaleDataContext Context { get; set; }
    private AccountDBServer Account { get; set; }
    private ExcelTableService ExcelTableService { get; set; }
    private ConsumeResultDatas ConsumeResultData { get; set; }
    private IMapper Mapper { get; set; }

    private readonly List<EquipmentStatExcelT> equipmentStatExcels;
    private readonly List<FurnitureExcelT> furnitureExcels;
    private readonly List<ItemExcelT> itemExcels;

    private Dictionary<long, EquipmentDBServer> _equipmentDBs = new();
    private Dictionary<long, FurnitureDBServer> _furnitureDBs = new();
    private Dictionary<long, ItemDBServer> _itemDBs = new();

    public ConsumeResolver(SchaleDataContext context, AccountDBServer account, ExcelTableService excelTableService, ConsumeResultDatas consumeResult, IMapper mapper)
    {
        this.Context = context;
        this.Account = account;
        this.ExcelTableService = excelTableService;
        this.ConsumeResultData = consumeResult;
        this.Mapper = mapper;

        this.equipmentStatExcels = excelTableService.GetTable<EquipmentStatExcelT>().ToList();
        this.furnitureExcels = excelTableService.GetTable<FurnitureExcelT>().ToList();
        this.itemExcels = excelTableService.GetTable<ItemExcelT>().ToList();
    }

    public void HandleEquipmentConsumption(ConsumeRequestDB consumeRequest)
    {
        foreach (var consumeData in consumeRequest.ConsumeEquipmentServerIdAndCounts)
        {
            var equipment = Context.Equipments.FirstOrDefault(x => x.AccountServerId == Account.ServerId && x.ServerId == consumeData.Key);
            if (equipment == null) continue;

            var equipmentStatExcel = equipmentStatExcels.GetEquipmentStatExcelById(equipment.UniqueId);
            long levelUpFeedExp = equipmentStatExcel?.LevelUpFeedExp ?? 0;

            if (equipment.StackCount >= consumeData.Value)
            {
                equipment.StackCount -= consumeData.Value;
                ConsumeResultData.AccumulatedExp += levelUpFeedExp * consumeData.Value;
                Context.Equipments.Update(equipment);

                if (equipment.StackCount <= 0)
                {
                    Context.Equipments.Remove(equipment);
                    ConsumeResultData.ConsumeResult.RemovedEquipmentServerIds.Add(equipment.ServerId);
                }
                else
                {
                    _equipmentDBs[equipment.UniqueId] = equipment;
                    ConsumeResultData.ConsumeResult.UsedEquipmentServerIdAndRemainingCounts.TryAdd(equipment.ServerId, equipment.StackCount);
                }
            }
            else
                ConsumeResultData.ConsumeResult.RemovedEquipmentServerIds.Add(equipment.ServerId);
        }
    }

    public void HandleFurnitureConsumption(ConsumeRequestDB consumeRequest)
    {
        foreach (var consumeData in consumeRequest.ConsumeFurnitureServerIdAndCounts)
        {
            var furniture = Context.Furnitures.FirstOrDefault(x => x.AccountServerId == Account.ServerId && x.ServerId == consumeData.Key);
            if (furniture == null) continue;

            var furnitureExcel = furnitureExcels.FirstOrDefault(x => x.Id == furniture.UniqueId);
            long levelUpFeedExp = furnitureExcel?.ComfortBonus ?? 0;

            if (furniture.StackCount >= consumeData.Value)
            {
                furniture.StackCount -= consumeData.Value;
                ConsumeResultData.AccumulatedExp += levelUpFeedExp * consumeData.Value;
                Context.Furnitures.Update(furniture);

                if (furniture.StackCount <= 0)
                {
                    Context.Furnitures.Remove(furniture);
                    ConsumeResultData.ConsumeResult.RemovedFurnitureServerIds.Add(furniture.ServerId);
                }
                else
                {
                    _furnitureDBs[furniture.UniqueId] = furniture;
                    ConsumeResultData.ConsumeResult.UsedFurnitureServerIdAndRemainingCounts.TryAdd(furniture.ServerId, furniture.StackCount);
                }
            }
            else
                ConsumeResultData.ConsumeResult.RemovedFurnitureServerIds.Add(furniture.ServerId);
        }
    }

    public void HandleItemConsumption(ConsumeRequestDB consumeRequest)
    {
        foreach (var consumeData in consumeRequest.ConsumeItemServerIdAndCounts)
        {
            var item = Context.Items.FirstOrDefault(x => x.AccountServerId == Account.ServerId && x.ServerId == consumeData.Key);
            if (item == null) continue;

            var itemExcel = itemExcels.FirstOrDefault(x => x.Id == item.UniqueId);
            long levelUpFeedExp = itemExcel?.StackableFunction ?? 0;

            if (item.StackCount >= consumeData.Value)
            {
                item.StackCount -= consumeData.Value;
                ConsumeResultData.AccumulatedExp += levelUpFeedExp * consumeData.Value;
                Context.Items.Update(item);

                if (item.StackCount <= 0)
                {
                    Context.Items.Remove(item);
                    ConsumeResultData.ConsumeResult.RemovedItemServerIds.Add(item.ServerId);
                }
                else
                {
                    _itemDBs[item.UniqueId] = item;
                    ConsumeResultData.ConsumeResult.UsedItemServerIdAndRemainingCounts.TryAdd(item.ServerId, item.StackCount);
                }
            }
            else
                ConsumeResultData.ConsumeResult.RemovedItemServerIds.Add(item.ServerId);
        }
    }

    public async Task FinalizeUpdates(IDbContextTransaction transaction)
    {
        var accountCurrencyDB = Context.Currencies.First(x => x.AccountServerId == Account.ServerId);
        if (Account.Level != accountCurrencyDB.AccountLevel)
            accountCurrencyDB.UpdateAccountLevel(Account.Level);
        ConsumeResultData.ParcelResult.AccountDB = Account.ToMap(Mapper);
        ConsumeResultData.ParcelResult.AccountCurrencyDB = accountCurrencyDB.ToMap(Mapper);

        await Context.SaveChangesAsync();
        await transaction.CommitAsync();

        foreach (var equipment in _equipmentDBs.Values)
        {
            ConsumeResultData.ParcelResult.EquipmentDBs.TryAdd(equipment.ServerId, equipment.ToMap(Mapper));
        }
        foreach (var furniture in _furnitureDBs.Values)
        {
            ConsumeResultData.ParcelResult.FurnitureDBs.TryAdd(furniture.ServerId, furniture.ToMap(Mapper));
        }
        foreach (var item in _itemDBs.Values)
        {
            ConsumeResultData.ParcelResult.ItemDBs.TryAdd(item.ServerId, item.ToMap(Mapper));
        }
    }
}
