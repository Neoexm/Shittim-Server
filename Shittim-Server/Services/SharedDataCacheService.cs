using BlueArchiveAPI.Services;
using Schale.Excel;
using Schale.FlatData;
using Shittim.GameMasters;

namespace Shittim_Server.Services;

public class SharedDataCacheService
{
    private readonly List<PickupDuplicateBonusExcelT> _charaPickUp;
    private readonly List<CharacterExcelT> _charaList;
    private readonly List<CharacterExcelT> _charaListR;
    private readonly List<CharacterExcelT> _charaListSR;
    private readonly List<CharacterExcelT> _charaListSSR;
    private readonly List<CharacterExcelT> _charaListRNormal;
    private readonly List<CharacterExcelT> _charaListSRNormal;
    private readonly List<CharacterExcelT> _charaListSSRNormal;
    private readonly List<CharacterExcelT> _charaListUnique;
    private readonly List<CharacterExcelT> _charaListEvent;
    private readonly List<CharacterExcelT> _charaListFest;

    public IReadOnlyList<CharacterExcelT> CharaList => _charaList;
    public IReadOnlyList<CharacterExcelT> CharaListR => _charaListR;
    public IReadOnlyList<CharacterExcelT> CharaListSR => _charaListSR;
    public IReadOnlyList<CharacterExcelT> CharaListSSR => _charaListSSR;
    public IReadOnlyList<CharacterExcelT> CharaListRNormal => _charaListRNormal;
    public IReadOnlyList<CharacterExcelT> CharaListSRNormal => _charaListSRNormal;
    public IReadOnlyList<CharacterExcelT> CharaListSSRNormal => _charaListSSRNormal;
    public IReadOnlyList<CharacterExcelT> CharaListUnique => _charaListUnique;
    public IReadOnlyList<CharacterExcelT> CharaListEvent => _charaListEvent;
    public IReadOnlyList<CharacterExcelT> CharaListFest => _charaListFest;

    public SharedDataCacheService(ExcelTableService excelTableService)
    {
        _charaList = excelTableService.GetTable<CharacterExcelT>()
            .GetReleaseCharacters()
            .ToList();

        _charaPickUp = excelTableService.GetTable<PickupDuplicateBonusExcelT>();

        _charaListR = _charaList.Where(x => x.Rarity == Rarity.R).ToList();
        _charaListSR = _charaList.Where(x => x.Rarity == Rarity.SR).ToList();
        _charaListSSR = _charaList.Where(x => x.Rarity == Rarity.SSR).ToList();

        _charaListRNormal = _charaListR.Where(x => GetStudentType(x) == StudentType.Permanent).ToList();
        _charaListSRNormal = _charaListSR.Where(x => GetStudentType(x) == StudentType.Permanent).ToList();
        _charaListSSRNormal = _charaListSSR.Where(x => GetStudentType(x) == StudentType.Permanent).ToList();

        _charaListUnique = _charaListSSR.Where(x => GetStudentType(x) == StudentType.Unique).ToList();
        _charaListEvent = _charaListR.Where(x => GetStudentType(x) == StudentType.Event).ToList();
        _charaListFest = GetFestCharacter(_charaPickUp, _charaListSSR);

        CharacterGM.characterExcel = excelTableService.GetTable<CharacterExcelT>();
        CharacterGM.characterLevelExcel = excelTableService.GetTable<CharacterLevelExcelT>();
        CharacterGM.weaponExcel = excelTableService.GetTable<CharacterWeaponExcelT>();
        CharacterGM.equipmentExcel = excelTableService.GetTable<EquipmentExcelT>();
        CharacterGM.uniqueGearExcel = excelTableService.GetTable<CharacterGearExcelT>();
    }

    public static List<CharacterExcelT> GetFestCharacter(
        List<PickupDuplicateBonusExcelT> pickupDupBonus,
        List<CharacterExcelT> characterExcel)
    {
        return pickupDupBonus
            .Where(x => x.ShopCategoryType == ShopCategoryType.FesGacha)
            .Select(x => characterExcel.First(c => c.Id == x.PickupCharacterId))
            .DistinctBy(x => x.Id).ToList();
    }

    public static StudentType GetStudentType(CharacterExcelT ch)
    {
        if (!ch.CollectionVisibleEndDate.StartsWith("2099"))
        {
            return StudentType.Unique;
        }
        else if (ch.CombineRecipeId == 0)
        {
            return StudentType.Event;
        }

        return StudentType.Permanent;
    }

    public enum StudentType
    {
        Permanent,
        Unique,
        Event,
        Festival
    }
}

public static class DataCacheServiceExtensions
{
    public static void AddSharedDataCache(this IServiceCollection services)
    {
        services.AddSingleton<SharedDataCacheService>();
    }
}
