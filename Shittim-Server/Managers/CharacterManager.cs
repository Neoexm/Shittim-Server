using AutoMapper;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Services;

namespace Shittim_Server.Managers
{
    public class CharacterManager
    {
        private readonly ExcelTableService excelTableService;
        private readonly ParcelHandler parcelHandler;
        private readonly ConsumeHandler consumeHandler;
        private readonly IMapper mapper;

        private readonly List<CharacterExcelT> characterExcels;
        private readonly List<CharacterSkillListExcelT> characterSkillListExcels;
        private readonly List<CharacterWeaponExcelT> characterWeaponExcels;
        private readonly List<CharacterWeaponExpBonusExcelT> characterWeaponExpBonusExcels;
        private readonly List<CharacterTranscendenceExcelT> characterTranscendenceExcels;
        private readonly List<CharacterPotentialExcelT> potentialExcels;
        private readonly List<CharacterPotentialStatExcelT> potentialStatExcels;
        private readonly List<EquipmentExcelT> equipmentExcels;
        private readonly List<EquipmentStatExcelT> equipmentStatExcels;
        private readonly List<SkillExcelT> skillExcels;
        private readonly List<RecipeIngredientExcelT> recipeIngredientExcels;

        private readonly List<ExpLevelData> characterExpLevelDatas;
        private readonly List<ExpLevelData> weaponExpLevelDatas;

        public CharacterManager(
            ExcelTableService _excelTableService, ParcelHandler _parcelHandler, ConsumeHandler _consumeHandler, IMapper _mapper)
        {
            excelTableService = _excelTableService;
            parcelHandler = _parcelHandler;
            consumeHandler = _consumeHandler;
            mapper = _mapper;

            characterExcels = excelTableService.GetTable<CharacterExcelT>();
            characterSkillListExcels = excelTableService.GetTable<CharacterSkillListExcelT>();
            characterWeaponExcels = excelTableService.GetTable<CharacterWeaponExcelT>();
            characterWeaponExpBonusExcels = excelTableService.GetTable<CharacterWeaponExpBonusExcelT>();
            characterTranscendenceExcels = excelTableService.GetTable<CharacterTranscendenceExcelT>();
            potentialExcels = excelTableService.GetTable<CharacterPotentialExcelT>();
            potentialStatExcels = excelTableService.GetTable<CharacterPotentialStatExcelT>();
            equipmentExcels = excelTableService.GetTable<EquipmentExcelT>();
            equipmentStatExcels = excelTableService.GetTable<EquipmentStatExcelT>();
            skillExcels = excelTableService.GetTable<SkillExcelT>();
            recipeIngredientExcels = excelTableService.GetTable<RecipeIngredientExcelT>();

            var characterLevelExcel = excelTableService.GetTable<CharacterLevelExcelT>();
            var characterWeaponLevelExcel = excelTableService.GetTable<CharacterWeaponLevelExcelT>();
            characterExpLevelDatas = new ExpLevelData().ConvertExpLevelData(characterLevelExcel);
            weaponExpLevelDatas = new ExpLevelData().ConvertExpLevelData(characterWeaponLevelExcel);
        }

        public async Task<List<CharacterDBServer>> CharacterSetFavorite(
            SchaleDataContext context, AccountDBServer account, Dictionary<long, bool> characterServerIds)
        {
            var charServerIds = characterServerIds.Keys.ToList();
            var characters = context.GetAccountCharacters(account.ServerId).Where(x => charServerIds.Contains(x.ServerId)).ToList();
            foreach (var character in characters)
            {
                character.IsFavorite = characterServerIds[character.ServerId];
                context.Characters.Update(character);
            }
            await context.SaveChangesAsync();
            return characters;
        }

        public async Task<(CharacterDBServer, ParcelResultDB)> CharacterUpdateSkillLevel(
            SchaleDataContext context, AccountDBServer account, CharacterSkillLevelUpdateRequest req)
        {
            List<ParcelResult> parcelResultList = [];
            var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == req.TargetCharacterDBId);
            var characterSkillList = characterSkillListExcels.GetCharacterSkillListByCharacterId(targetCharacter.UniqueId);
            var (previousLevel, skillName) = CharacterService.IdentifyAndUpdateSkill(targetCharacter, req.Level, req.SkillSlot, characterSkillList);
            var characterSkillExcel = skillExcels.GetSkillExcelByGroupId(skillName).ToList();
            var allIngreRecipeIds = CharacterService.GetIngredientRecipeListBySkillLevel(characterSkillExcel, previousLevel, req.Level);
            CharacterService.CreateRecipes(parcelResultList, recipeIngredientExcels, allIngreRecipeIds);
        
            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResultList, isConsume: true);
            await context.SaveChangesAsync();

