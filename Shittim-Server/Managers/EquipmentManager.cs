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
            var originalStack = context.Equipments.FirstOrDefault(x => x.ServerId == req.EquipmentServerId);

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

            var equippedCharacter = context.Characters.FirstOrDefault(x => x.ServerId == req.CharacterServerId);
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
            var targetEquipment = context.Equipments.FirstOrDefault(x => x.ServerId == req.TargetServerId);
            var currentEquipmentExcel = equipmentExcels.GetEquipmentExcelById(targetEquipment.UniqueId);
            var allEquipmentCategory = equipmentExcels
                .GetEquipmentExcelByCategory(currentEquipmentExcel.EquipmentCategory).GetCharacterEquipment();
            var (resultLevel, resultExp) = MathService.CalculateLevelExpWithoutReset(targetEquipment.Level, targetEquipment.Exp, consumeResultData.AccumulatedExp, equipmentExpLevelDatas);
            var (finalLevel, finalExp) = EquipmentService.CalculateEquipmentExpLevel(currentEquipmentExcel, equipmentLevelExcel, resultLevel, resultExp);

            targetEquipment.Level = finalLevel;
            targetEquipment.Exp = finalExp;
            targetEquipment.StackCount = 1;

            context.Equipments.Update(targetEquipment);

            await context.SaveChangesAsync();

            return (targetEquipment, consumeResultData.ConsumeResult);
        }

        public async Task<(EquipmentDBServer, ParcelResultDB)> EquipmentTierUp(
            SchaleDataContext context, AccountDBServer account, EquipmentItemTierUpRequest req)
        {
            List<ParcelResult> parcelResults = [];
            var targetEquipment = context.Equipments.FirstOrDefault(x => x.ServerId == req.TargetEquipmentServerId);
            var currentEquipmentExcel = equipmentExcels.GetEquipmentExcelById(targetEquipment.UniqueId);
            EquipmentService.CreateRecipes(parcelResults, recipeIngredientExcels, currentEquipmentExcel.RecipeId);
            var nextEquipmentExcel = equipmentExcels.GetEquipmentExcelById(currentEquipmentExcel.NextTierEquipment);

            targetEquipment.UniqueId = nextEquipmentExcel.Id;
            targetEquipment.Tier = (int)nextEquipmentExcel.TierInit;
            await context.SaveChangesAsync();

            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResults, isConsume: true);

            return (targetEquipment, parcelResolver.ParcelResult);
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
                    var targetEquipment = context.Equipments.FirstOrDefault(x => x.ServerId == batchGrowthDB.TargetServerId);
                    var currentEquipmentExcel = equipmentExcels.GetEquipmentExcelById(targetEquipment.UniqueId);
                    var allEquipmentCategory = equipmentExcels.GetEquipmentExcelByCategory(currentEquipmentExcel.EquipmentCategory).GetCharacterEquipment();

                    var finalTier = targetEquipment.Tier;
                    var newEquipmentExcel = currentEquipmentExcel;
                    if (batchGrowthDB.AfterTier > targetEquipment.Tier)
                    {
                        var equipmentDatas = allEquipmentCategory.GetEquipmentByTierUpgrade(targetEquipment.Tier, batchGrowthDB.AfterTier);
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
                var targetGear = context.Gears.FirstOrDefault(x => x.ServerId == req.GearTierUpRequestDB.TargetServerId);
                var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == targetGear.BoundCharacterServerId);
                var characterGears = characterGearExcels.GetCharacterGearExcelByCharacterId(targetCharacter.UniqueId);
                var characterGear = characterGears.GetCharacterGearExcelByTier(targetGear.Tier + 1);
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
