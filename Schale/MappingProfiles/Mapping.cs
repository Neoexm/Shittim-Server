using AutoMapper;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;

namespace Schale.MappingProfiles
{
    public class GameModelsMappingProfile : Profile
    {
        // Exact key set + order the official server serializes for AccountCurrencyDB.CurrencyDict (live captures, client 1.90): no Invalid/deprecated WeekDungeon tickets, no Max sentinel.
        private static readonly CurrencyTypes[] OfficialCurrencyKeys =
        {
            CurrencyTypes.Gem, CurrencyTypes.GemPaid, CurrencyTypes.GemBonus, CurrencyTypes.Gold,
            CurrencyTypes.ActionPoint, CurrencyTypes.AcademyTicket, CurrencyTypes.ArenaTicket,
            CurrencyTypes.RaidTicket, CurrencyTypes.WeekDungeonChaserATicket,
            CurrencyTypes.WeekDungeonChaserBTicket, CurrencyTypes.WeekDungeonChaserCTicket,
            CurrencyTypes.SchoolDungeonATicket, CurrencyTypes.SchoolDungeonBTicket,
            CurrencyTypes.SchoolDungeonCTicket, CurrencyTypes.TimeAttackDungeonTicket,
            CurrencyTypes.MasterCoin, CurrencyTypes.WorldRaidTicketA, CurrencyTypes.WorldRaidTicketB,
            CurrencyTypes.WorldRaidTicketC, CurrencyTypes.ChaserTotalTicket,
            CurrencyTypes.SchoolDungeonTotalTicket, CurrencyTypes.EliminateTicketA,
            CurrencyTypes.EliminateTicketB, CurrencyTypes.EliminateTicketC,
            CurrencyTypes.EliminateTicketD, CurrencyTypes.CafeSummonTicket1,
            CurrencyTypes.CafeSummonTicket2,
        };

        // Official UpdateTimeDict = the same set minus the four non-regenerating currencies.
        private static readonly CurrencyTypes[] OfficialCurrencyTimeKeys =
            OfficialCurrencyKeys
                .Where(c => c is not (CurrencyTypes.Gem or CurrencyTypes.GemPaid or CurrencyTypes.GemBonus or CurrencyTypes.Gold))
                .ToArray();

        // Official localized mail text carries exactly these languages, in this order (no Jp on GL).
        private static readonly Language[] OfficialMailLanguages =
        {
            Language.Kr, Language.En, Language.Th, Language.Tw,
        };

        private static Dictionary<CurrencyTypes, long> ToOfficialCurrencyDict(Dictionary<CurrencyTypes, long>? source)
            => OfficialCurrencyKeys.ToDictionary(k => k, k => source != null && source.TryGetValue(k, out var v) ? v : 0L);

        private static Dictionary<CurrencyTypes, DateTime> ToOfficialCurrencyTimeDict(Dictionary<CurrencyTypes, DateTime>? source)
            => OfficialCurrencyTimeKeys.ToDictionary(k => k, k => source != null && source.TryGetValue(k, out var v) ? v : DateTime.Now);

        private static Dictionary<Language, string>? ToOfficialLocalized(Dictionary<Language, string>? source)
            => source == null ? null : OfficialMailLanguages.ToDictionary(l => l, l => source.TryGetValue(l, out var v) ? v : string.Empty);

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
            CreateMap<AccountDBServer, AccountDB>()
                // Official emits RetentionDays: 0 even for fresh accounts.
                .ForMember(dest => dest.RetentionDays, opt => opt.MapFrom(_ => (int?)0));
            CreateMap<AccountCurrencyDBServer, AccountCurrencyDB>()
                .ForMember(dest => dest.CurrencyDict, opt => opt.MapFrom(src => ToOfficialCurrencyDict(src.CurrencyDict)))
                .ForMember(dest => dest.UpdateTimeDict, opt => opt.MapFrom(src => ToOfficialCurrencyTimeDict(src.UpdateTimeDict)));
            CreateMap<ItemDBServer, ItemDB>()
                .ForMember(dest => dest.CanConsume, opt => opt.Ignore());
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
            CreateMap<MailDBServer, MailDB>()
                .ForMember(dest => dest.LocalizedSender, opt => opt.MapFrom(src => ToOfficialLocalized(src.LocalizedSender)))
                .ForMember(dest => dest.LocalizedComment, opt => opt.MapFrom(src => ToOfficialLocalized(src.LocalizedComment)))
                // Official MailDB never carries RemainParcelInfos (absent, not []).
                .ForMember(dest => dest.RemainParcelInfos, opt => opt.MapFrom(src =>
                    src.RemainParcelInfos != null && src.RemainParcelInfos.Count > 0 ? src.RemainParcelInfos : null));
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
            // AcademyDB.ZoneScheduleGroupRecords is [OmitWhenEmpty]: AutoMapper always materialises an empty dictionary here, and the gateway drops it on the way out.
            CreateMap<AcademyDBServer, AcademyDB>();
            CreateMap<AcademyLocationDBServer, AcademyLocationDB>();
            CreateMap<CampaignMainStageSaveDBServer, CampaignMainStageSaveDB>();
            CreateMap<CampaignMainStageSaveDBServer, EventContentMainStageSaveDB>();
            CreateMap<CampaignMainStageSaveDBServer, StoryStrategyStageSaveDB>();
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
            CreateMap<SkipHistoryDBServer, SkipHistoryDB>();
            CreateMap<CraftInfoDBServer, CraftInfoDB>();
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

            // Import direction for the rows accountdata load restores.
            CreateMap<ScenarioHistoryDB, ScenarioHistoryDBServer>();
            CreateMap<ScenarioGroupHistoryDB, ScenarioGroupHistoryDBServer>();
            CreateMap<CampaignStageHistoryDB, CampaignStageHistoryDBServer>();
            CreateMap<CampaignChapterClearRewardHistoryDB, CampaignChapterClearRewardHistoryDBServer>();
            CreateMap<StrategyObjectHistoryDB, StrategyObjectHistoryDBServer>();

            CreateMap<MomoTalkOutLineDB, MomoTalkOutLineDBServer>();
            CreateMap<EventContentPermanentDB, EventContentPermanentDBServer>();
            CreateMap<StickerBookDB, StickerBookDBServer>();
            CreateMap<ShopFreeRecruitHistoryDB, ShopFreeRecruitHistoryDBServer>();
            CreateMap<CraftInfoDB, CraftInfoDBServer>();
            CreateMap<MultiFloorRaidDB, MultiFloorRaidDBServer>();
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


