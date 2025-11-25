using Schale.FlatData;

namespace Schale.Excel
{
    public static class IngredientRecipeExcelExt
    {
        public static RecipeIngredientExcelT GetRecipeIngredientExcelById(
            this List<RecipeIngredientExcelT> recipes, long id) =>
            recipes.FirstOrDefault(recipe => recipe.Id == id);
    }
}


