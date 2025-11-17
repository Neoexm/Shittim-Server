using System.Linq.Expressions;
using AutoMapper;
using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.ModelMapping
{
    public static class ModelToFirst
    {
        #region IQueryable Mappings

        // --- Specific FirstOrDefault / First Extensions (for IQueryable sources) ---
        // These methods perform FirstOrDefault/First on the database and then map the single result.

        //
        // User Data
        // 

        public static AccountCurrencyDB? FirstOrDefaultMapTo(this IQueryable<AccountCurrencyDBServer> source, Expression<Func<AccountCurrencyDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);
        public static AccountCurrencyDB? FirstOrDefaultMapTo(this IQueryable<AccountCurrencyDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);
        public static AccountCurrencyDB? FirstMapTo(this IQueryable<AccountCurrencyDBServer> source, Expression<Func<AccountCurrencyDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);
        public static AccountCurrencyDB? FirstMapTo(this IQueryable<AccountCurrencyDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);

        public static ItemDB? FirstOrDefaultMapTo(this IQueryable<ItemDBServer> source, Expression<Func<ItemDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<ItemDBServer, ItemDB>(mapper);
        public static ItemDB? FirstOrDefaultMapTo(this IQueryable<ItemDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<ItemDBServer, ItemDB>(mapper);
        public static ItemDB? FirstMapTo(this IQueryable<ItemDBServer> source, Expression<Func<ItemDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<ItemDBServer, ItemDB>(mapper);
        public static ItemDB? FirstMapTo(this IQueryable<ItemDBServer> source, IMapper mapper) => source.First().MapInternalSingle<ItemDBServer, ItemDB>(mapper);

        public static CharacterDB? FirstOrDefaultMapTo(this IQueryable<CharacterDBServer> source, Expression<Func<CharacterDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CharacterDBServer, CharacterDB>(mapper);
        public static CharacterDB? FirstOrDefaultMapTo(this IQueryable<CharacterDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CharacterDBServer, CharacterDB>(mapper);
        public static CharacterDB? FirstMapTo(this IQueryable<CharacterDBServer> source, Expression<Func<CharacterDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CharacterDBServer, CharacterDB>(mapper);
        public static CharacterDB? FirstMapTo(this IQueryable<CharacterDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CharacterDBServer, CharacterDB>(mapper);

        public static EquipmentDB? FirstOrDefaultMapTo(this IQueryable<EquipmentDBServer> source, Expression<Func<EquipmentDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EquipmentDBServer, EquipmentDB>(mapper);
        public static EquipmentDB? FirstOrDefaultMapTo(this IQueryable<EquipmentDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EquipmentDBServer, EquipmentDB>(mapper);
        public static EquipmentDB? FirstMapTo(this IQueryable<EquipmentDBServer> source, Expression<Func<EquipmentDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EquipmentDBServer, EquipmentDB>(mapper);
        public static EquipmentDB? FirstMapTo(this IQueryable<EquipmentDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EquipmentDBServer, EquipmentDB>(mapper);

        public static WeaponDB? FirstOrDefaultMapTo(this IQueryable<WeaponDBServer> source, Expression<Func<WeaponDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<WeaponDBServer, WeaponDB>(mapper);
        public static WeaponDB? FirstOrDefaultMapTo(this IQueryable<WeaponDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<WeaponDBServer, WeaponDB>(mapper);
        public static WeaponDB? FirstMapTo(this IQueryable<WeaponDBServer> source, Expression<Func<WeaponDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<WeaponDBServer, WeaponDB>(mapper);
        public static WeaponDB? FirstMapTo(this IQueryable<WeaponDBServer> source, IMapper mapper) => source.First().MapInternalSingle<WeaponDBServer, WeaponDB>(mapper);

        public static GearDB? FirstOrDefaultMapTo(this IQueryable<GearDBServer> source, Expression<Func<GearDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<GearDBServer, GearDB>(mapper);
        public static GearDB? FirstOrDefaultMapTo(this IQueryable<GearDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<GearDBServer, GearDB>(mapper);
        public static GearDB? FirstMapTo(this IQueryable<GearDBServer> source, Expression<Func<GearDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<GearDBServer, GearDB>(mapper);
        public static GearDB? FirstMapTo(this IQueryable<GearDBServer> source, IMapper mapper) => source.First().MapInternalSingle<GearDBServer, GearDB>(mapper);

        public static EchelonDB? FirstOrDefaultMapTo(this IQueryable<EchelonDBServer> source, Expression<Func<EchelonDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EchelonDBServer, EchelonDB>(mapper);
        public static EchelonDB? FirstOrDefaultMapTo(this IQueryable<EchelonDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EchelonDBServer, EchelonDB>(mapper);
        public static EchelonDB? FirstMapTo(this IQueryable<EchelonDBServer> source, Expression<Func<EchelonDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EchelonDBServer, EchelonDB>(mapper);
        public static EchelonDB? FirstMapTo(this IQueryable<EchelonDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EchelonDBServer, EchelonDB>(mapper);

        public static EchelonPresetDB? FirstOrDefaultMapTo(this IQueryable<EchelonPresetDBServer> source, Expression<Func<EchelonPresetDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EchelonPresetDBServer, EchelonPresetDB>(mapper);
        public static EchelonPresetDB? FirstOrDefaultMapTo(this IQueryable<EchelonPresetDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EchelonPresetDBServer, EchelonPresetDB>(mapper);
        public static EchelonPresetDB? FirstMapTo(this IQueryable<EchelonPresetDBServer> source, Expression<Func<EchelonPresetDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EchelonPresetDBServer, EchelonPresetDB>(mapper);
        public static EchelonPresetDB? FirstMapTo(this IQueryable<EchelonPresetDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EchelonPresetDBServer, EchelonPresetDB>(mapper);

        public static EchelonPresetGroupDB? FirstOrDefaultMapTo(this IQueryable<EchelonPresetGroupDBServer> source, Expression<Func<EchelonPresetGroupDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);
        public static EchelonPresetGroupDB? FirstOrDefaultMapTo(this IQueryable<EchelonPresetGroupDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);
        public static EchelonPresetGroupDB? FirstMapTo(this IQueryable<EchelonPresetGroupDBServer> source, Expression<Func<EchelonPresetGroupDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);
        public static EchelonPresetGroupDB? FirstMapTo(this IQueryable<EchelonPresetGroupDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);


        public static CafeDB? FirstOrDefaultMapTo(this IQueryable<CafeDBServer> source, Expression<Func<CafeDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CafeDBServer, CafeDB>(mapper);
        public static CafeDB? FirstOrDefaultMapTo(this IQueryable<CafeDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CafeDBServer, CafeDB>(mapper);
        public static CafeDB? FirstMapTo(this IQueryable<CafeDBServer> source, Expression<Func<CafeDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CafeDBServer, CafeDB>(mapper);
        public static CafeDB? FirstMapTo(this IQueryable<CafeDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CafeDBServer, CafeDB>(mapper);

        public static FurnitureDB? FirstOrDefaultMapTo(this IQueryable<FurnitureDBServer> source, Expression<Func<FurnitureDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<FurnitureDBServer, FurnitureDB>(mapper);
        public static FurnitureDB? FirstOrDefaultMapTo(this IQueryable<FurnitureDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<FurnitureDBServer, FurnitureDB>(mapper);
        public static FurnitureDB? FirstMapTo(this IQueryable<FurnitureDBServer> source, Expression<Func<FurnitureDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<FurnitureDBServer, FurnitureDB>(mapper);
        public static FurnitureDB? FirstMapTo(this IQueryable<FurnitureDBServer> source, IMapper mapper) => source.First().MapInternalSingle<FurnitureDBServer, FurnitureDB>(mapper);

        public static MemoryLobbyDB? FirstOrDefaultMapTo(this IQueryable<MemoryLobbyDBServer> source, Expression<Func<MemoryLobbyDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);
        public static MemoryLobbyDB? FirstOrDefaultMapTo(this IQueryable<MemoryLobbyDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);
        public static MemoryLobbyDB? FirstMapTo(this IQueryable<MemoryLobbyDBServer> source, Expression<Func<MemoryLobbyDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);
        public static MemoryLobbyDB? FirstMapTo(this IQueryable<MemoryLobbyDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);

        public static MailDB? FirstOrDefaultMapTo(this IQueryable<MailDBServer> source, Expression<Func<MailDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MailDBServer, MailDB>(mapper);
        public static MailDB? FirstOrDefaultMapTo(this IQueryable<MailDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MailDBServer, MailDB>(mapper);
        public static MailDB? FirstMapTo(this IQueryable<MailDBServer> source, Expression<Func<MailDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MailDBServer, MailDB>(mapper);
        public static MailDB? FirstMapTo(this IQueryable<MailDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MailDBServer, MailDB>(mapper);

        public static EmblemDB? FirstOrDefaultMapTo(this IQueryable<EmblemDBServer> source, Expression<Func<EmblemDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EmblemDBServer, EmblemDB>(mapper);
        public static EmblemDB? FirstOrDefaultMapTo(this IQueryable<EmblemDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EmblemDBServer, EmblemDB>(mapper);
        public static EmblemDB? FirstMapTo(this IQueryable<EmblemDBServer> source, Expression<Func<EmblemDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EmblemDBServer, EmblemDB>(mapper);
        public static EmblemDB? FirstMapTo(this IQueryable<EmblemDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EmblemDBServer, EmblemDB>(mapper);

        public static IdCardBackgroundDB? FirstOrDefaultMapTo(this IQueryable<IdCardBackgroundDBServer> source, Expression<Func<IdCardBackgroundDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);
        public static IdCardBackgroundDB? FirstOrDefaultMapTo(this IQueryable<IdCardBackgroundDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);
        public static IdCardBackgroundDB? FirstMapTo(this IQueryable<IdCardBackgroundDBServer> source, Expression<Func<IdCardBackgroundDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);
        public static IdCardBackgroundDB? FirstMapTo(this IQueryable<IdCardBackgroundDBServer> source, IMapper mapper) => source.First().MapInternalSingle<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);

        public static CostumeDB? FirstOrDefaultMapTo(this IQueryable<CostumeDBServer> source, Expression<Func<CostumeDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CostumeDBServer, CostumeDB>(mapper);
        public static CostumeDB? FirstOrDefaultMapTo(this IQueryable<CostumeDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CostumeDBServer, CostumeDB>(mapper);
        public static CostumeDB? FirstMapTo(this IQueryable<CostumeDBServer> source, Expression<Func<CostumeDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CostumeDBServer, CostumeDB>(mapper);
        public static CostumeDB? FirstMapTo(this IQueryable<CostumeDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CostumeDBServer, CostumeDB>(mapper);

        public static StickerDB? FirstOrDefaultMapTo(this IQueryable<StickerDBServer> source, Expression<Func<StickerDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<StickerDBServer, StickerDB>(mapper);
        public static StickerDB? FirstOrDefaultMapTo(this IQueryable<StickerDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<StickerDBServer, StickerDB>(mapper);
        public static StickerDB? FirstMapTo(this IQueryable<StickerDBServer> source, Expression<Func<StickerDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<StickerDBServer, StickerDB>(mapper);
        public static StickerDB? FirstMapTo(this IQueryable<StickerDBServer> source, IMapper mapper) => source.First().MapInternalSingle<StickerDBServer, StickerDB>(mapper);

        public static AccountAttachmentDB? FirstOrDefaultMapTo(this IQueryable<AccountAttachmentDBServer> source, Expression<Func<AccountAttachmentDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);
        public static AccountAttachmentDB? FirstOrDefaultMapTo(this IQueryable<AccountAttachmentDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);
        public static AccountAttachmentDB? FirstMapTo(this IQueryable<AccountAttachmentDBServer> source, Expression<Func<AccountAttachmentDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);
        public static AccountAttachmentDB? FirstMapTo(this IQueryable<AccountAttachmentDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);

        public static AccountLevelRewardDB? FirstOrDefaultMapTo(this IQueryable<AccountLevelRewardDBServer> source, Expression<Func<AccountLevelRewardDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);
        public static AccountLevelRewardDB? FirstOrDefaultMapTo(this IQueryable<AccountLevelRewardDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);
        public static AccountLevelRewardDB? FirstMapTo(this IQueryable<AccountLevelRewardDBServer> source, Expression<Func<AccountLevelRewardDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);
        public static AccountLevelRewardDB? FirstMapTo(this IQueryable<AccountLevelRewardDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);

        //
        // Progress Data
        // 

        public static MissionProgressDB? FirstOrDefaultMapTo(this IQueryable<MissionProgressDBServer> source, Expression<Func<MissionProgressDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MissionProgressDBServer, MissionProgressDB>(mapper);
        public static MissionProgressDB? FirstOrDefaultMapTo(this IQueryable<MissionProgressDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MissionProgressDBServer, MissionProgressDB>(mapper);
        public static MissionProgressDB? FirstMapTo(this IQueryable<MissionProgressDBServer> source, Expression<Func<MissionProgressDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MissionProgressDBServer, MissionProgressDB>(mapper);
        public static MissionProgressDB? FirstMapTo(this IQueryable<MissionProgressDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MissionProgressDBServer, MissionProgressDB>(mapper);

        public static AttendanceHistoryDB? FirstOrDefaultMapTo(this IQueryable<AttendanceHistoryDBServer> source, Expression<Func<AttendanceHistoryDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);
        public static AttendanceHistoryDB? FirstOrDefaultMapTo(this IQueryable<AttendanceHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);
        public static AttendanceHistoryDB? FirstMapTo(this IQueryable<AttendanceHistoryDBServer> source, Expression<Func<AttendanceHistoryDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);
        public static AttendanceHistoryDB? FirstMapTo(this IQueryable<AttendanceHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);

        public static AcademyDB? FirstOrDefaultMapTo(this IQueryable<AcademyDBServer> source, Expression<Func<AcademyDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AcademyDBServer, AcademyDB>(mapper);
        public static AcademyDB? FirstOrDefaultMapTo(this IQueryable<AcademyDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AcademyDBServer, AcademyDB>(mapper);
        public static AcademyDB? FirstMapTo(this IQueryable<AcademyDBServer> source, Expression<Func<AcademyDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AcademyDBServer, AcademyDB>(mapper);
        public static AcademyDB? FirstMapTo(this IQueryable<AcademyDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AcademyDBServer, AcademyDB>(mapper);

        public static AcademyLocationDB? FirstOrDefaultMapTo(this IQueryable<AcademyLocationDBServer> source, Expression<Func<AcademyLocationDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AcademyLocationDBServer, AcademyLocationDB>(mapper);
        public static AcademyLocationDB? FirstOrDefaultMapTo(this IQueryable<AcademyLocationDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AcademyLocationDBServer, AcademyLocationDB>(mapper);
        public static AcademyLocationDB? FirstMapTo(this IQueryable<AcademyLocationDBServer> source, Expression<Func<AcademyLocationDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AcademyLocationDBServer, AcademyLocationDB>(mapper);
        public static AcademyLocationDB? FirstMapTo(this IQueryable<AcademyLocationDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AcademyLocationDBServer, AcademyLocationDB>(mapper);

        public static CampaignMainStageSaveDB? FirstOrDefaultMapTo(this IQueryable<CampaignMainStageSaveDBServer> source, Expression<Func<CampaignMainStageSaveDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);
        public static CampaignMainStageSaveDB? FirstOrDefaultMapTo(this IQueryable<CampaignMainStageSaveDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);
        public static CampaignMainStageSaveDB? FirstMapTo(this IQueryable<CampaignMainStageSaveDBServer> source, Expression<Func<CampaignMainStageSaveDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);
        public static CampaignMainStageSaveDB? FirstMapTo(this IQueryable<CampaignMainStageSaveDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);

        public static CampaignChapterClearRewardHistoryDB? FirstOrDefaultMapTo(this IQueryable<CampaignChapterClearRewardHistoryDBServer> source, Expression<Func<CampaignChapterClearRewardHistoryDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);
        public static CampaignChapterClearRewardHistoryDB? FirstOrDefaultMapTo(this IQueryable<CampaignChapterClearRewardHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);
        public static CampaignChapterClearRewardHistoryDB? FirstMapTo(this IQueryable<CampaignChapterClearRewardHistoryDBServer> source, Expression<Func<CampaignChapterClearRewardHistoryDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);
        public static CampaignChapterClearRewardHistoryDB? FirstMapTo(this IQueryable<CampaignChapterClearRewardHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);

        public static StrategyObjectHistoryDB? FirstOrDefaultMapTo(this IQueryable<StrategyObjectHistoryDBServer> source, Expression<Func<StrategyObjectHistoryDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);
        public static StrategyObjectHistoryDB? FirstOrDefaultMapTo(this IQueryable<StrategyObjectHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);
        public static StrategyObjectHistoryDB? FirstMapTo(this IQueryable<StrategyObjectHistoryDBServer> source, Expression<Func<StrategyObjectHistoryDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);
        public static StrategyObjectHistoryDB? FirstMapTo(this IQueryable<StrategyObjectHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);

        public static ScenarioHistoryDB? FirstOrDefaultMapTo(this IQueryable<ScenarioHistoryDBServer> source, Expression<Func<ScenarioHistoryDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);
        public static ScenarioHistoryDB? FirstOrDefaultMapTo(this IQueryable<ScenarioHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);
        public static ScenarioHistoryDB? FirstMapTo(this IQueryable<ScenarioHistoryDBServer> source, Expression<Func<ScenarioHistoryDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);
        public static ScenarioHistoryDB? FirstMapTo(this IQueryable<ScenarioHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);

        public static ScenarioGroupHistoryDB? FirstOrDefaultMapTo(this IQueryable<ScenarioGroupHistoryDBServer> source, Expression<Func<ScenarioGroupHistoryDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);
        public static ScenarioGroupHistoryDB? FirstOrDefaultMapTo(this IQueryable<ScenarioGroupHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);
        public static ScenarioGroupHistoryDB? FirstMapTo(this IQueryable<ScenarioGroupHistoryDBServer> source, Expression<Func<ScenarioGroupHistoryDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);
        public static ScenarioGroupHistoryDB? FirstMapTo(this IQueryable<ScenarioGroupHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);

        public static CampaignStageHistoryDB? FirstOrDefaultMapTo(this IQueryable<CampaignStageHistoryDBServer> source, Expression<Func<CampaignStageHistoryDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);
        public static CampaignStageHistoryDB? FirstOrDefaultMapTo(this IQueryable<CampaignStageHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);
        public static CampaignStageHistoryDB? FirstMapTo(this IQueryable<CampaignStageHistoryDBServer> source, Expression<Func<CampaignStageHistoryDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);
        public static CampaignStageHistoryDB? FirstMapTo(this IQueryable<CampaignStageHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);

        public static WeekDungeonStageHistoryDB? FirstOrDefaultMapTo(this IQueryable<WeekDungeonStageHistoryDBServer> source, Expression<Func<WeekDungeonStageHistoryDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);
        public static WeekDungeonStageHistoryDB? FirstOrDefaultMapTo(this IQueryable<WeekDungeonStageHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);
        public static WeekDungeonStageHistoryDB? FirstMapTo(this IQueryable<WeekDungeonStageHistoryDBServer> source, Expression<Func<WeekDungeonStageHistoryDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);
        public static WeekDungeonStageHistoryDB? FirstMapTo(this IQueryable<WeekDungeonStageHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);

        public static MomoTalkOutLineDB? FirstOrDefaultMapTo(this IQueryable<MomoTalkOutLineDBServer> source, Expression<Func<MomoTalkOutLineDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);
        public static MomoTalkOutLineDB? FirstOrDefaultMapTo(this IQueryable<MomoTalkOutLineDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);
        public static MomoTalkOutLineDB? FirstMapTo(this IQueryable<MomoTalkOutLineDBServer> source, Expression<Func<MomoTalkOutLineDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);
        public static MomoTalkOutLineDB? FirstMapTo(this IQueryable<MomoTalkOutLineDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);

        public static MomoTalkChoiceDB? FirstOrDefaultMapTo(this IQueryable<MomoTalkChoiceDBServer> source, Expression<Func<MomoTalkChoiceDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);
        public static MomoTalkChoiceDB? FirstOrDefaultMapTo(this IQueryable<MomoTalkChoiceDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);
        public static MomoTalkChoiceDB? FirstMapTo(this IQueryable<MomoTalkChoiceDBServer> source, Expression<Func<MomoTalkChoiceDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);
        public static MomoTalkChoiceDB? FirstMapTo(this IQueryable<MomoTalkChoiceDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);

        public static EventContentPermanentDB? FirstOrDefaultMapTo(this IQueryable<EventContentPermanentDBServer> source, Expression<Func<EventContentPermanentDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);
        public static EventContentPermanentDB? FirstOrDefaultMapTo(this IQueryable<EventContentPermanentDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);
        public static EventContentPermanentDB? FirstMapTo(this IQueryable<EventContentPermanentDBServer> source, Expression<Func<EventContentPermanentDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);
        public static EventContentPermanentDB? FirstMapTo(this IQueryable<EventContentPermanentDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);

        public static StickerBookDB? FirstOrDefaultMapTo(this IQueryable<StickerBookDBServer> source, Expression<Func<StickerBookDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<StickerBookDBServer, StickerBookDB>(mapper);
        public static StickerBookDB? FirstOrDefaultMapTo(this IQueryable<StickerBookDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<StickerBookDBServer, StickerBookDB>(mapper);
        public static StickerBookDB? FirstMapTo(this IQueryable<StickerBookDBServer> source, Expression<Func<StickerBookDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<StickerBookDBServer, StickerBookDB>(mapper);
        public static StickerBookDB? FirstMapTo(this IQueryable<StickerBookDBServer> source, IMapper mapper) => source.First().MapInternalSingle<StickerBookDBServer, StickerBookDB>(mapper);

        //
        // Content Data
        // 

        public static SingleRaidLobbyInfoDB? FirstOrDefaultMapTo(this IQueryable<SingleRaidLobbyInfoDBServer> source, Expression<Func<SingleRaidLobbyInfoDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);
        public static SingleRaidLobbyInfoDB? FirstOrDefaultMapTo(this IQueryable<SingleRaidLobbyInfoDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);
        public static SingleRaidLobbyInfoDB? FirstMapTo(this IQueryable<SingleRaidLobbyInfoDBServer> source, Expression<Func<SingleRaidLobbyInfoDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);
        public static SingleRaidLobbyInfoDB? FirstMapTo(this IQueryable<SingleRaidLobbyInfoDBServer> source, IMapper mapper) => source.First().MapInternalSingle<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);

        public static EliminateRaidLobbyInfoDB? FirstOrDefaultMapTo(this IQueryable<EliminateRaidLobbyInfoDBServer> source, Expression<Func<EliminateRaidLobbyInfoDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);
        public static EliminateRaidLobbyInfoDB? FirstOrDefaultMapTo(this IQueryable<EliminateRaidLobbyInfoDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);
        public static EliminateRaidLobbyInfoDB? FirstMapTo(this IQueryable<EliminateRaidLobbyInfoDBServer> source, Expression<Func<EliminateRaidLobbyInfoDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);
        public static EliminateRaidLobbyInfoDB? FirstMapTo(this IQueryable<EliminateRaidLobbyInfoDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);

        public static RaidDB? FirstOrDefaultMapTo(this IQueryable<RaidDBServer> source, Expression<Func<RaidDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<RaidDBServer, RaidDB>(mapper);
        public static RaidDB? FirstOrDefaultMapTo(this IQueryable<RaidDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<RaidDBServer, RaidDB>(mapper);
        public static RaidDB? FirstMapTo(this IQueryable<RaidDBServer> source, Expression<Func<RaidDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<RaidDBServer, RaidDB>(mapper);
        public static RaidDB? FirstMapTo(this IQueryable<RaidDBServer> source, IMapper mapper) => source.First().MapInternalSingle<RaidDBServer, RaidDB>(mapper);

        public static RaidBattleDB? FirstOrDefaultMapTo(this IQueryable<RaidBattleDBServer> source, Expression<Func<RaidBattleDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<RaidBattleDBServer, RaidBattleDB>(mapper);
        public static RaidBattleDB? FirstOrDefaultMapTo(this IQueryable<RaidBattleDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<RaidBattleDBServer, RaidBattleDB>(mapper);
        public static RaidBattleDB? FirstMapTo(this IQueryable<RaidBattleDBServer> source, Expression<Func<RaidBattleDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<RaidBattleDBServer, RaidBattleDB>(mapper);
        public static RaidBattleDB? FirstMapTo(this IQueryable<RaidBattleDBServer> source, IMapper mapper) => source.First().MapInternalSingle<RaidBattleDBServer, RaidBattleDB>(mapper);

        public static TimeAttackDungeonRoomDB? FirstOrDefaultMapTo(this IQueryable<TimeAttackDungeonRoomDBServer> source, Expression<Func<TimeAttackDungeonRoomDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);
        public static TimeAttackDungeonRoomDB? FirstOrDefaultMapTo(this IQueryable<TimeAttackDungeonRoomDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);
        public static TimeAttackDungeonRoomDB? FirstMapTo(this IQueryable<TimeAttackDungeonRoomDBServer> source, Expression<Func<TimeAttackDungeonRoomDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);
        public static TimeAttackDungeonRoomDB? FirstMapTo(this IQueryable<TimeAttackDungeonRoomDBServer> source, IMapper mapper) => source.First().MapInternalSingle<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);

        public static TimeAttackDungeonBattleHistoryDB? FirstOrDefaultMapTo(this IQueryable<TimeAttackDungeonBattleHistoryDBServer> source, Expression<Func<TimeAttackDungeonBattleHistoryDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);
        public static TimeAttackDungeonBattleHistoryDB? FirstOrDefaultMapTo(this IQueryable<TimeAttackDungeonBattleHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);
        public static TimeAttackDungeonBattleHistoryDB? FirstMapTo(this IQueryable<TimeAttackDungeonBattleHistoryDBServer> source, Expression<Func<TimeAttackDungeonBattleHistoryDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);
        public static TimeAttackDungeonBattleHistoryDB? FirstMapTo(this IQueryable<TimeAttackDungeonBattleHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);

        public static MultiFloorRaidDB? FirstOrDefaultMapTo(this IQueryable<MultiFloorRaidDBServer> source, Expression<Func<MultiFloorRaidDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);
        public static MultiFloorRaidDB? FirstOrDefaultMapTo(this IQueryable<MultiFloorRaidDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);
        public static MultiFloorRaidDB? FirstMapTo(this IQueryable<MultiFloorRaidDBServer> source, Expression<Func<MultiFloorRaidDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);
        public static MultiFloorRaidDB? FirstMapTo(this IQueryable<MultiFloorRaidDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);

        public static WorldRaidLocalBossDB? FirstOrDefaultMapTo(this IQueryable<WorldRaidLocalBossDBServer> source, Expression<Func<WorldRaidLocalBossDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);
        public static WorldRaidLocalBossDB? FirstOrDefaultMapTo(this IQueryable<WorldRaidLocalBossDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);
        public static WorldRaidLocalBossDB? FirstMapTo(this IQueryable<WorldRaidLocalBossDBServer> source, Expression<Func<WorldRaidLocalBossDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);
        public static WorldRaidLocalBossDB? FirstMapTo(this IQueryable<WorldRaidLocalBossDBServer> source, IMapper mapper) => source.First().MapInternalSingle<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);

        public static WorldRaidBossListInfoDB? FirstOrDefaultMapTo(this IQueryable<WorldRaidBossListInfoDBServer> source, Expression<Func<WorldRaidBossListInfoDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);
        public static WorldRaidBossListInfoDB? FirstOrDefaultMapTo(this IQueryable<WorldRaidBossListInfoDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);
        public static WorldRaidBossListInfoDB? FirstMapTo(this IQueryable<WorldRaidBossListInfoDBServer> source, Expression<Func<WorldRaidBossListInfoDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);
        public static WorldRaidBossListInfoDB? FirstMapTo(this IQueryable<WorldRaidBossListInfoDBServer> source, IMapper mapper) => source.First().MapInternalSingle<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);

        public static WorldRaidClearHistoryDB? FirstOrDefaultMapTo(this IQueryable<WorldRaidClearHistoryDBServer> source, Expression<Func<WorldRaidClearHistoryDBServer, bool>> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);
        public static WorldRaidClearHistoryDB? FirstOrDefaultMapTo(this IQueryable<WorldRaidClearHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);
        public static WorldRaidClearHistoryDB? FirstMapTo(this IQueryable<WorldRaidClearHistoryDBServer> source, Expression<Func<WorldRaidClearHistoryDBServer, bool>> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);
        public static WorldRaidClearHistoryDB? FirstMapTo(this IQueryable<WorldRaidClearHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);

        #endregion

        #region IEnumerable Mappings

        // --- Specific FirstOrDefault / First Extensions (for IEnumerable sources) ---
        // These methods perform FirstOrDefault/First in-memory and then map the single result.

        //
        // User Data
        //

        public static AccountCurrencyDB? FirstOrDefaultMapTo(this IEnumerable<AccountCurrencyDBServer> source, Func<AccountCurrencyDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);
        public static AccountCurrencyDB? FirstOrDefaultMapTo(this IEnumerable<AccountCurrencyDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);
        public static AccountCurrencyDB? FirstMapTo(this IEnumerable<AccountCurrencyDBServer> source, Func<AccountCurrencyDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);
        public static AccountCurrencyDB? FirstMapTo(this IEnumerable<AccountCurrencyDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);

        public static ItemDB? FirstOrDefaultMapTo(this IEnumerable<ItemDBServer> source, Func<ItemDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<ItemDBServer, ItemDB>(mapper);
        public static ItemDB? FirstOrDefaultMapTo(this IEnumerable<ItemDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<ItemDBServer, ItemDB>(mapper);
        public static ItemDB? FirstMapTo(this IEnumerable<ItemDBServer> source, Func<ItemDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<ItemDBServer, ItemDB>(mapper);
        public static ItemDB? FirstMapTo(this IEnumerable<ItemDBServer> source, IMapper mapper) => source.First().MapInternalSingle<ItemDBServer, ItemDB>(mapper);

        public static CharacterDB? FirstOrDefaultMapTo(this IEnumerable<CharacterDBServer> source, Func<CharacterDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CharacterDBServer, CharacterDB>(mapper);
        public static CharacterDB? FirstOrDefaultMapTo(this IEnumerable<CharacterDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CharacterDBServer, CharacterDB>(mapper);
        public static CharacterDB? FirstMapTo(this IEnumerable<CharacterDBServer> source, Func<CharacterDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CharacterDBServer, CharacterDB>(mapper);
        public static CharacterDB? FirstMapTo(this IEnumerable<CharacterDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CharacterDBServer, CharacterDB>(mapper);

        public static EquipmentDB? FirstOrDefaultMapTo(this IEnumerable<EquipmentDBServer> source, Func<EquipmentDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EquipmentDBServer, EquipmentDB>(mapper);
        public static EquipmentDB? FirstOrDefaultMapTo(this IEnumerable<EquipmentDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EquipmentDBServer, EquipmentDB>(mapper);
        public static EquipmentDB? FirstMapTo(this IEnumerable<EquipmentDBServer> source, Func<EquipmentDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EquipmentDBServer, EquipmentDB>(mapper);
        public static EquipmentDB? FirstMapTo(this IEnumerable<EquipmentDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EquipmentDBServer, EquipmentDB>(mapper);

        public static WeaponDB? FirstOrDefaultMapTo(this IEnumerable<WeaponDBServer> source, Func<WeaponDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<WeaponDBServer, WeaponDB>(mapper);
        public static WeaponDB? FirstOrDefaultMapTo(this IEnumerable<WeaponDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<WeaponDBServer, WeaponDB>(mapper);
        public static WeaponDB? FirstMapTo(this IEnumerable<WeaponDBServer> source, Func<WeaponDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<WeaponDBServer, WeaponDB>(mapper);
        public static WeaponDB? FirstMapTo(this IEnumerable<WeaponDBServer> source, IMapper mapper) => source.First().MapInternalSingle<WeaponDBServer, WeaponDB>(mapper);

        public static GearDB? FirstOrDefaultMapTo(this IEnumerable<GearDBServer> source, Func<GearDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<GearDBServer, GearDB>(mapper);
        public static GearDB? FirstOrDefaultMapTo(this IEnumerable<GearDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<GearDBServer, GearDB>(mapper);
        public static GearDB? FirstMapTo(this IEnumerable<GearDBServer> source, Func<GearDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<GearDBServer, GearDB>(mapper);
        public static GearDB? FirstMapTo(this IEnumerable<GearDBServer> source, IMapper mapper) => source.First().MapInternalSingle<GearDBServer, GearDB>(mapper);

        public static EchelonDB? FirstOrDefaultMapTo(this IEnumerable<EchelonDBServer> source, Func<EchelonDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EchelonDBServer, EchelonDB>(mapper);
        public static EchelonDB? FirstOrDefaultMapTo(this IEnumerable<EchelonDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EchelonDBServer, EchelonDB>(mapper);
        public static EchelonDB? FirstMapTo(this IEnumerable<EchelonDBServer> source, Func<EchelonDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EchelonDBServer, EchelonDB>(mapper);
        public static EchelonDB? FirstMapTo(this IEnumerable<EchelonDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EchelonDBServer, EchelonDB>(mapper);

        public static EchelonPresetDB? FirstOrDefaultMapTo(this IEnumerable<EchelonPresetDBServer> source, Func<EchelonPresetDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EchelonPresetDBServer, EchelonPresetDB>(mapper);
        public static EchelonPresetDB? FirstOrDefaultMapTo(this IEnumerable<EchelonPresetDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EchelonPresetDBServer, EchelonPresetDB>(mapper);
        public static EchelonPresetDB? FirstMapTo(this IEnumerable<EchelonPresetDBServer> source, Func<EchelonPresetDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EchelonPresetDBServer, EchelonPresetDB>(mapper);
        public static EchelonPresetDB? FirstMapTo(this IEnumerable<EchelonPresetDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EchelonPresetDBServer, EchelonPresetDB>(mapper);

        public static EchelonPresetGroupDB? FirstOrDefaultMapTo(this IEnumerable<EchelonPresetGroupDBServer> source, Func<EchelonPresetGroupDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);
        public static EchelonPresetGroupDB? FirstOrDefaultMapTo(this IEnumerable<EchelonPresetGroupDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);
        public static EchelonPresetGroupDB? FirstMapTo(this IEnumerable<EchelonPresetGroupDBServer> source, Func<EchelonPresetGroupDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);
        public static EchelonPresetGroupDB? FirstMapTo(this IEnumerable<EchelonPresetGroupDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);

        public static CafeDB? FirstOrDefaultMapTo(this IEnumerable<CafeDBServer> source, Func<CafeDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CafeDBServer, CafeDB>(mapper);
        public static CafeDB? FirstOrDefaultMapTo(this IEnumerable<CafeDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CafeDBServer, CafeDB>(mapper);
        public static CafeDB? FirstMapTo(this IEnumerable<CafeDBServer> source, Func<CafeDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CafeDBServer, CafeDB>(mapper);
        public static CafeDB? FirstMapTo(this IEnumerable<CafeDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CafeDBServer, CafeDB>(mapper);

        public static FurnitureDB? FirstOrDefaultMapTo(this IEnumerable<FurnitureDBServer> source, Func<FurnitureDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<FurnitureDBServer, FurnitureDB>(mapper);
        public static FurnitureDB? FirstOrDefaultMapTo(this IEnumerable<FurnitureDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<FurnitureDBServer, FurnitureDB>(mapper);
        public static FurnitureDB? FirstMapTo(this IEnumerable<FurnitureDBServer> source, Func<FurnitureDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<FurnitureDBServer, FurnitureDB>(mapper);
        public static FurnitureDB? FirstMapTo(this IEnumerable<FurnitureDBServer> source, IMapper mapper) => source.First().MapInternalSingle<FurnitureDBServer, FurnitureDB>(mapper);

        public static MemoryLobbyDB? FirstOrDefaultMapTo(this IEnumerable<MemoryLobbyDBServer> source, Func<MemoryLobbyDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);
        public static MemoryLobbyDB? FirstOrDefaultMapTo(this IEnumerable<MemoryLobbyDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);
        public static MemoryLobbyDB? FirstMapTo(this IEnumerable<MemoryLobbyDBServer> source, Func<MemoryLobbyDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);
        public static MemoryLobbyDB? FirstMapTo(this IEnumerable<MemoryLobbyDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);

        public static MailDB? FirstOrDefaultMapTo(this IEnumerable<MailDBServer> source, Func<MailDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MailDBServer, MailDB>(mapper);
        public static MailDB? FirstOrDefaultMapTo(this IEnumerable<MailDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MailDBServer, MailDB>(mapper);
        public static MailDB? FirstMapTo(this IEnumerable<MailDBServer> source, Func<MailDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MailDBServer, MailDB>(mapper);
        public static MailDB? FirstMapTo(this IEnumerable<MailDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MailDBServer, MailDB>(mapper);

        public static EmblemDB? FirstOrDefaultMapTo(this IEnumerable<EmblemDBServer> source, Func<EmblemDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EmblemDBServer, EmblemDB>(mapper);
        public static EmblemDB? FirstOrDefaultMapTo(this IEnumerable<EmblemDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EmblemDBServer, EmblemDB>(mapper);
        public static EmblemDB? FirstMapTo(this IEnumerable<EmblemDBServer> source, Func<EmblemDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EmblemDBServer, EmblemDB>(mapper);
        public static EmblemDB? FirstMapTo(this IEnumerable<EmblemDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EmblemDBServer, EmblemDB>(mapper);

        public static IdCardBackgroundDB? FirstOrDefaultMapTo(this IEnumerable<IdCardBackgroundDBServer> source, Func<IdCardBackgroundDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);
        public static IdCardBackgroundDB? FirstOrDefaultMapTo(this IEnumerable<IdCardBackgroundDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);
        public static IdCardBackgroundDB? FirstMapTo(this IEnumerable<IdCardBackgroundDBServer> source, Func<IdCardBackgroundDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);
        public static IdCardBackgroundDB? FirstMapTo(this IEnumerable<IdCardBackgroundDBServer> source, IMapper mapper) => source.First().MapInternalSingle<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);

        public static CostumeDB? FirstOrDefaultMapTo(this IEnumerable<CostumeDBServer> source, Func<CostumeDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CostumeDBServer, CostumeDB>(mapper);
        public static CostumeDB? FirstOrDefaultMapTo(this IEnumerable<CostumeDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CostumeDBServer, CostumeDB>(mapper);
        public static CostumeDB? FirstMapTo(this IEnumerable<CostumeDBServer> source, Func<CostumeDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CostumeDBServer, CostumeDB>(mapper);
        public static CostumeDB? FirstMapTo(this IEnumerable<CostumeDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CostumeDBServer, CostumeDB>(mapper);

        public static StickerDB? FirstOrDefaultMapTo(this IEnumerable<StickerDBServer> source, Func<StickerDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<StickerDBServer, StickerDB>(mapper);
        public static StickerDB? FirstOrDefaultMapTo(this IEnumerable<StickerDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<StickerDBServer, StickerDB>(mapper);
        public static StickerDB? FirstMapTo(this IEnumerable<StickerDBServer> source, Func<StickerDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<StickerDBServer, StickerDB>(mapper);
        public static StickerDB? FirstMapTo(this IEnumerable<StickerDBServer> source, IMapper mapper) => source.First().MapInternalSingle<StickerDBServer, StickerDB>(mapper);

        public static AccountAttachmentDB? FirstOrDefaultMapTo(this IEnumerable<AccountAttachmentDBServer> source, Func<AccountAttachmentDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);
        public static AccountAttachmentDB? FirstOrDefaultMapTo(this IEnumerable<AccountAttachmentDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);
        public static AccountAttachmentDB? FirstMapTo(this IEnumerable<AccountAttachmentDBServer> source, Func<AccountAttachmentDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);
        public static AccountAttachmentDB? FirstMapTo(this IEnumerable<AccountAttachmentDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);

        public static AccountLevelRewardDB? FirstOrDefaultMapTo(this IEnumerable<AccountLevelRewardDBServer> source, Func<AccountLevelRewardDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);
        public static AccountLevelRewardDB? FirstOrDefaultMapTo(this IEnumerable<AccountLevelRewardDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);
        public static AccountLevelRewardDB? FirstMapTo(this IEnumerable<AccountLevelRewardDBServer> source, Func<AccountLevelRewardDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);
        public static AccountLevelRewardDB? FirstMapTo(this IEnumerable<AccountLevelRewardDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);

        //
        // Progress Data
        //

        public static MissionProgressDB? FirstOrDefaultMapTo(this IEnumerable<MissionProgressDBServer> source, Func<MissionProgressDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MissionProgressDBServer, MissionProgressDB>(mapper);
        public static MissionProgressDB? FirstOrDefaultMapTo(this IEnumerable<MissionProgressDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MissionProgressDBServer, MissionProgressDB>(mapper);
        public static MissionProgressDB? FirstMapTo(this IEnumerable<MissionProgressDBServer> source, Func<MissionProgressDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MissionProgressDBServer, MissionProgressDB>(mapper);
        public static MissionProgressDB? FirstMapTo(this IEnumerable<MissionProgressDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MissionProgressDBServer, MissionProgressDB>(mapper);

        public static AttendanceHistoryDB? FirstOrDefaultMapTo(this IEnumerable<AttendanceHistoryDBServer> source, Func<AttendanceHistoryDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);
        public static AttendanceHistoryDB? FirstOrDefaultMapTo(this IEnumerable<AttendanceHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);
        public static AttendanceHistoryDB? FirstMapTo(this IEnumerable<AttendanceHistoryDBServer> source, Func<AttendanceHistoryDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);
        public static AttendanceHistoryDB? FirstMapTo(this IEnumerable<AttendanceHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);

        public static AcademyDB? FirstOrDefaultMapTo(this IEnumerable<AcademyDBServer> source, Func<AcademyDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AcademyDBServer, AcademyDB>(mapper);
        public static AcademyDB? FirstOrDefaultMapTo(this IEnumerable<AcademyDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AcademyDBServer, AcademyDB>(mapper);
        public static AcademyDB? FirstMapTo(this IEnumerable<AcademyDBServer> source, Func<AcademyDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AcademyDBServer, AcademyDB>(mapper);
        public static AcademyDB? FirstMapTo(this IEnumerable<AcademyDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AcademyDBServer, AcademyDB>(mapper);

        public static AcademyLocationDB? FirstOrDefaultMapTo(this IEnumerable<AcademyLocationDBServer> source, Func<AcademyLocationDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<AcademyLocationDBServer, AcademyLocationDB>(mapper);
        public static AcademyLocationDB? FirstOrDefaultMapTo(this IEnumerable<AcademyLocationDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<AcademyLocationDBServer, AcademyLocationDB>(mapper);
        public static AcademyLocationDB? FirstMapTo(this IEnumerable<AcademyLocationDBServer> source, Func<AcademyLocationDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<AcademyLocationDBServer, AcademyLocationDB>(mapper);
        public static AcademyLocationDB? FirstMapTo(this IEnumerable<AcademyLocationDBServer> source, IMapper mapper) => source.First().MapInternalSingle<AcademyLocationDBServer, AcademyLocationDB>(mapper);

        public static CampaignMainStageSaveDB? FirstOrDefaultMapTo(this IEnumerable<CampaignMainStageSaveDBServer> source, Func<CampaignMainStageSaveDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);
        public static CampaignMainStageSaveDB? FirstOrDefaultMapTo(this IEnumerable<CampaignMainStageSaveDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);
        public static CampaignMainStageSaveDB? FirstMapTo(this IEnumerable<CampaignMainStageSaveDBServer> source, Func<CampaignMainStageSaveDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);
        public static CampaignMainStageSaveDB? FirstMapTo(this IEnumerable<CampaignMainStageSaveDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);

        public static CampaignChapterClearRewardHistoryDB? FirstOrDefaultMapTo(this IEnumerable<CampaignChapterClearRewardHistoryDBServer> source, Func<CampaignChapterClearRewardHistoryDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);
        public static CampaignChapterClearRewardHistoryDB? FirstOrDefaultMapTo(this IEnumerable<CampaignChapterClearRewardHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);
        public static CampaignChapterClearRewardHistoryDB? FirstMapTo(this IEnumerable<CampaignChapterClearRewardHistoryDBServer> source, Func<CampaignChapterClearRewardHistoryDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);
        public static CampaignChapterClearRewardHistoryDB? FirstMapTo(this IEnumerable<CampaignChapterClearRewardHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);

        public static StrategyObjectHistoryDB? FirstOrDefaultMapTo(this IEnumerable<StrategyObjectHistoryDBServer> source, Func<StrategyObjectHistoryDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);
        public static StrategyObjectHistoryDB? FirstOrDefaultMapTo(this IEnumerable<StrategyObjectHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);
        public static StrategyObjectHistoryDB? FirstMapTo(this IEnumerable<StrategyObjectHistoryDBServer> source, Func<StrategyObjectHistoryDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);
        public static StrategyObjectHistoryDB? FirstMapTo(this IEnumerable<StrategyObjectHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);

        public static ScenarioHistoryDB? FirstOrDefaultMapTo(this IEnumerable<ScenarioHistoryDBServer> source, Func<ScenarioHistoryDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);
        public static ScenarioHistoryDB? FirstOrDefaultMapTo(this IEnumerable<ScenarioHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);
        public static ScenarioHistoryDB? FirstMapTo(this IEnumerable<ScenarioHistoryDBServer> source, Func<ScenarioHistoryDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);
        public static ScenarioHistoryDB? FirstMapTo(this IEnumerable<ScenarioHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);

        public static ScenarioGroupHistoryDB? FirstOrDefaultMapTo(this IEnumerable<ScenarioGroupHistoryDBServer> source, Func<ScenarioGroupHistoryDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);
        public static ScenarioGroupHistoryDB? FirstOrDefaultMapTo(this IEnumerable<ScenarioGroupHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);
        public static ScenarioGroupHistoryDB? FirstMapTo(this IEnumerable<ScenarioGroupHistoryDBServer> source, Func<ScenarioGroupHistoryDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);
        public static ScenarioGroupHistoryDB? FirstMapTo(this IEnumerable<ScenarioGroupHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);

        public static CampaignStageHistoryDB? FirstOrDefaultMapTo(this IEnumerable<CampaignStageHistoryDBServer> source, Func<CampaignStageHistoryDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);
        public static CampaignStageHistoryDB? FirstOrDefaultMapTo(this IEnumerable<CampaignStageHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);
        public static CampaignStageHistoryDB? FirstMapTo(this IEnumerable<CampaignStageHistoryDBServer> source, Func<CampaignStageHistoryDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);
        public static CampaignStageHistoryDB? FirstMapTo(this IEnumerable<CampaignStageHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);

        public static WeekDungeonStageHistoryDB? FirstOrDefaultMapTo(this IEnumerable<WeekDungeonStageHistoryDBServer> source, Func<WeekDungeonStageHistoryDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);
        public static WeekDungeonStageHistoryDB? FirstOrDefaultMapTo(this IEnumerable<WeekDungeonStageHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);
        public static WeekDungeonStageHistoryDB? FirstMapTo(this IEnumerable<WeekDungeonStageHistoryDBServer> source, Func<WeekDungeonStageHistoryDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);
        public static WeekDungeonStageHistoryDB? FirstMapTo(this IEnumerable<WeekDungeonStageHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);

        public static MomoTalkOutLineDB? FirstOrDefaultMapTo(this IEnumerable<MomoTalkOutLineDBServer> source, Func<MomoTalkOutLineDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);
        public static MomoTalkOutLineDB? FirstOrDefaultMapTo(this IEnumerable<MomoTalkOutLineDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);
        public static MomoTalkOutLineDB? FirstMapTo(this IEnumerable<MomoTalkOutLineDBServer> source, Func<MomoTalkOutLineDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);
        public static MomoTalkOutLineDB? FirstMapTo(this IEnumerable<MomoTalkOutLineDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);

        public static MomoTalkChoiceDB? FirstOrDefaultMapTo(this IEnumerable<MomoTalkChoiceDBServer> source, Func<MomoTalkChoiceDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);
        public static MomoTalkChoiceDB? FirstOrDefaultMapTo(this IEnumerable<MomoTalkChoiceDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);
        public static MomoTalkChoiceDB? FirstMapTo(this IEnumerable<MomoTalkChoiceDBServer> source, Func<MomoTalkChoiceDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);
        public static MomoTalkChoiceDB? FirstMapTo(this IEnumerable<MomoTalkChoiceDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);

        public static EventContentPermanentDB? FirstOrDefaultMapTo(this IEnumerable<EventContentPermanentDBServer> source, Func<EventContentPermanentDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);
        public static EventContentPermanentDB? FirstOrDefaultMapTo(this IEnumerable<EventContentPermanentDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);
        public static EventContentPermanentDB? FirstMapTo(this IEnumerable<EventContentPermanentDBServer> source, Func<EventContentPermanentDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);
        public static EventContentPermanentDB? FirstMapTo(this IEnumerable<EventContentPermanentDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);

        public static StickerBookDB? FirstOrDefaultMapTo(this IEnumerable<StickerBookDBServer> source, Func<StickerBookDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<StickerBookDBServer, StickerBookDB>(mapper);
        public static StickerBookDB? FirstOrDefaultMapTo(this IEnumerable<StickerBookDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<StickerBookDBServer, StickerBookDB>(mapper);
        public static StickerBookDB? FirstMapTo(this IEnumerable<StickerBookDBServer> source, Func<StickerBookDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<StickerBookDBServer, StickerBookDB>(mapper);
        public static StickerBookDB? FirstMapTo(this IEnumerable<StickerBookDBServer> source, IMapper mapper) => source.First().MapInternalSingle<StickerBookDBServer, StickerBookDB>(mapper);

        //
        // Content Data
        //

        public static SingleRaidLobbyInfoDB? FirstOrDefaultMapTo(this IEnumerable<SingleRaidLobbyInfoDBServer> source, Func<SingleRaidLobbyInfoDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);
        public static SingleRaidLobbyInfoDB? FirstOrDefaultMapTo(this IEnumerable<SingleRaidLobbyInfoDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);
        public static SingleRaidLobbyInfoDB? FirstMapTo(this IEnumerable<SingleRaidLobbyInfoDBServer> source, Func<SingleRaidLobbyInfoDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);
        public static SingleRaidLobbyInfoDB? FirstMapTo(this IEnumerable<SingleRaidLobbyInfoDBServer> source, IMapper mapper) => source.First().MapInternalSingle<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);

        public static EliminateRaidLobbyInfoDB? FirstOrDefaultMapTo(this IEnumerable<EliminateRaidLobbyInfoDBServer> source, Func<EliminateRaidLobbyInfoDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);
        public static EliminateRaidLobbyInfoDB? FirstOrDefaultMapTo(this IEnumerable<EliminateRaidLobbyInfoDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);
        public static EliminateRaidLobbyInfoDB? FirstMapTo(this IEnumerable<EliminateRaidLobbyInfoDBServer> source, Func<EliminateRaidLobbyInfoDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);
        public static EliminateRaidLobbyInfoDB? FirstMapTo(this IEnumerable<EliminateRaidLobbyInfoDBServer> source, IMapper mapper) => source.First().MapInternalSingle<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);

        public static RaidDB? FirstOrDefaultMapTo(this IEnumerable<RaidDBServer> source, Func<RaidDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<RaidDBServer, RaidDB>(mapper);
        public static RaidDB? FirstOrDefaultMapTo(this IEnumerable<RaidDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<RaidDBServer, RaidDB>(mapper);
        public static RaidDB? FirstMapTo(this IEnumerable<RaidDBServer> source, Func<RaidDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<RaidDBServer, RaidDB>(mapper);
        public static RaidDB? FirstMapTo(this IEnumerable<RaidDBServer> source, IMapper mapper) => source.First().MapInternalSingle<RaidDBServer, RaidDB>(mapper);

        public static RaidBattleDB? FirstOrDefaultMapTo(this IEnumerable<RaidBattleDBServer> source, Func<RaidBattleDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<RaidBattleDBServer, RaidBattleDB>(mapper);
        public static RaidBattleDB? FirstOrDefaultMapTo(this IEnumerable<RaidBattleDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<RaidBattleDBServer, RaidBattleDB>(mapper);
        public static RaidBattleDB? FirstMapTo(this IEnumerable<RaidBattleDBServer> source, Func<RaidBattleDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<RaidBattleDBServer, RaidBattleDB>(mapper);
        public static RaidBattleDB? FirstMapTo(this IEnumerable<RaidBattleDBServer> source, IMapper mapper) => source.First().MapInternalSingle<RaidBattleDBServer, RaidBattleDB>(mapper);

        public static TimeAttackDungeonRoomDB? FirstOrDefaultMapTo(this IEnumerable<TimeAttackDungeonRoomDBServer> source, Func<TimeAttackDungeonRoomDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);
        public static TimeAttackDungeonRoomDB? FirstOrDefaultMapTo(this IEnumerable<TimeAttackDungeonRoomDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);
        public static TimeAttackDungeonRoomDB? FirstMapTo(this IEnumerable<TimeAttackDungeonRoomDBServer> source, Func<TimeAttackDungeonRoomDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);
        public static TimeAttackDungeonRoomDB? FirstMapTo(this IEnumerable<TimeAttackDungeonRoomDBServer> source, IMapper mapper) => source.First().MapInternalSingle<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);

        public static TimeAttackDungeonBattleHistoryDB? FirstOrDefaultMapTo(this IEnumerable<TimeAttackDungeonBattleHistoryDBServer> source, Func<TimeAttackDungeonBattleHistoryDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);
        public static TimeAttackDungeonBattleHistoryDB? FirstOrDefaultMapTo(this IEnumerable<TimeAttackDungeonBattleHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);
        public static TimeAttackDungeonBattleHistoryDB? FirstMapTo(this IEnumerable<TimeAttackDungeonBattleHistoryDBServer> source, Func<TimeAttackDungeonBattleHistoryDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);
        public static TimeAttackDungeonBattleHistoryDB? FirstMapTo(this IEnumerable<TimeAttackDungeonBattleHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);

        public static MultiFloorRaidDB? FirstOrDefaultMapTo(this IEnumerable<MultiFloorRaidDBServer> source, Func<MultiFloorRaidDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);
        public static MultiFloorRaidDB? FirstOrDefaultMapTo(this IEnumerable<MultiFloorRaidDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);
        public static MultiFloorRaidDB? FirstMapTo(this IEnumerable<MultiFloorRaidDBServer> source, Func<MultiFloorRaidDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);
        public static MultiFloorRaidDB? FirstMapTo(this IEnumerable<MultiFloorRaidDBServer> source, IMapper mapper) => source.First().MapInternalSingle<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);

        public static WorldRaidLocalBossDB? FirstOrDefaultMapTo(this IEnumerable<WorldRaidLocalBossDBServer> source, Func<WorldRaidLocalBossDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);
        public static WorldRaidLocalBossDB? FirstOrDefaultMapTo(this IEnumerable<WorldRaidLocalBossDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);
        public static WorldRaidLocalBossDB? FirstMapTo(this IEnumerable<WorldRaidLocalBossDBServer> source, Func<WorldRaidLocalBossDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);
        public static WorldRaidLocalBossDB? FirstMapTo(this IEnumerable<WorldRaidLocalBossDBServer> source, IMapper mapper) => source.First().MapInternalSingle<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);

        public static WorldRaidBossListInfoDB? FirstOrDefaultMapTo(this IEnumerable<WorldRaidBossListInfoDBServer> source, Func<WorldRaidBossListInfoDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);
        public static WorldRaidBossListInfoDB? FirstOrDefaultMapTo(this IEnumerable<WorldRaidBossListInfoDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);
        public static WorldRaidBossListInfoDB? FirstMapTo(this IEnumerable<WorldRaidBossListInfoDBServer> source, Func<WorldRaidBossListInfoDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);
        public static WorldRaidBossListInfoDB? FirstMapTo(this IEnumerable<WorldRaidBossListInfoDBServer> source, IMapper mapper) => source.First().MapInternalSingle<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);

        public static WorldRaidClearHistoryDB? FirstOrDefaultMapTo(this IEnumerable<WorldRaidClearHistoryDBServer> source, Func<WorldRaidClearHistoryDBServer, bool> predicate, IMapper mapper) => source.FirstOrDefault(predicate).MapInternalSingle<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);
        public static WorldRaidClearHistoryDB? FirstOrDefaultMapTo(this IEnumerable<WorldRaidClearHistoryDBServer> source, IMapper mapper) => source.FirstOrDefault().MapInternalSingle<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);
        public static WorldRaidClearHistoryDB? FirstMapTo(this IEnumerable<WorldRaidClearHistoryDBServer> source, Func<WorldRaidClearHistoryDBServer, bool> predicate, IMapper mapper) => source.First(predicate).MapInternalSingle<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);
        public static WorldRaidClearHistoryDB? FirstMapTo(this IEnumerable<WorldRaidClearHistoryDBServer> source, IMapper mapper) => source.First().MapInternalSingle<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);

        #endregion
    }
}


