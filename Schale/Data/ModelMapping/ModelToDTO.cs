using AutoMapper;
using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.ModelMapping
{
    public static class ModelToDTO
    {
        //
        // Account Data
        //

        public static AccountDB? ToMap(this AccountDBServer source, IMapper mapper) => source.MapInternalSingle<AccountDBServer, AccountDB>(mapper);

        //
        // User Data
        // 

        public static AccountCurrencyDB? ToMap(this AccountCurrencyDBServer source, IMapper mapper) => source.MapInternalSingle<AccountCurrencyDBServer, AccountCurrencyDB>(mapper);
        public static ItemDB? ToMap(this ItemDBServer source, IMapper mapper) => source.MapInternalSingle<ItemDBServer, ItemDB>(mapper);
        public static CharacterDB? ToMap(this CharacterDBServer source, IMapper mapper) => source.MapInternalSingle<CharacterDBServer, CharacterDB>(mapper);
        public static EquipmentDB? ToMap(this EquipmentDBServer source, IMapper mapper) => source.MapInternalSingle<EquipmentDBServer, EquipmentDB>(mapper);
        public static WeaponDB? ToMap(this WeaponDBServer source, IMapper mapper) => source.MapInternalSingle<WeaponDBServer, WeaponDB>(mapper);
        public static GearDB? ToMap(this GearDBServer source, IMapper mapper) => source.MapInternalSingle<GearDBServer, GearDB>(mapper);
        public static EchelonDB? ToMap(this EchelonDBServer source, IMapper mapper) => source.MapInternalSingle<EchelonDBServer, EchelonDB>(mapper);
        public static EchelonPresetDB? ToMap(this EchelonPresetDBServer source, IMapper mapper) => source.MapInternalSingle<EchelonPresetDBServer, EchelonPresetDB>(mapper);
        public static EchelonPresetGroupDB? ToMap(this EchelonPresetGroupDBServer source, IMapper mapper) => source.MapInternalSingle<EchelonPresetGroupDBServer, EchelonPresetGroupDB>(mapper);
        public static CafeDB? ToMap(this CafeDBServer source, IMapper mapper) => source.MapInternalSingle<CafeDBServer, CafeDB>(mapper);
        public static FurnitureDB? ToMap(this FurnitureDBServer source, IMapper mapper) => source.MapInternalSingle<FurnitureDBServer, FurnitureDB>(mapper);
        public static MemoryLobbyDB? ToMap(this MemoryLobbyDBServer source, IMapper mapper) => source.MapInternalSingle<MemoryLobbyDBServer, MemoryLobbyDB>(mapper);
        public static MailDB? ToMap(this MailDBServer source, IMapper mapper) => source.MapInternalSingle<MailDBServer, MailDB>(mapper);
        public static EmblemDB? ToMap(this EmblemDBServer source, IMapper mapper) => source.MapInternalSingle<EmblemDBServer, EmblemDB>(mapper);
        public static IdCardBackgroundDB? ToMap(this IdCardBackgroundDBServer source, IMapper mapper) => source.MapInternalSingle<IdCardBackgroundDBServer, IdCardBackgroundDB>(mapper);
        public static CostumeDB? ToMap(this CostumeDBServer source, IMapper mapper) => source.MapInternalSingle<CostumeDBServer, CostumeDB>(mapper);
        public static StickerDB? ToMap(this StickerDBServer source, IMapper mapper) => source.MapInternalSingle<StickerDBServer, StickerDB>(mapper);
        public static AccountAttachmentDB? ToMap(this AccountAttachmentDBServer source, IMapper mapper) => source.MapInternalSingle<AccountAttachmentDBServer, AccountAttachmentDB>(mapper);
        public static AccountLevelRewardDB? ToMap(this AccountLevelRewardDBServer source, IMapper mapper) => source.MapInternalSingle<AccountLevelRewardDBServer, AccountLevelRewardDB>(mapper);

        //
        // Progress Data
        // 
        public static MissionProgressDB? ToMap(this MissionProgressDBServer source, IMapper mapper) => source.MapInternalSingle<MissionProgressDBServer, MissionProgressDB>(mapper);
        public static AttendanceHistoryDB? ToMap(this AttendanceHistoryDBServer source, IMapper mapper) => source.MapInternalSingle<AttendanceHistoryDBServer, AttendanceHistoryDB>(mapper);
        public static AcademyDB? ToMap(this AcademyDBServer source, IMapper mapper) => source.MapInternalSingle<AcademyDBServer, AcademyDB>(mapper);
        public static AcademyLocationDB? ToMap(this AcademyLocationDBServer source, IMapper mapper) => source.MapInternalSingle<AcademyLocationDBServer, AcademyLocationDB>(mapper);
        public static CampaignMainStageSaveDB? ToMap(this CampaignMainStageSaveDBServer source, IMapper mapper) => source.MapInternalSingle<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>(mapper);
        public static CampaignChapterClearRewardHistoryDB? ToMap(this CampaignChapterClearRewardHistoryDBServer source, IMapper mapper) => source.MapInternalSingle<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>(mapper);
        public static StrategyObjectHistoryDB? ToMap(this StrategyObjectHistoryDBServer source, IMapper mapper) => source.MapInternalSingle<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>(mapper);
        public static ScenarioHistoryDB? ToMap(this ScenarioHistoryDBServer source, IMapper mapper) => source.MapInternalSingle<ScenarioHistoryDBServer, ScenarioHistoryDB>(mapper);
        public static ScenarioGroupHistoryDB? ToMap(this ScenarioGroupHistoryDBServer source, IMapper mapper) => source.MapInternalSingle<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>(mapper);
        public static CampaignStageHistoryDB? ToMap(this CampaignStageHistoryDBServer source, IMapper mapper) => source.MapInternalSingle<CampaignStageHistoryDBServer, CampaignStageHistoryDB>(mapper);
        public static WeekDungeonStageHistoryDB? ToMap(this WeekDungeonStageHistoryDBServer source, IMapper mapper) => source.MapInternalSingle<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>(mapper);
        public static SchoolDungeonStageHistoryDB? ToMap(this SchoolDungeonStageHistoryDBServer source, IMapper mapper) => source.MapInternalSingle<SchoolDungeonStageHistoryDBServer, SchoolDungeonStageHistoryDB>(mapper);
        public static MomoTalkOutLineDB? ToMap(this MomoTalkOutLineDBServer source, IMapper mapper) => source.MapInternalSingle<MomoTalkOutLineDBServer, MomoTalkOutLineDB>(mapper);
        public static MomoTalkChoiceDB? ToMap(this MomoTalkChoiceDBServer source, IMapper mapper) => source.MapInternalSingle<MomoTalkChoiceDBServer, MomoTalkChoiceDB>(mapper);
        public static EventContentPermanentDB? ToMap(this EventContentPermanentDBServer source, IMapper mapper) => source.MapInternalSingle<EventContentPermanentDBServer, EventContentPermanentDB>(mapper);
        public static StickerBookDB? ToMap(this StickerBookDBServer source, IMapper mapper) => source.MapInternalSingle<StickerBookDBServer, StickerBookDB>(mapper);

        //
        // Content Data
        // 
        public static SingleRaidLobbyInfoDB? ToMap(this SingleRaidLobbyInfoDBServer source, IMapper mapper) => source.MapInternalSingle<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>(mapper);
        public static EliminateRaidLobbyInfoDB? ToMap(this EliminateRaidLobbyInfoDBServer source, IMapper mapper) => source.MapInternalSingle<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>(mapper);
        public static RaidDB? ToMap(this RaidDBServer source, IMapper mapper) => source.MapInternalSingle<RaidDBServer, RaidDB>(mapper);
        public static RaidBattleDB? ToMap(this RaidBattleDBServer source, IMapper mapper) => source.MapInternalSingle<RaidBattleDBServer, RaidBattleDB>(mapper);
        public static TimeAttackDungeonRoomDB? ToMap(this TimeAttackDungeonRoomDBServer source, IMapper mapper) => source.MapInternalSingle<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>(mapper);
        public static TimeAttackDungeonBattleHistoryDB? ToMap(this TimeAttackDungeonBattleHistoryDBServer source, IMapper mapper) => source.MapInternalSingle<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>(mapper);
        public static MultiFloorRaidDB? ToMap(this MultiFloorRaidDBServer source, IMapper mapper) => source.MapInternalSingle<MultiFloorRaidDBServer, MultiFloorRaidDB>(mapper);
        public static WorldRaidLocalBossDB? ToMap(this WorldRaidLocalBossDBServer source, IMapper mapper) => source.MapInternalSingle<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>(mapper);
        public static WorldRaidBossListInfoDB? ToMap(this WorldRaidBossListInfoDBServer source, IMapper mapper) => source.MapInternalSingle<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>(mapper);
        public static WorldRaidClearHistoryDB? ToMap(this WorldRaidClearHistoryDBServer source, IMapper mapper) => source.MapInternalSingle<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>(mapper);
    }
}


