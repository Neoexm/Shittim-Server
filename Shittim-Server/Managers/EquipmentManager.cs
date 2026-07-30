using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Services;
using Shittim.Services;

namespace Shittim_Server.Managers
{
    public class EquipmentManager
    {
        private readonly ExcelTableService excelTableService;
        private readonly ParcelHandler parcelHandler;
        private readonly ConsumeHandler consumeHandler;

        private readonly List<EquipmentExcelT> equipmentExcels;
        private readonly List<EquipmentLevelExcelT> equipmentLevelExcel;
        private readonly List<CharacterGearExcelT> characterGearExcels;
        private readonly List<RecipeIngredientExcelT> recipeIngredientExcels;

        private readonly List<ExpLevelData> equipmentExpLevelDatas;

        public EquipmentManager(ExcelTableService _excelTableService, ParcelHandler _parcelHandler, ConsumeHandler _consumeHandler)
        {
            excelTableService = _excelTableService;
            parcelHandler = _parcelHandler;
            consumeHandler = _consumeHandler;

            equipmentExcels = excelTableService.GetTable<EquipmentExcelT>();
            characterGearExcels = excelTableService.GetTable<CharacterGearExcelT>();
            recipeIngredientExcels = excelTableService.GetTable<RecipeIngredientExcelT>();

            equipmentLevelExcel = excelTableService.GetTable<EquipmentLevelExcelT>();
            equipmentExpLevelDatas = new ExpLevelData().ConvertExpLevelData(equipmentLevelExcel);
        }

        public async Task<(CharacterDBServer, EquipmentDBServer, EquipmentDBServer)> EquipmentEquip(
            SchaleDataContext context, AccountDBServer account, EquipmentItemEquipRequest req)
        {
            var originalStack = context.Equipments.FirstOrDefault(x => x.ServerId == req.EquipmentServerId)
                ?? throw new WebAPIException(WebAPIErrorCode.EquipmentNotFound);

            originalStack.StackCount--;
            if (originalStack.StackCount <= 0) context.Equipments.Remove(originalStack);
            else context.Equipments.Update(originalStack);

            var newEquipment = new EquipmentDBServer()
            {
                UniqueId = originalStack.UniqueId,
                StackCount = 1,
                BoundCharacterServerId = req.CharacterServerId,
            };
            context.AddEquipment(account.ServerId, [newEquipment]);
            await context.SaveChangesAsync();

            var equippedCharacter = context.Characters.FirstOrDefault(x => x.ServerId == req.CharacterServerId)
                ?? throw new WebAPIException(WebAPIErrorCode.CharacterNotFound);
            equippedCharacter.EquipmentServerIds ??= [];

            while (equippedCharacter.EquipmentServerIds.Count < 3)
                equippedCharacter.EquipmentServerIds.Add(0);

            equippedCharacter.EquipmentServerIds[req.SlotIndex] = newEquipment.ServerId;
            context.Characters.Update(equippedCharacter);

            await context.SaveChangesAsync();

            return (equippedCharacter, originalStack, newEquipment);
        }

        public async Task<(EquipmentDBServer, ConsumeResultDB)> EquipmentLevelUp(
            SchaleDataContext context, AccountDBServer account, EquipmentItemLevelUpRequest req)
        {
            var consumeResultData = await consumeHandler.BuildConsumeResult(context, account, req.ConsumeRequestDB);
            var targetEquipment = context.Equipments.FirstOrDefault(x => x.ServerId == req.TargetServerId)
                ?? throw new WebAPIException(WebAPIErrorCode.EquipmentNotFound);
            var currentEquipmentExcel = equipmentExcels.FirstOrDefault(x => x.Id == targetEquipment.UniqueId)
                ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound);
            var allEquipmentCategory = equipmentExcels
                .GetEquipmentExcelByCategory(currentEquipmentExcel.EquipmentCategory).GetCharacterEquipment();
            var (resultLevel, resultExp) = MathService.CalculateLevelExpWithoutReset(targetEquipment.Level, targetEquipment.Exp, consumeResultData.AccumulatedExp, equipmentExpLevelDatas);
            var (finalLevel, finalExp) = EquipmentService.CalculateEquipmentExpLevel(currentEquipmentExcel, equipmentLevelExcel, resultLevel, resultExp);

