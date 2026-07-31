using BlueArchiveAPI.Services;
using Shittim_Server.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Managers
{
    public class ItemManager
    {
        private readonly ExcelTableService excelTableService;
        private readonly ParcelHandler parcelHandler;

        private readonly List<ParcelAutoSynthExcelT> parcelAutoSynthExcels;
        private readonly List<ItemExcelT> itemExcels;
        private readonly List<RecipeExcelT> recipeExcels;
        private readonly List<RecipeSelectionGroupExcelT> recipeSelectionGroupExcels;
        private readonly List<GachaElementExcelT> gachaElementExcels;

        public ItemManager(ExcelTableService _excelTableService, ParcelHandler _parcelHandler)
        {
            excelTableService = _excelTableService;
            parcelHandler = _parcelHandler;

            parcelAutoSynthExcels = excelTableService.GetTable<ParcelAutoSynthExcelT>();
            itemExcels = excelTableService.GetTable<ItemExcelT>();
            recipeExcels = excelTableService.GetTable<RecipeExcelT>();
            recipeSelectionGroupExcels = excelTableService.GetTable<RecipeSelectionGroupExcelT>();
            gachaElementExcels = excelTableService.GetTable<GachaElementExcelT>();
        }

        public async Task<ParcelResultDB> SelectTicket(
            SchaleDataContext context, AccountDBServer account, ItemSelectTicketRequest req)
        {
            var item = context.Items.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.ServerId == req.TicketItemServerId)
                ?? throw new WebAPIException(WebAPIErrorCode.ItemNotFound);

            // the request only carries the picked parcel id; the type has to come from the ticket's selection recipe. Furniture select boxes send furniture ids here, and recording those as characters poisons the character list for every later login.
            var itemExcel = itemExcels.FirstOrDefault(x => x.Id == item.UniqueId);
            var recipe = itemExcel == null ? null : recipeExcels.FirstOrDefault(x => x.Id == itemExcel.UsingResultId);
            var selection = recipe == null ? null : recipeSelectionGroupExcels.FirstOrDefault(x =>
                x.RecipeSelectionGroupId == recipe.RecipeSelectionGroupId && x.ParcelId == req.SelectItemUniqueId);

            var parcelResult = new ParcelResult(item.Type, item.UniqueId, req.ConsumeCount);
            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResult, isConsume: true);
            var parcelAdd = selection != null
                ? new ParcelResult(selection.ParcelType, selection.ParcelId, selection.ResultAmountMax * req.ConsumeCount)
                : new ParcelResult(ParcelType.Character, req.SelectItemUniqueId, req.ConsumeCount);
            var parcelResolverAdd = await parcelHandler.BuildParcel(context, account, parcelAdd, parcelResolver.ParcelResult);

            return parcelResolverAdd.ParcelResult;
        }

        public async Task<(ItemDBServer, ParcelResultDB)> Consume(
            SchaleDataContext context, AccountDBServer account, ItemConsumeRequest req)
        {
            var item = context.Items.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.ServerId == req.TargetItemServerId)
                ?? throw new WebAPIException(WebAPIErrorCode.ItemNotFound);

            var itemExcel = itemExcels.First(x => x.Id == item.UniqueId);

            var parcelResult = new ParcelResult(item.Type, item.UniqueId, req.ConsumeCount);
            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResult, isConsume: true);
            var parcelAdd = new ParcelResult(itemExcel.UsingResultParcelType, itemExcel.UsingResultId, itemExcel.UsingResultAmount * req.ConsumeCount);
            var parcelResolverAdd = await parcelHandler.BuildParcel(context, account, parcelAdd, parcelResolver.ParcelResult);

            if (parcelResolverAdd.ParcelResult.RemovedItemIds.Contains(item.ServerId))
                item.StackCount = 0;

            return (item, parcelResolverAdd.ParcelResult);
        }

        public async Task<(ItemDBServer, List<ParcelInfo>)> BulkConsume(
            SchaleDataContext context, AccountDBServer account, ItemBulkConsumeRequest req)
        {
            var item = context.Items.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.ServerId == req.TargetItemServerId)
                ?? throw new WebAPIException(WebAPIErrorCode.ItemNotFound);

            var itemExcel = itemExcels.First(x => x.Id == item.UniqueId);

            var opened = new ParcelResult(itemExcel.UsingResultParcelType, itemExcel.UsingResultId, itemExcel.UsingResultAmount * req.ConsumeCount);
            var rewards = opened.Type == ParcelType.GachaGroup
                ? new GachaGroupHandler(gachaElementExcels).CreateGachaGroupParcel([opened]).Aggregate()
                : new List<ParcelResult> { opened };

            var parcelInfos = rewards.Select(x => new ParcelInfo
            {
                Key = new ParcelKeyPair { Type = x.Type, Id = x.Id },
                Amount = x.Amount
            }).ToList();

            // bulk open delivers through the mailbox, the response only previews what landed there
            var now = account.GameSettings.ServerDateTime();
            context.Mails.Add(new MailDBServer
            {
                AccountServerId = account.ServerId,
                Type = itemExcel.MailType,
                UniqueId = item.UniqueId,
                SendDate = now,
                ExpireDate = now.AddDays(7),
                ParcelInfos = parcelInfos,
                RemainParcelInfos = new List<ParcelInfo>()
            });

            var parcelResolver = await parcelHandler.BuildParcel(context, account, new ParcelResult(item.Type, item.UniqueId, req.ConsumeCount), isConsume: true);
            MailNotificationService.MarkNewMail(account.ServerId);

            if (parcelResolver.ParcelResult.RemovedItemIds.Contains(item.ServerId))
                item.StackCount = 0;

            return (item, parcelInfos);
        }

        public async Task<(Dictionary<long, ItemDBServer>, Dictionary<long, EquipmentDBServer>)> AutoSynth(
            SchaleDataContext context, AccountDBServer account, ItemAutoSynthRequest req)
        {
            var itemDBs = context.GetAccountItems(account.ServerId);
            var equipmentDBs = context.GetAccountEquipments(account.ServerId);
            Dictionary<long, ItemDBServer> itemDBParcel = [];
            Dictionary<long, EquipmentDBServer> equipmentDBParcel = [];

            foreach (var parcel in req.TargetParcels)
            {
                var parcelData = parcelAutoSynthExcels.FirstOrDefault(x => x.RequireParcelType == parcel.Type && x.RequireParcelId == parcel.Id);
                if (parcelData.RequireParcelType == ParcelType.Item)
                {
                    var itemParcel = itemDBs.FirstOrDefault(x => x.UniqueId == parcelData.RequireParcelId);

                    double totalItemRemoved = 0;
                    var totalSynthAdded = 0;
                    if (itemParcel.StackCount > parcelData.SynthStartAmount)
                    {
                        totalItemRemoved = itemParcel.StackCount - parcelData.SynthEndAmount;
                        totalSynthAdded = (int)Math.Floor(totalItemRemoved / parcelData.RequireParcelAmount);
                    }

                    var reducedItem = itemDBs.FirstOrDefault(x => x.UniqueId == parcelData.RequireParcelId);
                    var synthItem = itemDBs.FirstOrDefault(x => x.UniqueId == parcelData.ResultParcelId);
                    reducedItem.StackCount -= (int)totalItemRemoved;
                    synthItem.StackCount += totalSynthAdded;
                    itemDBParcel.Remove(reducedItem.UniqueId);
                    itemDBParcel.Remove(synthItem.UniqueId);
                    itemDBParcel.Add(reducedItem.UniqueId, reducedItem);
                    itemDBParcel.Add(synthItem.UniqueId, synthItem);
                    context.SaveChanges();
                }
                else if (parcelData.RequireParcelType == ParcelType.Equipment)
                {
                    var equipmentParcel = equipmentDBs.FirstOrDefault(x => x.UniqueId == parcelData.RequireParcelId);

                    double totalEquipmentRemoved = 0;
                    var totalSynthAdded = 0;
                    if (equipmentParcel.StackCount > parcelData.SynthStartAmount)
                    {
                        totalEquipmentRemoved = equipmentParcel.StackCount - parcelData.SynthEndAmount;
                        totalSynthAdded = (int)Math.Floor(totalEquipmentRemoved / parcelData.RequireParcelAmount);
                    }

                    var reducedEq = equipmentDBs.FirstOrDefault(x => x.UniqueId == parcelData.RequireParcelId);
                    var synthEq = equipmentDBs.FirstOrDefault(x => x.UniqueId == parcelData.ResultParcelId);
                    reducedEq.StackCount -= (int)totalEquipmentRemoved;
                    synthEq.StackCount += totalSynthAdded;
                    equipmentDBParcel.Remove(reducedEq.UniqueId);
                    equipmentDBParcel.Remove(synthEq.UniqueId);
                    equipmentDBParcel.Add(reducedEq.UniqueId, reducedEq);
                    equipmentDBParcel.Add(synthEq.UniqueId, synthEq);
                    context.SaveChanges();
                }
            }
            
            return (itemDBParcel, equipmentDBParcel);
        }
    }
}
