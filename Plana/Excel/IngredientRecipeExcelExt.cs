using Plana.FlatData;

namespace Plana.Excel
{
    public static class IngredientRecipeExcelExt
    {
        public static RecipeIngredientExcelT GetRecipeIngredientExcelById(this List<RecipeIngredientExcelT> recipeIngredientExcels, long id)
        {
            return recipeIngredientExcels.First(x => x.Id == id);
        }
    }
}