            return (targetCharacter, parcelResolver.ParcelResult);
        }
        
        public async Task<(CharacterDBServer, ParcelResultDB)> CharacterBatchUpdateSkillLevel(
            SchaleDataContext context, AccountDBServer account, CharacterBatchSkillLevelUpdateRequest req)
        {
            var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == req.TargetCharacterDBId);
            var characterSkillList = characterSkillListExcels.GetCharacterSkillListByCharacterId(targetCharacter.UniqueId);
            List<ParcelResult> parcelResultList = [];
            foreach (var skillDatas in req.SkillLevelUpdateRequestDBs)
            {
                var (previousLevel, skillName) = CharacterService.IdentifyAndUpdateSkill(targetCharacter, skillDatas.Level, skillDatas.SkillSlot, characterSkillList);
                var characterSkillExcel = skillExcels.GetSkillExcelByGroupId(skillName).ToList();
                var allIngreRecipeIds = CharacterService.GetIngredientRecipeListBySkillLevel(characterSkillExcel, previousLevel, skillDatas.Level);
                CharacterService.CreateRecipes(parcelResultList, recipeIngredientExcels, allIngreRecipeIds);
            }

            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResultList, isConsume: true);
            await context.SaveChangesAsync();

            return (targetCharacter, parcelResolver.ParcelResult);
        }
        
        public async Task<(CharacterDBServer, ConsumeResultDB, AccountCurrencyDBServer)> CharacterGrowth(
            SchaleDataContext context, AccountDBServer account, CharacterExpGrowthRequest req)
        {
            var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == req.TargetCharacterServerId);
            var accountCurrency = context.Currencies.FirstOrDefault(x => x.AccountServerId == account.ServerId);

            var consumeResultDatas = await consumeHandler.BuildConsumeResult(context, account, req.ConsumeRequestDB);
            long addExp = consumeResultDatas.AccumulatedExp;
            var calculatedCredit = CharacterService.CalculateCharacterCreditExp(addExp);
            accountCurrency.UpdateCurrency(CurrencyTypes.Gold, -calculatedCredit, account.GameSettings.ServerDateTime());

            var (resultLevel, resultExp) = MathService.CalculateLevelExp(targetCharacter.Level, targetCharacter.Exp, addExp, characterExpLevelDatas);
            targetCharacter.Level = resultLevel;
            targetCharacter.Exp = resultExp;

            context.Currencies.Update(accountCurrency);
            context.Characters.Update(targetCharacter);
            await context.SaveChangesAsync();

            return (targetCharacter, consumeResultDatas.ConsumeResult, accountCurrency);
        }

        public async Task<WeaponDBServer> UnlockWeapon(
            SchaleDataContext context, AccountDBServer account, long targetCharacterServerId)
        {
            var character = context.Characters.FirstOrDefault(x => x.ServerId == targetCharacterServerId)
                ?? throw new WebAPIException(WebAPIErrorCode.CharacterNotFound);

            var weapon = context.Weapons.FirstOrDefault(x => x.BoundCharacterServerId == targetCharacterServerId);
            if (weapon != null) return weapon;
            
            var weaponExcel = characterWeaponExcels.GetCharacterWeaponExcelByCharacterId(character.UniqueId);
            var newWeapon = new WeaponDBServer()
            {
                UniqueId = weaponExcel.Id,
                BoundCharacterServerId = targetCharacterServerId,
                StarGrade = 1,
                Level = 1,
                Exp = 0
            };
            context.AddWeapons(account.ServerId, [newWeapon]);
            await context.SaveChangesAsync();

            return newWeapon;
        }

        public async Task<(CharacterDBServer, ParcelResultDB)> PotentialGrowth(
            SchaleDataContext context, AccountDBServer account, CharacterPotentialGrowthRequest req)
        {
            var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == req.TargetCharacterDBId);
            List<ParcelResult> parcelResultList = [];
            foreach (var growthReq in req.PotentialGrowthRequestDBs)
            {
                var currentPotentialLevel = targetCharacter.PotentialStats[(int)growthReq.Type];
                var potentialExcel = potentialExcels.GetCharacterPotentialByCharacterId(targetCharacter.UniqueId, growthReq.Type);
                var characterPotentialData = potentialStatExcels.GetCharacterPotentialByGroupId(potentialExcel.PotentialStatGroupId);
                var allIngreRecipeIds = CharacterService.GetIngredientRecipeListByPotentialLevel(potentialStatExcels, currentPotentialLevel, growthReq.Level);
                CharacterService.CreateRecipes(parcelResultList, recipeIngredientExcels, allIngreRecipeIds);
                targetCharacter.PotentialStats[(int)growthReq.Type] = growthReq.Level;
            }
            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResultList, isConsume: true);
            await context.SaveChangesAsync();

            return (targetCharacter, parcelResolver.ParcelResult);
        }

        public async Task<(CharacterDBServer, ParcelResultDB)> CharacterTranscendence(
            SchaleDataContext context, AccountDBServer account, CharacterTranscendenceRequest req)
        {
            List<ParcelResult> parcelResult = [];
            var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == req.TargetCharacterServerId);
            var characterWeaponExcel = characterTranscendenceExcels.GetCharacterTranscendenceExcelByCharacterId(targetCharacter.UniqueId);
            var ingreRecipe = characterWeaponExcel.RecipeId[targetCharacter.StarGrade - 1];
            CharacterService.CreateRecipes(parcelResult, recipeIngredientExcels, ingreRecipe);

            targetCharacter.StarGrade += 1;

            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResult, isConsume: true);
            await context.SaveChangesAsync();

            return (targetCharacter, parcelResolver.ParcelResult);
        }

        public async Task<ParcelResultDB> WeaponGrowth(
            SchaleDataContext context, AccountDBServer account, CharacterWeaponExpGrowthRequest req)
        {
            var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == req.TargetCharacterServerId);
            var weaponCharacter = context.Weapons.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == targetCharacter.UniqueId);

            var charWeaponType = characterExcels.GetCharacter(targetCharacter.UniqueId).WeaponType;
            long addExp = 0;
            List<ParcelResult> parcelResult = [];
            
            foreach (var consumeItem in req.ConsumeUniqueIdAndCounts)
            {
                var equipment = context.Equipments.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == consumeItem.Key);
                var equipmentExcel = equipmentExcels.GetEquipmentExcelById(equipment.UniqueId);
                parcelResult.Add(new ParcelResult(ParcelType.Equipment, equipmentExcel.Id, consumeItem.Value));
                var equipmentStat = equipmentStatExcels.GetEquipmentStatExcelById(equipmentExcel.Id);
                var weaponStat = characterWeaponExpBonusExcels.GetCharacterWeaponExpBonusExcelById(charWeaponType);
                var baseExp = equipmentStat.LevelUpFeedExp * consumeItem.Value;
                var bonusExpCalc = CharacterService.CalculateWeaponBonusExp(equipmentExcel, weaponStat, baseExp);
                addExp += bonusExpCalc;
            }
            
            var calculatedCredit = CharacterService.CalculateWeaponCreditExp(addExp);
            parcelResult.Add(new ParcelResult(ParcelType.Currency, (long)CurrencyTypes.Gold, calculatedCredit));

            var (resultLevel, resultExp) = MathService.CalculateLevelExp(weaponCharacter.Level, weaponCharacter.Exp, addExp, weaponExpLevelDatas);
            weaponCharacter.Level = resultLevel;
            weaponCharacter.Exp = resultExp;

            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResult, isConsume: true);
            parcelResolver.ParcelResult.WeaponDBs.Add(weaponCharacter.ToMap(mapper));
            await context.SaveChangesAsync();

            return parcelResolver.ParcelResult;
        }

        public async Task<ParcelResultDB> WeaponTranscendence(
            SchaleDataContext context, AccountDBServer account, CharacterWeaponTranscendenceRequest req)
        {
            List<ParcelResult> parcelResult = [];
            var targetCharacter = context.Characters.FirstOrDefault(x => x.ServerId == req.TargetCharacterServerId);
            var weapon = context.Weapons.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.UniqueId == targetCharacter.UniqueId);
            var characterWeaponExcel = characterWeaponExcels.GetCharacterWeaponExcelByCharacterId(targetCharacter.UniqueId);
            var ingreRecipe = characterWeaponExcel.RecipeId[weapon.StarGrade - 1];
            CharacterService.CreateRecipes(parcelResult, recipeIngredientExcels, ingreRecipe);

            weapon.StarGrade += 1;

            var parcelResolver = await parcelHandler.BuildParcel(context, account, parcelResult, isConsume: true);
            await context.SaveChangesAsync();
            parcelResolver.ParcelResult.WeaponDBs.Add(weapon.ToMap(mapper));

            return parcelResolver.ParcelResult;
        }
    }
}
