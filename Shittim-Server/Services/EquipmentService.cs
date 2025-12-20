using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;

namespace Shittim_Server.Services
{
    public class EquipmentService
    {
        public static (int, long) CalculateEquipmentExpLevel(
            EquipmentExcelT equipmentResult, List<EquipmentLevelExcelT> equipmentLevels,
            long resultLevel, long resultExp)
        {
            var currentEquipmentLevelData = equipmentLevels.GetEquipmentLevelExcelByLevel(resultLevel);
            var tierIndex = (int)equipmentResult.TierInit - 1;
            var equipmentMaxExp = currentEquipmentLevelData.TierLevelExp[tierIndex];

            var finalLevel = resultLevel > equipmentResult.MaxLevel ? equipmentResult.MaxLevel : resultLevel;
            var finalExp = resultExp > equipmentMaxExp ? equipmentMaxExp : resultExp;

            return ((int)finalLevel, finalExp);
        }

        public static void CreateRecipes(
            List<ParcelResult> parcelResults, List<RecipeIngredientExcelT> recipeExcels, List<EquipmentExcelT> equipments)
        {
            if (equipments == null) return;
            foreach (var equipment in equipments)
            {
                if (equipment.RecipeId == 0) continue;
                var recipeExcel = recipeExcels.GetRecipeIngredientExcelById(equipment.RecipeId);
                if (recipeExcel == null) continue;
                parcelResults.AddRange(ParcelResult.ConvertParcelResult(recipeExcel.CostParcelType, recipeExcel.CostId, recipeExcel.CostAmount));
                parcelResults.AddRange(ParcelResult.ConvertParcelResult(recipeExcel.IngredientParcelType, recipeExcel.IngredientId, recipeExcel.IngredientAmount));
            }
        }

        public static void CreateRecipes(
            List<ParcelResult> parcelResults, List<RecipeIngredientExcelT> recipeExcels, long recipeId)
        {
            if (recipeId == 0) return;
            var recipeExcel = recipeExcels.GetRecipeIngredientExcelById(recipeId);
            if (recipeExcel == null) return;
            parcelResults.AddRange(ParcelResult.ConvertParcelResult(recipeExcel.CostParcelType, recipeExcel.CostId, recipeExcel.CostAmount));
            parcelResults.AddRange(ParcelResult.ConvertParcelResult(recipeExcel.IngredientParcelType, recipeExcel.IngredientId, recipeExcel.IngredientAmount));
        }
    }
}
