using AutoMapper;
using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;

namespace Schale.MappingProfiles
{
    public class GameModelsMappingProfile : Profile
    {
        public GameModelsMappingProfile()
        {
            ConfigureUserDataMappings();
            ConfigureProgressMappings();
            ConfigureContentMappings();
            ConfigureReverseMappings();
            ConfigureNestedDTOMappings();
        }

        private void ConfigureUserDataMappings()
        {
            CreateMap<AccountDBServer, AccountDB>();
            CreateMap<AccountCurrencyDBServer, AccountCurrencyDB>();
            CreateMap<ItemDBServer, ItemDB>();
            CreateMap<CharacterDBServer, CharacterDB>();
            CreateMap<EquipmentDBServer, EquipmentDB>();
            CreateMap<WeaponDBServer, WeaponDB>();
            CreateMap<GearDBServer, GearDB>();
            CreateMap<EchelonDBServer, EchelonDB>();
            CreateMap<EchelonPresetDBServer, EchelonPresetDB>();
            CreateMap<EchelonPresetGroupDBServer, EchelonPresetGroupDB>();
            CreateMap<CafeDBServer, CafeDB>();
            CreateMap<FurnitureDBServer, FurnitureDB>();
            CreateMap<MemoryLobbyDBServer, MemoryLobbyDB>();
            CreateMap<MailDBServer, MailDB>();
            CreateMap<EmblemDBServer, EmblemDB>();
            CreateMap<IdCardBackgroundDBServer, IdCardBackgroundDB>();
            CreateMap<CostumeDBServer, CostumeDB>();
            CreateMap<StickerDBServer, StickerDB>();
            CreateMap<AccountAttachmentDBServer, AccountAttachmentDB>();
            CreateMap<AccountLevelRewardDBServer, AccountLevelRewardDB>();
        }

        private void ConfigureProgressMappings()
        {
            CreateMap<MissionProgressDBServer, MissionProgressDB>();
            CreateMap<AttendanceHistoryDBServer, AttendanceHistoryDB>();
            CreateMap<AcademyDBServer, AcademyDB>();
            CreateMap<AcademyLocationDBServer, AcademyLocationDB>();
            CreateMap<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>();
            CreateMap<CampaignChapterClearRewardHistoryDBServer, CampaignChapterClearRewardHistoryDB>();
            CreateMap<StrategyObjectHistoryDBServer, StrategyObjectHistoryDB>();
            CreateMap<ScenarioHistoryDBServer, ScenarioHistoryDB>();
            CreateMap<ScenarioGroupHistoryDBServer, ScenarioGroupHistoryDB>();
            CreateMap<CampaignStageHistoryDBServer, CampaignStageHistoryDB>();
            CreateMap<WeekDungeonStageHistoryDBServer, WeekDungeonStageHistoryDB>();
            CreateMap<SchoolDungeonStageHistoryDBServer, SchoolDungeonStageHistoryDB>();
            CreateMap<MomoTalkOutLineDBServer, MomoTalkOutLineDB>();
            CreateMap<MomoTalkChoiceDBServer, MomoTalkChoiceDB>();
            CreateMap<EventContentPermanentDBServer, EventContentPermanentDB>();
            CreateMap<StickerBookDBServer, StickerBookDB>();
        }

        private void ConfigureContentMappings()
        {
            CreateMap<SingleRaidLobbyInfoDBServer, SingleRaidLobbyInfoDB>();
            CreateMap<EliminateRaidLobbyInfoDBServer, EliminateRaidLobbyInfoDB>();
            CreateMap<RaidDBServer, RaidDB>();
            CreateMap<RaidBattleDBServer, RaidBattleDB>();
            CreateMap<TimeAttackDungeonRoomDBServer, TimeAttackDungeonRoomDB>();
            CreateMap<TimeAttackDungeonBattleHistoryDBServer, TimeAttackDungeonBattleHistoryDB>();
            CreateMap<MultiFloorRaidDBServer, MultiFloorRaidDB>();
            CreateMap<WorldRaidLocalBossDBServer, WorldRaidLocalBossDB>();
            CreateMap<WorldRaidBossListInfoDBServer, WorldRaidBossListInfoDB>();
            CreateMap<WorldRaidClearHistoryDBServer, WorldRaidClearHistoryDB>();
        }

        private void ConfigureReverseMappings()
        {
            CreateMap<AccountDB, AccountDBServer>();
            CreateMap<AccountCurrencyDB, AccountCurrencyDBServer>();
            CreateMap<ItemDB, ItemDBServer>();
            CreateMap<CharacterDB, CharacterDBServer>();
            CreateMap<EquipmentDB, EquipmentDBServer>();
            CreateMap<WeaponDB, WeaponDBServer>();
            CreateMap<GearDB, GearDBServer>();
            CreateMap<EchelonDB, EchelonDBServer>();
            CreateMap<EchelonPresetDB, EchelonPresetDBServer>();
            CreateMap<EchelonPresetGroupDB, EchelonPresetGroupDBServer>();
            CreateMap<CafeDB, CafeDBServer>();
            CreateMap<FurnitureDB, FurnitureDBServer>();
            CreateMap<MemoryLobbyDB, MemoryLobbyDBServer>();
            CreateMap<MailDB, MailDBServer>();
            CreateMap<EmblemDB, EmblemDBServer>();
            CreateMap<IdCardBackgroundDB, IdCardBackgroundDBServer>();
            CreateMap<CostumeDB, CostumeDBServer>();
            CreateMap<StickerDB, StickerDBServer>();
            CreateMap<AccountAttachmentDB, AccountAttachmentDBServer>();
        }

        private void ConfigureNestedDTOMappings()
        {
            CreateMap<TimeAttackDungeonCharacterDBServer, TimeAttackDungeonCharacterDB>();
            CreateMap<RaidBossDBServer, RaidBossDB>();
            CreateMap<WorldRaidWorldBossDBServer, WorldRaidWorldBossDB>();
            CreateMap<CafeProductionDBServer, CafeProductionDB>();
            CreateMap<VisitingCharacterDBServer, VisitingCharacterDB>();
            CreateMap<CafeDBServer.CafeCharacterDBServer, CafeDB.CafeCharacterDB>();
            CreateMap<CafeProductionDBServer.CafeProductionParcelInfoServer, CafeProductionDB.CafeProductionParcelInfo>();

            CreateMap<TimeAttackDungeonCharacterDB, TimeAttackDungeonCharacterDBServer>();
            CreateMap<RaidBossDB, RaidBossDBServer>();
            CreateMap<CafeProductionDB, CafeProductionDBServer>();
            CreateMap<VisitingCharacterDB, VisitingCharacterDBServer>();
            CreateMap<CafeDB.CafeCharacterDB, CafeDBServer.CafeCharacterDBServer>();
            CreateMap<CafeProductionDB.CafeProductionParcelInfo, CafeProductionDBServer.CafeProductionParcelInfoServer>();
        }
    }
}


