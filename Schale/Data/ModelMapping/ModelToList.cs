using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.ModelMapping
{
    public static class ModelToList
    {
        // --- Specific Collection Mapping Extensions (for IQueryable/Enumerable sources to Lists/Enumerables) ---
        // These methods materialize the IQueryable into memory before mapping.

        //
        // User Data
        // 
        
        public static List<AccountCurrencyDB> ToMapList(this IQueryable<AccountCurrencyDBServer> source, IMapper mapper) => source.AsNoTracking().ToList().MapInternalEnumerable<AccountCurrencyDBServer, AccountCurrencyDB>(mapper).ToList();
        public static IEnumerable<AccountCurrencyDB> ToMapEnumerable(this IQueryable<AccountCurrencyDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);
        public static List<AccountCurrencyDB> ToMapList(this IEnumerable<AccountCurrencyDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AccountCurrencyDBServer, AccountCurrencyDB>(mapper).ToList();
        public static IEnumerable<AccountCurrencyDB> ToMapEnumerable(this IEnumerable<AccountCurrencyDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);

        public static List<ItemDB> ToMapList(this IQueryable<ItemDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<ItemDBServer, ItemDB>(mapper).ToList();
        public static IEnumerable<ItemDB> ToMapEnumerable(this IQueryable<ItemDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<ItemDBServer, ItemDB>(mapper);
        public static List<ItemDB> ToMapList(this IEnumerable<ItemDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<ItemDBServer, ItemDB>(mapper).ToList();
        public static IEnumerable<ItemDB> ToMapEnumerable(this IEnumerable<ItemDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<ItemDBServer, ItemDB>(mapper);

        public static List<CharacterDB> ToMapList(this IQueryable<CharacterDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CharacterDBServer, CharacterDB>(mapper).ToList();
        public static IEnumerable<CharacterDB> ToMapEnumerable(this IQueryable<CharacterDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CharacterDBServer, CharacterDB>(mapper);
        public static List<CharacterDB> ToMapList(this IEnumerable<CharacterDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CharacterDBServer, CharacterDB>(mapper).ToList();
        public static IEnumerable<CharacterDB> ToMapEnumerable(this IEnumerable<CharacterDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CharacterDBServer, CharacterDB>(mapper);

        public static List<EquipmentDB> ToMapList(this IQueryable<EquipmentDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EquipmentDBServer, EquipmentDB>(mapper).ToList();
        public static IEnumerable<EquipmentDB> ToMapEnumerable(this IQueryable<EquipmentDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EquipmentDBServer, EquipmentDB>(mapper);
        public static List<EquipmentDB> ToMapList(this IEnumerable<EquipmentDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EquipmentDBServer, EquipmentDB>(mapper).ToList();
        public static IEnumerable<EquipmentDB> ToMapEnumerable(this IEnumerable<EquipmentDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EquipmentDBServer, EquipmentDB>(mapper);

        public static List<WeaponDB> ToMapList(this IQueryable<WeaponDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<WeaponDBServer, WeaponDB>(mapper).ToList();
        public static IEnumerable<WeaponDB> ToMapEnumerable(this IQueryable<WeaponDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<WeaponDBServer, WeaponDB>(mapper);
        public static List<WeaponDB> ToMapList(this IEnumerable<WeaponDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<WeaponDBServer, WeaponDB>(mapper).ToList();
        public static IEnumerable<WeaponDB> ToMapEnumerable(this IEnumerable<WeaponDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<WeaponDBServer, WeaponDB>(mapper);

        public static List<GearDB> ToMapList(this IQueryable<GearDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<GearDBServer, GearDB>(mapper).ToList();
        public static IEnumerable<GearDB> ToMapEnumerable(this IQueryable<GearDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<GearDBServer, GearDB>(mapper);
        public static List<GearDB> ToMapList(this IEnumerable<GearDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<GearDBServer, GearDB>(mapper).ToList();
        public static IEnumerable<GearDB> ToMapEnumerable(this IEnumerable<GearDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<GearDBServer, GearDB>(mapper);

        public static List<EchelonDB> ToMapList(this IQueryable<EchelonDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EchelonDBServer, EchelonDB>(mapper).ToList();
        public static IEnumerable<EchelonDB> ToMapEnumerable(this IQueryable<EchelonDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EchelonDBServer, EchelonDB>(mapper);
        public static List<EchelonDB> ToMapList(this IEnumerable<EchelonDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EchelonDBServer, EchelonDB>(mapper).ToList();
        public static IEnumerable<EchelonDB> ToMapEnumerable(this IEnumerable<EchelonDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EchelonDBServer, EchelonDB>(mapper);

        public static List<EchelonPresetDB> ToMapList(this IQueryable<EchelonPresetDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EchelonPresetDBServer, EchelonPresetDB>(mapper).ToList();
        public static IEnumerable<EchelonPresetDB> ToMapEnumerable(this IQueryable<EchelonPresetDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EchelonPresetDBServer, EchelonPresetDB>(mapper);
        public static List<EchelonPresetDB> ToMapList(this IEnumerable<EchelonPresetDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EchelonPresetDBServer, EchelonPresetDB>(mapper).ToList();
        public static IEnumerable<EchelonPresetDB> ToMapEnumerable(this IEnumerable<EchelonPresetDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EchelonPresetDBServer, EchelonPresetDB>(mapper);

        public static List<EchelonPresetGroupDB> ToMapList(this IQueryable<EchelonPresetGroupDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper).ToList();
        public static IEnumerable<EchelonPresetGroupDB> ToMapEnumerable(this IQueryable<EchelonPresetGroupDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);
        public static List<EchelonPresetGroupDB> ToMapList(this IEnumerable<EchelonPresetGroupDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper).ToList();
        public static IEnumerable<EchelonPresetGroupDB> ToMapEnumerable(this IEnumerable<EchelonPresetGroupDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);

        public static List<CafeDB> ToMapList(this IQueryable<CafeDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CafeDBServer, CafeDB>(mapper).ToList();
        public static IEnumerable<CafeDB> ToMapEnumerable(this IQueryable<CafeDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CafeDBServer, CafeDB>(mapper);
        public static List<CafeDB> ToMapList(this IEnumerable<CafeDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CafeDBServer, CafeDB>(mapper).ToList();
        public static IEnumerable<CafeDB> ToMapEnumerable(this IEnumerable<CafeDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CafeDBServer, CafeDB>(mapper);

        public static List<FurnitureDB> ToMapList(this IQueryable<FurnitureDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<FurnitureDBServer, FurnitureDB>(mapper).ToList();
        public static IEnumerable<FurnitureDB> ToMapEnumerable(this IQueryable<FurnitureDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<FurnitureDBServer, FurnitureDB>(mapper);
        public static List<FurnitureDB> ToMapList(this IEnumerable<FurnitureDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<FurnitureDBServer, FurnitureDB>(mapper).ToList();
        public static IEnumerable<FurnitureDB> ToMapEnumerable(this IEnumerable<FurnitureDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<FurnitureDBServer, FurnitureDB>(mapper);

        public static List<MemoryLobbyDB> ToMapList(this IQueryable<MemoryLobbyDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MemoryLobbyDBServer, MemoryLobbyDB>(mapper).ToList();
        public static IEnumerable<MemoryLobbyDB> ToMapEnumerable(this IQueryable<MemoryLobbyDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);
        public static List<MemoryLobbyDB> ToMapList(this IEnumerable<MemoryLobbyDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MemoryLobbyDBServer, MemoryLobbyDB>(mapper).ToList();
        public static IEnumerable<MemoryLobbyDB> ToMapEnumerable(this IEnumerable<MemoryLobbyDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);

        public static List<MailDB> ToMapList(this IQueryable<MailDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MailDBServer, MailDB>(mapper).ToList();
        public static IEnumerable<MailDB> ToMapEnumerable(this IQueryable<MailDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MailDBServer, MailDB>(mapper);
        public static List<MailDB> ToMapList(this IEnumerable<MailDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MailDBServer, MailDB>(mapper).ToList();
        public static IEnumerable<MailDB> ToMapEnumerable(this IEnumerable<MailDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MailDBServer, MailDB>(mapper);

        public static List<EmblemDB> ToMapList(this IQueryable<EmblemDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EmblemDBServer, EmblemDB>(mapper).ToList();
        public static IEnumerable<EmblemDB> ToMapEnumerable(this IQueryable<EmblemDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EmblemDBServer, EmblemDB>(mapper);
        public static List<EmblemDB> ToMapList(this IEnumerable<EmblemDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EmblemDBServer, EmblemDB>(mapper).ToList();
        public static IEnumerable<EmblemDB> ToMapEnumerable(this IEnumerable<EmblemDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EmblemDBServer, EmblemDB>(mapper);

        public static List<IdCardBackgroundDB> ToMapList(this IQueryable<IdCardBackgroundDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper).ToList();
        public static IEnumerable<IdCardBackgroundDB> ToMapEnumerable(this IQueryable<IdCardBackgroundDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);
        public static List<IdCardBackgroundDB> ToMapList(this IEnumerable<IdCardBackgroundDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper).ToList();
        public static IEnumerable<IdCardBackgroundDB> ToMapEnumerable(this IEnumerable<IdCardBackgroundDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);

        public static List<CostumeDB> ToMapList(this IQueryable<CostumeDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CostumeDBServer, CostumeDB>(mapper).ToList();
        public static IEnumerable<CostumeDB> ToMapEnumerable(this IQueryable<CostumeDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CostumeDBServer, CostumeDB>(mapper);
        public static List<CostumeDB> ToMapList(this IEnumerable<CostumeDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CostumeDBServer, CostumeDB>(mapper).ToList();
        public static IEnumerable<CostumeDB> ToMapEnumerable(this IEnumerable<CostumeDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CostumeDBServer, CostumeDB>(mapper);

        public static List<StickerDB> ToMapList(this IQueryable<StickerDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<StickerDBServer, StickerDB>(mapper).ToList();
        public static IEnumerable<StickerDB> ToMapEnumerable(this IQueryable<StickerDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<StickerDBServer, StickerDB>(mapper);
        public static List<StickerDB> ToMapList(this IEnumerable<StickerDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<StickerDBServer, StickerDB>(mapper).ToList();
        public static IEnumerable<StickerDB> ToMapEnumerable(this IEnumerable<StickerDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<StickerDBServer, StickerDB>(mapper);
    
        public static List<AccountAttachmentDB> ToMapList(this IQueryable<AccountAttachmentDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AccountAttachmentDBServer, AccountAttachmentDB>(mapper).ToList();
        public static IEnumerable<AccountAttachmentDB> ToMapEnumerable(this IQueryable<AccountAttachmentDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);
        public static List<AccountAttachmentDB> ToMapList(this IEnumerable<AccountAttachmentDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AccountAttachmentDBServer, AccountAttachmentDB>(mapper).ToList();
        public static IEnumerable<AccountAttachmentDB> ToMapEnumerable(this IEnumerable<AccountAttachmentDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);
    
        public static List<AccountLevelRewardDB> ToMapList(this IQueryable<AccountLevelRewardDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper).ToList();
        public static IEnumerable<AccountLevelRewardDB> ToMapEnumerable(this IQueryable<AccountLevelRewardDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);
        public static List<AccountLevelRewardDB> ToMapList(this IEnumerable<AccountLevelRewardDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper).ToList();
        public static IEnumerable<AccountLevelRewardDB> ToMapEnumerable(this IEnumerable<AccountLevelRewardDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);

        //
        // Progress Data
        // 

        public static List<MissionProgressDB> ToMapList(this IQueryable<MissionProgressDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MissionProgressDBServer, MissionProgressDB>(mapper).ToList();
        public static IEnumerable<MissionProgressDB> ToMapEnumerable(this IQueryable<MissionProgressDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MissionProgressDBServer, MissionProgressDB>(mapper);
        public static List<MissionProgressDB> ToMapList(this IEnumerable<MissionProgressDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MissionProgressDBServer, MissionProgressDB>(mapper).ToList();
        public static IEnumerable<MissionProgressDB> ToMapEnumerable(this IEnumerable<MissionProgressDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MissionProgressDBServer, MissionProgressDB>(mapper);

        public static List<AttendanceHistoryDB> ToMapList(this IQueryable<AttendanceHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper).ToList();
        public static IEnumerable<AttendanceHistoryDB> ToMapEnumerable(this IQueryable<AttendanceHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);
        public static List<AttendanceHistoryDB> ToMapList(this IEnumerable<AttendanceHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper).ToList();
        public static IEnumerable<AttendanceHistoryDB> ToMapEnumerable(this IEnumerable<AttendanceHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);

        public static List<AcademyDB> ToMapList(this IQueryable<AcademyDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AcademyDBServer, AcademyDB>(mapper).ToList();
        public static IEnumerable<AcademyDB> ToMapEnumerable(this IQueryable<AcademyDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AcademyDBServer, AcademyDB>(mapper);
        public static List<AcademyDB> ToMapList(this IEnumerable<AcademyDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AcademyDBServer, AcademyDB>(mapper).ToList();
        public static IEnumerable<AcademyDB> ToMapEnumerable(this IEnumerable<AcademyDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AcademyDBServer, AcademyDB>(mapper);

        public static List<AcademyLocationDB> ToMapList(this IQueryable<AcademyLocationDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AcademyLocationDBServer, AcademyLocationDB>(mapper).ToList();
        public static IEnumerable<AcademyLocationDB> ToMapEnumerable(this IQueryable<AcademyLocationDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AcademyLocationDBServer, AcademyLocationDB>(mapper);
        public static List<AcademyLocationDB> ToMapList(this IEnumerable<AcademyLocationDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<AcademyLocationDBServer, AcademyLocationDB>(mapper).ToList();
        public static IEnumerable<AcademyLocationDB> ToMapEnumerable(this IEnumerable<AcademyLocationDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<AcademyLocationDBServer, AcademyLocationDB>(mapper);

        public static List<CampaignMainStageSaveDB> ToMapList(this IQueryable<CampaignMainStageSaveDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper).ToList();
        public static IEnumerable<CampaignMainStageSaveDB> ToMapEnumerable(this IQueryable<CampaignMainStageSaveDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);
        public static List<CampaignMainStageSaveDB> ToMapList(this IEnumerable<CampaignMainStageSaveDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper).ToList();
        public static IEnumerable<CampaignMainStageSaveDB> ToMapEnumerable(this IEnumerable<CampaignMainStageSaveDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);

        public static List<CampaignChapterClearRewardHistoryDB> ToMapList(this IQueryable<CampaignChapterClearRewardHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper).ToList();
        public static IEnumerable<CampaignChapterClearRewardHistoryDB> ToMapEnumerable(this IQueryable<CampaignChapterClearRewardHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);
        public static List<CampaignChapterClearRewardHistoryDB> ToMapList(this IEnumerable<CampaignChapterClearRewardHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper).ToList();
        public static IEnumerable<CampaignChapterClearRewardHistoryDB> ToMapEnumerable(this IEnumerable<CampaignChapterClearRewardHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);

        public static List<StrategyObjectHistoryDB> ToMapList(this IQueryable<StrategyObjectHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper).ToList();
        public static IEnumerable<StrategyObjectHistoryDB> ToMapEnumerable(this IQueryable<StrategyObjectHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);
        public static List<StrategyObjectHistoryDB> ToMapList(this IEnumerable<StrategyObjectHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper).ToList();
        public static IEnumerable<StrategyObjectHistoryDB> ToMapEnumerable(this IEnumerable<StrategyObjectHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);

        public static List<ScenarioHistoryDB> ToMapList(this IQueryable<ScenarioHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper).ToList();
        public static IEnumerable<ScenarioHistoryDB> ToMapEnumerable(this IQueryable<ScenarioHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);
        public static List<ScenarioHistoryDB> ToMapList(this IEnumerable<ScenarioHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper).ToList();
        public static IEnumerable<ScenarioHistoryDB> ToMapEnumerable(this IEnumerable<ScenarioHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);

        public static List<ScenarioGroupHistoryDB> ToMapList(this IQueryable<ScenarioGroupHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper).ToList();
        public static IEnumerable<ScenarioGroupHistoryDB> ToMapEnumerable(this IQueryable<ScenarioGroupHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);
        public static List<ScenarioGroupHistoryDB> ToMapList(this IEnumerable<ScenarioGroupHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper).ToList();
        public static IEnumerable<ScenarioGroupHistoryDB> ToMapEnumerable(this IEnumerable<ScenarioGroupHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);

        public static List<CampaignStageHistoryDB> ToMapList(this IQueryable<CampaignStageHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper).ToList();
        public static IEnumerable<CampaignStageHistoryDB> ToMapEnumerable(this IQueryable<CampaignStageHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);
        public static List<CampaignStageHistoryDB> ToMapList(this IEnumerable<CampaignStageHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper).ToList();
        public static IEnumerable<CampaignStageHistoryDB> ToMapEnumerable(this IEnumerable<CampaignStageHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);

        public static List<WeekDungeonStageHistoryDB> ToMapList(this IQueryable<WeekDungeonStageHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper).ToList();
        public static IEnumerable<WeekDungeonStageHistoryDB> ToMapEnumerable(this IQueryable<WeekDungeonStageHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);
        public static List<WeekDungeonStageHistoryDB> ToMapList(this IEnumerable<WeekDungeonStageHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper).ToList();
        public static IEnumerable<WeekDungeonStageHistoryDB> ToMapEnumerable(this IEnumerable<WeekDungeonStageHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);

        public static List<SchoolDungeonStageHistoryDB> ToMapList(this IQueryable<SchoolDungeonStageHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<SchoolDungeonStageHistoryDBServer, SchoolDungeonStageHistoryDB>(mapper).ToList();
        public static IEnumerable<SchoolDungeonStageHistoryDB> ToMapEnumerable(this IQueryable<SchoolDungeonStageHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<SchoolDungeonStageHistoryDBServer, SchoolDungeonStageHistoryDB>(mapper);
        public static List<SchoolDungeonStageHistoryDB> ToMapList(this IEnumerable<SchoolDungeonStageHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<SchoolDungeonStageHistoryDBServer, SchoolDungeonStageHistoryDB>(mapper).ToList();
        public static IEnumerable<SchoolDungeonStageHistoryDB> ToMapEnumerable(this IEnumerable<SchoolDungeonStageHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<SchoolDungeonStageHistoryDBServer, SchoolDungeonStageHistoryDB>(mapper);

        public static List<MomoTalkOutLineDB> ToMapList(this IQueryable<MomoTalkOutLineDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper).ToList();
        public static IEnumerable<MomoTalkOutLineDB> ToMapEnumerable(this IQueryable<MomoTalkOutLineDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);
        public static List<MomoTalkOutLineDB> ToMapList(this IEnumerable<MomoTalkOutLineDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper).ToList();
        public static IEnumerable<MomoTalkOutLineDB> ToMapEnumerable(this IEnumerable<MomoTalkOutLineDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);

        public static List<MomoTalkChoiceDB> ToMapList(this IQueryable<MomoTalkChoiceDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper).ToList();
        public static IEnumerable<MomoTalkChoiceDB> ToMapEnumerable(this IQueryable<MomoTalkChoiceDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);
        public static List<MomoTalkChoiceDB> ToMapList(this IEnumerable<MomoTalkChoiceDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper).ToList();
        public static IEnumerable<MomoTalkChoiceDB> ToMapEnumerable(this IEnumerable<MomoTalkChoiceDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);

        public static List<EventContentPermanentDB> ToMapList(this IQueryable<EventContentPermanentDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EventContentPermanentDBServer, EventContentPermanentDB>(mapper).ToList();
        public static IEnumerable<EventContentPermanentDB> ToMapEnumerable(this IQueryable<EventContentPermanentDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);
        public static List<EventContentPermanentDB> ToMapList(this IEnumerable<EventContentPermanentDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EventContentPermanentDBServer, EventContentPermanentDB>(mapper).ToList();
        public static IEnumerable<EventContentPermanentDB> ToMapEnumerable(this IEnumerable<EventContentPermanentDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);

        public static List<StickerBookDB> ToMapList(this IQueryable<StickerBookDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<StickerBookDBServer, StickerBookDB>(mapper).ToList();
        public static IEnumerable<StickerBookDB> ToMapEnumerable(this IQueryable<StickerBookDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<StickerBookDBServer, StickerBookDB>(mapper);
        public static List<StickerBookDB> ToMapList(this IEnumerable<StickerBookDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<StickerBookDBServer, StickerBookDB>(mapper).ToList();
        public static IEnumerable<StickerBookDB> ToMapEnumerable(this IEnumerable<StickerBookDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<StickerBookDBServer, StickerBookDB>(mapper);

        //
        // Content Data
        // 

        public static List<SingleRaidLobbyInfoDB> ToMapList(this IQueryable<SingleRaidLobbyInfoDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper).ToList();
        public static IEnumerable<SingleRaidLobbyInfoDB> ToMapEnumerable(this IQueryable<SingleRaidLobbyInfoDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);
        public static List<SingleRaidLobbyInfoDB> ToMapList(this IEnumerable<SingleRaidLobbyInfoDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper).ToList();
        public static IEnumerable<SingleRaidLobbyInfoDB> ToMapEnumerable(this IEnumerable<SingleRaidLobbyInfoDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);

        public static List<EliminateRaidLobbyInfoDB> ToMapList(this IQueryable<EliminateRaidLobbyInfoDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper).ToList();
        public static IEnumerable<EliminateRaidLobbyInfoDB> ToMapEnumerable(this IQueryable<EliminateRaidLobbyInfoDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);
        public static List<EliminateRaidLobbyInfoDB> ToMapList(this IEnumerable<EliminateRaidLobbyInfoDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper).ToList();
        public static IEnumerable<EliminateRaidLobbyInfoDB> ToMapEnumerable(this IEnumerable<EliminateRaidLobbyInfoDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);

        public static List<RaidDB> ToMapList(this IQueryable<RaidDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<RaidDBServer, RaidDB>(mapper).ToList();
        public static IEnumerable<RaidDB> ToMapEnumerable(this IQueryable<RaidDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<RaidDBServer, RaidDB>(mapper);
        public static List<RaidDB> ToMapList(this IEnumerable<RaidDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<RaidDBServer, RaidDB>(mapper).ToList();
        public static IEnumerable<RaidDB> ToMapEnumerable(this IEnumerable<RaidDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<RaidDBServer, RaidDB>(mapper);

        public static List<RaidBattleDB> ToMapList(this IQueryable<RaidBattleDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<RaidBattleDBServer, RaidBattleDB>(mapper).ToList();
        public static IEnumerable<RaidBattleDB> ToMapEnumerable(this IQueryable<RaidBattleDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<RaidBattleDBServer, RaidBattleDB>(mapper);
        public static List<RaidBattleDB> ToMapList(this IEnumerable<RaidBattleDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<RaidBattleDBServer, RaidBattleDB>(mapper).ToList();
        public static IEnumerable<RaidBattleDB> ToMapEnumerable(this IEnumerable<RaidBattleDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<RaidBattleDBServer, RaidBattleDB>(mapper);

        public static List<TimeAttackDungeonRoomDB> ToMapList(this IQueryable<TimeAttackDungeonRoomDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper).ToList();
        public static IEnumerable<TimeAttackDungeonRoomDB> ToMapEnumerable(this IQueryable<TimeAttackDungeonRoomDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);
        public static List<TimeAttackDungeonRoomDB> ToMapList(this IEnumerable<TimeAttackDungeonRoomDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper).ToList();
        public static IEnumerable<TimeAttackDungeonRoomDB> ToMapEnumerable(this IEnumerable<TimeAttackDungeonRoomDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);

        public static List<TimeAttackDungeonBattleHistoryDB> ToMapList(this IQueryable<TimeAttackDungeonBattleHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper).ToList();
        public static IEnumerable<TimeAttackDungeonBattleHistoryDB> ToMapEnumerable(this IQueryable<TimeAttackDungeonBattleHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);
        public static List<TimeAttackDungeonBattleHistoryDB> ToMapList(this IEnumerable<TimeAttackDungeonBattleHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper).ToList();
        public static IEnumerable<TimeAttackDungeonBattleHistoryDB> ToMapEnumerable(this IEnumerable<TimeAttackDungeonBattleHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);

        public static List<MultiFloorRaidDB> ToMapList(this IQueryable<MultiFloorRaidDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper).ToList();
        public static IEnumerable<MultiFloorRaidDB> ToMapEnumerable(this IQueryable<MultiFloorRaidDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);
        public static List<MultiFloorRaidDB> ToMapList(this IEnumerable<MultiFloorRaidDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper).ToList();
        public static IEnumerable<MultiFloorRaidDB> ToMapEnumerable(this IEnumerable<MultiFloorRaidDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);

        public static List<WorldRaidLocalBossDB> ToMapList(this IQueryable<WorldRaidLocalBossDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper).ToList();
        public static IEnumerable<WorldRaidLocalBossDB> ToMapEnumerable(this IQueryable<WorldRaidLocalBossDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);
        public static List<WorldRaidLocalBossDB> ToMapList(this IEnumerable<WorldRaidLocalBossDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper).ToList();
        public static IEnumerable<WorldRaidLocalBossDB> ToMapEnumerable(this IEnumerable<WorldRaidLocalBossDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);
        
        public static List<WorldRaidBossListInfoDB> ToMapList(this IQueryable<WorldRaidBossListInfoDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper).ToList();
        public static IEnumerable<WorldRaidBossListInfoDB> ToMapEnumerable(this IQueryable<WorldRaidBossListInfoDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);
        public static List<WorldRaidBossListInfoDB> ToMapList(this IEnumerable<WorldRaidBossListInfoDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper).ToList();
        public static IEnumerable<WorldRaidBossListInfoDB> ToMapEnumerable(this IEnumerable<WorldRaidBossListInfoDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);

        public static List<WorldRaidClearHistoryDB> ToMapList(this IQueryable<WorldRaidClearHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper).ToList();
        public static IEnumerable<WorldRaidClearHistoryDB> ToMapEnumerable(this IQueryable<WorldRaidClearHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);
        public static List<WorldRaidClearHistoryDB> ToMapList(this IEnumerable<WorldRaidClearHistoryDBServer> source, IMapper mapper) => source.ToList().MapInternalEnumerable<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper).ToList();
        public static IEnumerable<WorldRaidClearHistoryDB> ToMapEnumerable(this IEnumerable<WorldRaidClearHistoryDBServer> source, IMapper mapper) => source.AsEnumerable().MapInternalEnumerable<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);
    }
}


