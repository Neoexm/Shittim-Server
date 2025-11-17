using Shittim_Server.Services;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.Logic.BattlesEntities;
using Schale.MX.GameLogic.Parcel;

namespace Shittim_Server.Services
{
    public class CharacterService
    {
        public static (long, string) IdentifyAndUpdateSkill(
            CharacterDBServer targetCharacter, int level, SkillSlot skillSlot, CharacterSkillListExcelT characterSkillListExcel)
        {
            var previousLevel = 1;
            var skillSlotString = skillSlot.ToString();

            if (skillSlotString.StartsWith("ExSkill"))
            {
                previousLevel = targetCharacter.ExSkillLevel;
                targetCharacter.ExSkillLevel = level;
                return (previousLevel, characterSkillListExcel.ExSkillGroupId.First());
            }
            else if (skillSlotString.StartsWith("PublicSkill"))
            {
                previousLevel = targetCharacter.PublicSkillLevel;
                targetCharacter.PublicSkillLevel = level;
                return (previousLevel, characterSkillListExcel.PublicSkillGroupId.First());
            }
            else if (skillSlotString.StartsWith("Passive"))
            {
                previousLevel = targetCharacter.PassiveSkillLevel;
                targetCharacter.PassiveSkillLevel = level;
                return (previousLevel, characterSkillListExcel.PassiveSkillGroupId.First());
            }
            else if (skillSlotString.StartsWith("ExtraPassive"))
            {
                previousLevel = targetCharacter.ExtraPassiveSkillLevel;
                targetCharacter.ExtraPassiveSkillLevel = level;
                return (previousLevel, characterSkillListExcel.ExtraPassiveSkillGroupId.First());
            }

            return (previousLevel, "");
        }

        public static List<long> GetIngredientRecipeListBySkillLevel(List<SkillExcelT> recipeExcels, long currentLevel, long targetLevel)
        {
            List<long> recipeIds = [];
            for (long i = currentLevel; i < targetLevel; i++)
            {
                recipeIds.Add(recipeExcels.GetCharacterSkillByLevel(i).RequireLevelUpMaterial);
            }
            return recipeIds;
        }

        public static List<long> GetIngredientRecipeListByPotentialLevel(List<CharacterPotentialStatExcelT> recipeExcels, long currentLevel, long targetLevel)
        {
            List<long> recipeIds = [];
            for (long i = currentLevel; i < targetLevel; i++)
            {
                recipeIds.Add(recipeExcels.GetCharacterPotentialByLevel(i).RecipeId);
            }
            return recipeIds;
        }

        public static void CreateRecipes(List<ParcelResult> parcelResults, List<RecipeIngredientExcelT> recipeExcels, long recipeIds)
            => CreateRecipes(parcelResults, recipeExcels, [recipeIds]);
        public static void CreateRecipes(List<ParcelResult> parcelResults, List<RecipeIngredientExcelT> recipeExcels, List<long> recipeIds)
        {
            if (recipeIds == null) return;
            foreach (var recipeId in recipeIds)
            {
                if (recipeId == 0) continue;
                var recipeExcel = recipeExcels.GetRecipeIngredientExcelById(recipeId);
                parcelResults.AddRange(ParcelResult.ConvertParcelResult(recipeExcel.CostParcelType, recipeExcel.CostId, recipeExcel.CostAmount));
                parcelResults.AddRange(ParcelResult.ConvertParcelResult(recipeExcel.IngredientParcelType, recipeExcel.IngredientId, recipeExcel.IngredientAmount));
            }
        }

        public static long CalculateWeaponBonusExp(
            EquipmentExcelT equipmentExcel, CharacterWeaponExpBonusExcelT characterWeaponExpBonusExcel,
            long baseExp)
        {
            long exp = 0;
            if (equipmentExcel.EquipmentCategory == EquipmentCategory.WeaponExpGrowthA)
                exp = (long)(baseExp * (characterWeaponExpBonusExcel.WeaponExpGrowthA / 10000.0));
            else if (equipmentExcel.EquipmentCategory == EquipmentCategory.WeaponExpGrowthB)
                exp = (long)(baseExp * (characterWeaponExpBonusExcel.WeaponExpGrowthB / 10000.0));
            else if (equipmentExcel.EquipmentCategory == EquipmentCategory.WeaponExpGrowthC)
                exp = (long)(baseExp * (characterWeaponExpBonusExcel.WeaponExpGrowthC / 10000.0));
            else if (equipmentExcel.EquipmentCategory == EquipmentCategory.WeaponExpGrowthZ)
                exp = (long)(baseExp * (characterWeaponExpBonusExcel.WeaponExpGrowthZ / 10000.0));
            return exp;
        }
        
        public static long CalculateCharacterCreditExp(long exp)
        {
            return 7 * exp;
        }

        public static long CalculateWeaponCreditExp(long exp)
        {
            return 180 * exp;
        }
    }
}