            targetEquipment.Level = finalLevel;
            targetEquipment.Exp = finalExp;
            targetEquipment.StackCount = 1;

            context.Equipments.Update(targetEquipment);

            // Feeding costs gold, 1 per exp point: the captured official level-up burned exactly
            // 69,304 gold alongside its fodder, and every official response carries the updated
            // AccountCurrencyDB. Clamped so a poor account cannot go negative.
            var currency = context.GetAccountCurrencies(account.ServerId).FirstOrDefault();
            if (currency != null && consumeResultData.AccumulatedExp > 0)
            {
                var goldCost = Math.Min(consumeResultData.AccumulatedExp, currency.CurrencyDict[CurrencyTypes.Gold]);
                currency.CurrencyDict[CurrencyTypes.Gold] -= goldCost;
                currency.UpdateTimeDict[CurrencyTypes.Gold] = account.GameSettings.ServerDateTime();
            }

            await context.SaveChangesAsync();

            return (targetEquipment, consumeResultData.ConsumeResult);
        }

        public async Task<(EquipmentDBServer, ParcelResultDB, ConsumeResultDB)> EquipmentTierUp(
            SchaleDataContext context, AccountDBServer account, EquipmentItemTierUpRequest req)
        {
            List<ParcelResult> parcelResults = [];
            var targetEquipment = context.Equipments.FirstOrDefault(x => x.ServerId == req.TargetEquipmentServerId)
                ?? throw new WebAPIException(WebAPIErrorCode.EquipmentNotFound);
            var currentEquipmentExcel = equipmentExcels.FirstOrDefault(x => x.Id == targetEquipment.UniqueId)
                ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound);
            EquipmentService.CreateRecipes(parcelResults, recipeIngredientExcels, currentEquipmentExcel.RecipeId);
            var nextEquipmentExcel = equipmentExcels.FirstOrDefault(x => x.Id == currentEquipmentExcel.NextTierEquipment)
                ?? throw new WebAPIException(WebAPIErrorCode.EquipmentCannotTierUp);

