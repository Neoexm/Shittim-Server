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

        public ItemManager(ExcelTableService _excelTableService, ParcelHandler _parcelHandler)
        {
            excelTableService = _excelTableService;
            parcelHandler = _parcelHandler;
            
            parcelAutoSynthExcels = excelTableService.GetTable<ParcelAutoSynthExcelT>();
        }

        public async Task<ParcelResultDB> SelectTicket(
            SchaleDataContext context, AccountDBServer account, ItemSelectTicketRequest req)
        {
            var item = context.Items.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.ServerId == req.TicketItemServerId)
                ?? throw new WebAPIException(WebAPIErrorCode.ItemNotFound);

            var parcelResult = new ParcelResult(item.Type, item.UniqueId, req.ConsumeCount);
            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResult, isConsume: true);
            var parcelAdd = new ParcelResult(ParcelType.Character, req.SelectItemUniqueId, req.ConsumeCount);
            var parcelResolverAdd = await parcelHandler.BuildParcel(context, account, parcelAdd, parcelResolver.ParcelResult);

            return parcelResolverAdd.ParcelResult;
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
