using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Services;

namespace BlueArchiveAPI.Services
{
    public class GearManager
    {
        private readonly ExcelTableService excelTableService;
        private readonly ParcelHandler parcelHandler;

        private readonly List<CharacterGearExcelT> characterGearExcels;
        private readonly List<RecipeIngredientExcelT> recipeIngredientExcels;

        public GearManager(ExcelTableService _excelTableService, ParcelHandler _parcelHandler)
        {
            excelTableService = _excelTableService;
            parcelHandler = _parcelHandler;

            characterGearExcels = _excelTableService.GetTable<CharacterGearExcelT>();
            recipeIngredientExcels = _excelTableService.GetTable<RecipeIngredientExcelT>();
        }

        public async Task<(GearDBServer, CharacterDBServer)> GearUnlock(
            SchaleDataContext context, AccountDBServer account, CharacterGearUnlockRequest req)
        {
            var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == req.CharacterServerId);

            var gearExcel = characterGearExcels
                .GetCharacterGearExcelByCharacterId(targetCharacter.UniqueId).GetCharacterGearExcelByTier(1);

            var newGear = new GearDBServer()
            {
                UniqueId = gearExcel.Id,
                Level = (int)gearExcel.MaxLevel,
                SlotIndex = req.SlotIndex,
                BoundCharacterServerId = req.CharacterServerId,
                Tier = (int)gearExcel.Tier,
                Exp = 0,
            };

            context.AddGears(account.ServerId, [newGear]);
            await context.SaveChangesAsync();

            var gear = context.Gears.FirstOrDefault(x => x.UniqueId == gearExcel.Id && x.BoundCharacterServerId == req.CharacterServerId);

            return (gear, targetCharacter);
        }

        public async Task<(GearDBServer, ParcelResultDB)> GearTierUp(
            SchaleDataContext context, AccountDBServer account, CharacterGearTierUpRequest req)
        {
            List<ParcelResult> parcelResult = [];

            var targetGear = context.Gears.FirstOrDefault(x => x.ServerId == req.GearServerId);
            var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == targetGear.BoundCharacterServerId);

            var currentGearExcel = characterGearExcels
                .GetCharacterGearExcelByCharacterId(targetCharacter.UniqueId).GetCharacterGearExcelByTier(1);
            EquipmentService.CreateRecipes(parcelResult, recipeIngredientExcels, currentGearExcel.RecipeId);
            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResult, isConsume: true);

            var nextGearExcel = characterGearExcels.GetCharacterGearExcelById(currentGearExcel.NextTierEquipment);
            targetGear.UniqueId = nextGearExcel.Id;
            targetGear.Tier = (int)nextGearExcel.Tier;

            await context.SaveChangesAsync();

            return (targetGear, parcelResolver.ParcelResult);
        }
    }
}