            targetEquipment.UniqueId = nextEquipmentExcel.Id;
            targetEquipment.Tier = (int)nextEquipmentExcel.TierInit;
            await context.SaveChangesAsync();

            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResults, isConsume: true);

            // Official's tier-up carries ConsumeResultDB alongside ParcelResultDB - without it the
            // client keeps showing the spent ingredients until the next full list. The resolver's
            // post-consume stacks are exactly the "remaining counts" official reports.
            var consumeResult = new ConsumeResultDB
            {
                UsedEquipmentServerIdAndRemainingCounts = (parcelResolver.ParcelResult.EquipmentDBs ?? [])
                    .ToDictionary(x => x.Key, x => x.Value.StackCount),
                UsedItemServerIdAndRemainingCounts = (parcelResolver.ParcelResult.ItemDBs ?? [])
                    .ToDictionary(x => x.Key, x => x.Value.StackCount),
            };

            return (targetEquipment, parcelResolver.ParcelResult, consumeResult);
        }

        public async Task<(List<EquipmentDBServer>, GearDBServer?, ConsumeResultDB, ParcelResultDB)> EquipmentBatchGrowth(
            SchaleDataContext context, AccountDBServer account, EquipmentBatchGrowthRequest req)
        {
            List<EquipmentDBServer> equipmentDBs = [];
            GearDBServer gearDB = null;
            ConsumeResultDB consumeResultDB = new ConsumeResultDatas().ConsumeResult;
            ParcelResultDB parcelResultDB = new();

            List<ParcelResult> parcelResults = [];
            if (req.EquipmentBatchGrowthRequestDBs.Count != 0)
            {
                foreach (var batchGrowthDB in req.EquipmentBatchGrowthRequestDBs)
                {
                    var targetEquipment = context.Equipments.FirstOrDefault(x => x.ServerId == batchGrowthDB.TargetServerId)
                        ?? throw new WebAPIException(WebAPIErrorCode.EquipmentNotFound);
                    var currentEquipmentExcel = equipmentExcels.FirstOrDefault(x => x.Id == targetEquipment.UniqueId)
                        ?? throw new WebAPIException(WebAPIErrorCode.DataEntityNotFound);
                    var allEquipmentCategory = equipmentExcels.GetEquipmentExcelByCategory(currentEquipmentExcel.EquipmentCategory).GetCharacterEquipment();

                    var finalTier = targetEquipment.Tier;
                    var newEquipmentExcel = currentEquipmentExcel;
                    if (batchGrowthDB.AfterTier > targetEquipment.Tier)
                    {
                        var equipmentDatas = allEquipmentCategory.GetEquipmentByTierUpgrade(targetEquipment.Tier, batchGrowthDB.AfterTier);
                        if (equipmentDatas.Count == 0)
                            throw new WebAPIException(WebAPIErrorCode.EquipmentBatchGrowthNotValid);

                        EquipmentService.CreateRecipes(parcelResults, recipeIngredientExcels, equipmentDatas);
                        newEquipmentExcel = equipmentDatas.Last();
                        targetEquipment.UniqueId = newEquipmentExcel.Id;
                        finalTier = (int)newEquipmentExcel.TierInit;
                    }
                    targetEquipment.Tier = finalTier;

                    var consumeResultData = await consumeHandler.BuildConsumeResult(context, account, batchGrowthDB.ConsumeRequestDBs, consumeResultDB, parcelResultDB);
                    var (resultLevel, resultExp) = MathService.CalculateLevelExpWithoutReset(targetEquipment.Level, targetEquipment.Exp, consumeResultData.AccumulatedExp, equipmentExpLevelDatas);
                    var (finalLevel, finalExp) = EquipmentService.CalculateEquipmentExpLevel(newEquipmentExcel, equipmentLevelExcel, resultLevel, resultExp);

                    targetEquipment.Level = finalLevel;
                    targetEquipment.Exp = finalExp;

                    consumeResultDB = consumeResultData.ConsumeResult;
                    parcelResultDB = consumeResultData.ParcelResult;

                    context.Equipments.Update(targetEquipment);
                    equipmentDBs.Add(targetEquipment);
                }
            }

            if (req.GearTierUpRequestDB != null)
            {
                var targetGear = context.Gears.FirstOrDefault(x => x.ServerId == req.GearTierUpRequestDB.TargetServerId)
                    ?? throw new WebAPIException(WebAPIErrorCode.CharacterGearNotFound);
                var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == targetGear.BoundCharacterServerId)
                    ?? throw new WebAPIException(WebAPIErrorCode.CharacterNotFound);
                var characterGears = characterGearExcels.GetCharacterGearExcelByCharacterId(targetCharacter.UniqueId);
                var characterGear = characterGears.FirstOrDefault(x => x.Tier == targetGear.Tier + 1)
                    ?? throw new WebAPIException(WebAPIErrorCode.CharacterGearCannotTierUp);
                EquipmentService.CreateRecipes(parcelResults, recipeIngredientExcels, characterGear.RecipeId);

                targetGear.UniqueId = characterGear.Id;
                targetGear.Tier = (int)characterGear.Tier;
                context.Gears.Update(targetGear);

                gearDB = targetGear;
            }

            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResults, parcelResultDB, true);
            await context.SaveChangesAsync();

            return (equipmentDBs, gearDB, consumeResultDB, parcelResultDB);
        }
    }
}
