using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShittimServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameSettings = table.Column<string>(type: "TEXT", nullable: false),
                    ContentInfo = table.Column<string>(type: "TEXT", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", nullable: false),
                    CallName = table.Column<string>(type: "TEXT", nullable: true),
                    DevId = table.Column<string>(type: "TEXT", nullable: true),
                    PublisherAccountId = table.Column<long>(type: "INTEGER", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Exp = table.Column<long>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    LobbyMode = table.Column<int>(type: "INTEGER", nullable: false),
                    RepresentCharacterServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    MemoryLobbyUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    LastConnectTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BirthDay = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CallNameUpdateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RetentionDays = table.Column<int>(type: "INTEGER", nullable: true),
                    VIPLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UnReadMailCount = table.Column<int>(type: "INTEGER", nullable: true),
                    LinkRewardDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "AccountTutorials",
                columns: table => new
                {
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    TutorialIds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTutorials", x => x.AccountServerId);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uid = table.Column<long>(type: "INTEGER", nullable: false),
                    NpSN = table.Column<long>(type: "INTEGER", nullable: false),
                    NpToken = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorldRaidBossListInfos",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    WorldBossDB_GroupId = table.Column<long>(type: "INTEGER", nullable: true),
                    WorldBossDB_HP = table.Column<long>(type: "INTEGER", nullable: true),
                    WorldBossDB_Participants = table.Column<long>(type: "INTEGER", nullable: true),
                    LocalBossDBs = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldRaidBossListInfos", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "Academies",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ZoneVisitCharacterDBs = table.Column<string>(type: "TEXT", nullable: true),
                    ZoneScheduleGroupRecords = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Academies", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Academies_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcademyLocations",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    LocationId = table.Column<long>(type: "INTEGER", nullable: false),
                    Rank = table.Column<long>(type: "INTEGER", nullable: false),
                    Exp = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademyLocations", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_AcademyLocations_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountAttachments",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    EmblemUniqueId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountAttachments", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_AccountAttachments_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccountLevelRewards",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    RewardId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLevelRewards", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_AccountLevelRewards_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceHistories",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    AttendanceBookUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    AttendedDay = table.Column<string>(type: "TEXT", nullable: true),
                    Expired = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastAttendedDay = table.Column<long>(type: "INTEGER", nullable: false),
                    LastAttendedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AttendedDayNullable = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceHistories", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_AttendanceHistories_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BattleSummaries",
                columns: table => new
                {
                    BattleId = table.Column<string>(type: "TEXT", nullable: false),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SnapshotId = table.Column<long>(type: "INTEGER", nullable: false),
                    BossDatas = table.Column<string>(type: "TEXT", nullable: false),
                    Characters = table.Column<string>(type: "TEXT", nullable: false),
                    RaidMembers = table.Column<string>(type: "TEXT", nullable: false),
                    ElapsedRealtime = table.Column<float>(type: "REAL", nullable: false),
                    EndFrame = table.Column<long>(type: "INTEGER", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleSummaries", x => x.BattleId);
                    table.ForeignKey(
                        name: "FK_BattleSummaries_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cafes",
                columns: table => new
                {
                    CafeDBId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    CafeId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    CafeRank = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSummonDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CafeVisitCharacterDBs = table.Column<string>(type: "TEXT", nullable: false),
                    FurnitureDBs = table.Column<string>(type: "TEXT", nullable: false),
                    ProductionDB = table.Column<string>(type: "TEXT", nullable: true),
                    ProductionAppliedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cafes", x => x.CafeDBId);
                    table.ForeignKey(
                        name: "FK_Cafes_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignChapterClearRewardHistories",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChapterUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    RewardType = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiveDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignChapterClearRewardHistories", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_CampaignChapterClearRewardHistories_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignMainStageSaves",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContentType = table.Column<int>(type: "INTEGER", nullable: false),
                    CampaignState = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentTurn = table.Column<int>(type: "INTEGER", nullable: false),
                    EnemyClearCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastEnemyEntityId = table.Column<int>(type: "INTEGER", nullable: false),
                    TacticRankSCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EnemyInfos = table.Column<string>(type: "TEXT", nullable: false),
                    EchelonInfos = table.Column<string>(type: "TEXT", nullable: false),
                    WithdrawInfos = table.Column<string>(type: "TEXT", nullable: false),
                    StrategyObjects = table.Column<string>(type: "TEXT", nullable: false),
                    StrategyObjectRewards = table.Column<string>(type: "TEXT", nullable: false),
                    StrategyObjectHistory = table.Column<string>(type: "TEXT", nullable: false),
                    ActivatedHexaEventsAndConditions = table.Column<string>(type: "TEXT", nullable: false),
                    HexaEventDelayedExecutions = table.Column<string>(type: "TEXT", nullable: false),
                    TileMapStates = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayInfos = table.Column<string>(type: "TEXT", nullable: false),
                    DeployedEchelonInfos = table.Column<string>(type: "TEXT", nullable: false),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StageUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    LastEnterStageEchelonNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    StageEntranceFee = table.Column<string>(type: "TEXT", nullable: false),
                    EnemyKillCountByUniqueId = table.Column<string>(type: "TEXT", nullable: false),
                    TacticClearTimeMscSum = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountLevelWhenCreateDB = table.Column<long>(type: "INTEGER", nullable: false),
                    BIEchelon = table.Column<string>(type: "TEXT", nullable: true),
                    BIEchelon1 = table.Column<string>(type: "TEXT", nullable: true),
                    BIEchelon2 = table.Column<string>(type: "TEXT", nullable: true),
                    BIEchelon3 = table.Column<string>(type: "TEXT", nullable: true),
                    BIEchelon4 = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMainStageSaves", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_CampaignMainStageSaves_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignStageHistories",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    StoryUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChapterUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    StageUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    TacticClearCountWithRankSRecord = table.Column<long>(type: "INTEGER", nullable: false),
                    ClearTurnRecord = table.Column<long>(type: "INTEGER", nullable: false),
                    BestStarRecord = table.Column<long>(type: "INTEGER", nullable: false),
                    Star1Flag = table.Column<bool>(type: "INTEGER", nullable: false),
                    Star2Flag = table.Column<bool>(type: "INTEGER", nullable: false),
                    Star3Flag = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastPlay = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TodayPlayCount = table.Column<long>(type: "INTEGER", nullable: false),
                    TodayPurchasePlayCountHardStage = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstClearRewardReceive = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StarRewardReceive = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsClearedEver = table.Column<bool>(type: "INTEGER", nullable: false),
                    TodayPlayCountForUI = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignStageHistories", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_CampaignStageHistories_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    StarGrade = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Exp = table.Column<long>(type: "INTEGER", nullable: false),
                    FavorRank = table.Column<int>(type: "INTEGER", nullable: false),
                    FavorExp = table.Column<long>(type: "INTEGER", nullable: false),
                    PublicSkillLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    ExSkillLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    PassiveSkillLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtraPassiveSkillLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaderSkillLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    EquipmentServerIds = table.Column<string>(type: "TEXT", nullable: false),
                    PotentialStats = table.Column<string>(type: "TEXT", nullable: false),
                    EquipmentSlotAndDBIds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Characters_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Costumes",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    BoundCharacterServerId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Costumes", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Costumes_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountLevel = table.Column<long>(type: "INTEGER", nullable: false),
                    AcademyLocationRankSum = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrencyDict = table.Column<string>(type: "TEXT", nullable: false),
                    UpdateTimeDict = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Currencies_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EchelonPresetGroups",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    GroupIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    ExtensionType = table.Column<int>(type: "INTEGER", nullable: true),
                    GroupLabel = table.Column<string>(type: "TEXT", nullable: true),
                    PresetDBs = table.Column<string>(type: "TEXT", nullable: true),
                    Item = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EchelonPresetGroups", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_EchelonPresetGroups_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EchelonPresets",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    GroupIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: true),
                    LeaderUniqueId = table.Column<long>(type: "INTEGER", nullable: true),
                    TSSInteractionUniqueId = table.Column<long>(type: "INTEGER", nullable: true),
                    StrikerUniqueIds = table.Column<string>(type: "TEXT", nullable: true),
                    SpecialUniqueIds = table.Column<string>(type: "TEXT", nullable: true),
                    CombatStyleIndex = table.Column<string>(type: "TEXT", nullable: true),
                    MulliganUniqueIds = table.Column<string>(type: "TEXT", nullable: true),
                    ExtensionType = table.Column<int>(type: "INTEGER", nullable: true),
                    StrikerSlotCount = table.Column<int>(type: "INTEGER", nullable: true),
                    SpecialSlotCount = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EchelonPresets", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_EchelonPresets_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Echelons",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    EchelonType = table.Column<int>(type: "INTEGER", nullable: false),
                    EchelonNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    ExtensionType = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaderServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    MainSlotCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SupportSlotCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MainSlotServerIds = table.Column<string>(type: "TEXT", nullable: false),
                    SupportSlotServerIds = table.Column<string>(type: "TEXT", nullable: false),
                    TSSInteractionServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UsingFlag = table.Column<int>(type: "INTEGER", nullable: false),
                    IsUsing = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllCharacterServerIds = table.Column<string>(type: "TEXT", nullable: false),
                    AllCharacterWithoutTSSServerIds = table.Column<string>(type: "TEXT", nullable: false),
                    AllCharacterWithEmptyServerIds = table.Column<string>(type: "TEXT", nullable: false),
                    BattleCharacterServerIds = table.Column<string>(type: "TEXT", nullable: false),
                    SkillCardMulliganCharacterIds = table.Column<string>(type: "TEXT", nullable: false),
                    CombatStyleIndex = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Echelons", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Echelons_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EliminateRaidLobbyInfos",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OpenedBossGroups = table.Column<string>(type: "TEXT", nullable: false),
                    BestRankingPointPerBossGroup = table.Column<string>(type: "TEXT", nullable: false),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    Ranking = table.Column<long>(type: "INTEGER", nullable: false),
                    BestRankingPoint = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalRankingPoint = table.Column<long>(type: "INTEGER", nullable: false),
                    ReceivedRankingRewardId = table.Column<long>(type: "INTEGER", nullable: false),
                    CanReceiveRankingReward = table.Column<bool>(type: "INTEGER", nullable: false),
                    PlayingRaidDB = table.Column<string>(type: "TEXT", nullable: true),
                    ReceiveRewardIds = table.Column<string>(type: "TEXT", nullable: false),
                    ReceiveLimitedRewardIds = table.Column<string>(type: "TEXT", nullable: false),
                    ParticipateCharacterServerIds = table.Column<string>(type: "TEXT", nullable: false),
                    PlayableHighestDifficulty = table.Column<string>(type: "TEXT", nullable: false),
                    SweepPointByRaidUniqueId = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeasonEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SettlementEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextSeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    NextSeasonStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextSeasonEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextSettlementEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClanAssistUseInfo = table.Column<string>(type: "TEXT", nullable: true),
                    RemainFailCompensation = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EliminateRaidLobbyInfos", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_EliminateRaidLobbyInfos_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Emblems",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    ReceiveDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emblems", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Emblems_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Equipments",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Exp = table.Column<long>(type: "INTEGER", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    BoundCharacterServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    StackCount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipments", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Equipments_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventContentPermanents",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    EventContentId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsStageAllClear = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsReceivedCharacterReward = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventContentPermanents", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_EventContentPermanents_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Furnitures",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Location = table.Column<int>(type: "INTEGER", nullable: false),
                    CafeDBId = table.Column<long>(type: "INTEGER", nullable: false),
                    PositionX = table.Column<float>(type: "REAL", nullable: false),
                    PositionY = table.Column<float>(type: "REAL", nullable: false),
                    Rotation = table.Column<float>(type: "REAL", nullable: false),
                    ItemDeploySequence = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    StackCount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Furnitures", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Furnitures_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Gears",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Exp = table.Column<long>(type: "INTEGER", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    SlotIndex = table.Column<long>(type: "INTEGER", nullable: false),
                    BoundCharacterServerId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gears", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Gears_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdCardBackgrounds",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdCardBackgrounds", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_IdCardBackgrounds_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    StackCount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Items_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mails",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    Sender = table.Column<string>(type: "TEXT", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: false),
                    LocalizedSender = table.Column<string>(type: "TEXT", nullable: false),
                    LocalizedComment = table.Column<string>(type: "TEXT", nullable: false),
                    SendDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpireDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ParcelInfos = table.Column<string>(type: "TEXT", nullable: true),
                    RemainParcelInfos = table.Column<string>(type: "TEXT", nullable: true),
                    IsRefresher = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mails", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Mails_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemoryLobbies",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    MemoryLobbyUniqueId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoryLobbies", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_MemoryLobbies_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MissionProgresses",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    MissionUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    Complete = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProgressParameters = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionProgresses", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_MissionProgresses_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MomoTalkChoices",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterDBId = table.Column<long>(type: "INTEGER", nullable: false),
                    MessageGroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChosenMessageId = table.Column<long>(type: "INTEGER", nullable: true),
                    ChosenDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MomoTalkChoices", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_MomoTalkChoices_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MomoTalkOutLines",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterDBId = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<long>(type: "INTEGER", nullable: false),
                    LatestMessageGroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChosenMessageId = table.Column<long>(type: "INTEGER", nullable: true),
                    ScheduleIds = table.Column<string>(type: "TEXT", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MomoTalkOutLines", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_MomoTalkOutLines_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MultiFloorRaids",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    ClearedDifficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    LastClearDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RewardDifficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    LastRewardDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClearBattleFrame = table.Column<int>(type: "INTEGER", nullable: false),
                    AllCleared = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasReceivableRewards = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalReceivableRewards = table.Column<string>(type: "TEXT", nullable: true),
                    TotalReceivedRewards = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MultiFloorRaids", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_MultiFloorRaids_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RaidBattles",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<int>(type: "INTEGER", nullable: false),
                    RaidUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    RaidBossIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentBossHP = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentBossGroggy = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentBossAIPhase = table.Column<long>(type: "INTEGER", nullable: false),
                    BIEchelon = table.Column<string>(type: "TEXT", nullable: false),
                    IsClear = table.Column<bool>(type: "INTEGER", nullable: false),
                    RaidMembers = table.Column<string>(type: "TEXT", nullable: false),
                    SubPartsHPs = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidBattles", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_RaidBattles_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Raids",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    Owner = table.Column<string>(type: "TEXT", nullable: true),
                    ContentType = table.Column<int>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    Begin = table.Column<DateTime>(type: "TEXT", nullable: false),
                    End = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlayerCount = table.Column<long>(type: "INTEGER", nullable: false),
                    BossGroup = table.Column<string>(type: "TEXT", nullable: true),
                    BossDifficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    LastBossIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    SecretCode = table.Column<string>(type: "TEXT", nullable: false),
                    RaidState = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPractice = table.Column<bool>(type: "INTEGER", nullable: false),
                    RaidBossDBs = table.Column<string>(type: "TEXT", nullable: false),
                    ParticipateCharacterServerIds = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnterRoom = table.Column<bool>(type: "INTEGER", nullable: false),
                    SessionHitPoint = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountLevelWhenCreateDB = table.Column<long>(type: "INTEGER", nullable: false),
                    ClanAssistUsed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Raids", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Raids_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RaidSummaries",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    RaidStageId = table.Column<long>(type: "INTEGER", nullable: false),
                    SnapshotId = table.Column<long>(type: "INTEGER", nullable: false),
                    RaidDBId = table.Column<long>(type: "INTEGER", nullable: false),
                    BattleRaidDBId = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentTeam = table.Column<long>(type: "INTEGER", nullable: false),
                    IsMock = table.Column<bool>(type: "INTEGER", nullable: false),
                    Score = table.Column<long>(type: "INTEGER", nullable: false),
                    BattleStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<int>(type: "INTEGER", nullable: false),
                    BattleSummaryIds = table.Column<string>(type: "TEXT", nullable: false),
                    BattleSnapshotDatas = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RaidSummaries", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_RaidSummaries_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioGroupHistories",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    ScenarioGroupUqniueId = table.Column<long>(type: "INTEGER", nullable: false),
                    ScenarioType = table.Column<long>(type: "INTEGER", nullable: false),
                    EventContentId = table.Column<long>(type: "INTEGER", nullable: true),
                    ClearDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsReturn = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioGroupHistories", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_ScenarioGroupHistories_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioHistories",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    ScenarioUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    ClearDateTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioHistories", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_ScenarioHistories_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchoolDungeonStageHistories",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    StageUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    BestStarRecord = table.Column<long>(type: "INTEGER", nullable: false),
                    StarFlags = table.Column<string>(type: "TEXT", nullable: false),
                    Star1Flag = table.Column<bool>(type: "INTEGER", nullable: false),
                    Star2Flag = table.Column<bool>(type: "INTEGER", nullable: false),
                    Star3Flag = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsClearedEver = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolDungeonStageHistories", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_SchoolDungeonStageHistories_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SingleRaidLobbyInfos",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClearDifficulty = table.Column<string>(type: "TEXT", nullable: false),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    Ranking = table.Column<long>(type: "INTEGER", nullable: false),
                    BestRankingPoint = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalRankingPoint = table.Column<long>(type: "INTEGER", nullable: false),
                    ReceivedRankingRewardId = table.Column<long>(type: "INTEGER", nullable: false),
                    CanReceiveRankingReward = table.Column<bool>(type: "INTEGER", nullable: false),
                    PlayingRaidDB = table.Column<string>(type: "TEXT", nullable: true),
                    ReceiveRewardIds = table.Column<string>(type: "TEXT", nullable: false),
                    ReceiveLimitedRewardIds = table.Column<string>(type: "TEXT", nullable: false),
                    ParticipateCharacterServerIds = table.Column<string>(type: "TEXT", nullable: false),
                    PlayableHighestDifficulty = table.Column<string>(type: "TEXT", nullable: false),
                    SweepPointByRaidUniqueId = table.Column<string>(type: "TEXT", nullable: false),
                    SeasonStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SeasonEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SettlementEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextSeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    NextSeasonStartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextSeasonEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextSettlementEndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClanAssistUseInfo = table.Column<string>(type: "TEXT", nullable: true),
                    RemainFailCompensation = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SingleRaidLobbyInfos", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_SingleRaidLobbyInfos_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StickerBooks",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    UnusedStickerDBs = table.Column<string>(type: "TEXT", nullable: true),
                    UsedStickerDBs = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StickerBooks", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_StickerBooks_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Stickers",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    StickerUniqueId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stickers", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Stickers_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StrategyObjectHistories",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    StrategyObjectId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyObjectHistories", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_StrategyObjectHistories_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeAttackDungeonBattleHistories",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    DungeonType = table.Column<int>(type: "INTEGER", nullable: false),
                    GeasId = table.Column<long>(type: "INTEGER", nullable: false),
                    DefaultPoint = table.Column<long>(type: "INTEGER", nullable: false),
                    ClearTimePoint = table.Column<long>(type: "INTEGER", nullable: false),
                    EndFrame = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalPoint = table.Column<long>(type: "INTEGER", nullable: false),
                    MainCharacterDBs = table.Column<string>(type: "TEXT", nullable: true),
                    SupportCharacterDBs = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeAttackDungeonBattleHistories", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_TimeAttackDungeonBattleHistories_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeAttackDungeonRooms",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountId = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    RoomId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RewardDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPractice = table.Column<bool>(type: "INTEGER", nullable: false),
                    SweepHistoryDates = table.Column<string>(type: "TEXT", nullable: true),
                    BattleHistoryDBs = table.Column<string>(type: "TEXT", nullable: true),
                    PlayCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPointSum = table.Column<long>(type: "INTEGER", nullable: false),
                    IsRewardReceived = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOpened = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanUseAssist = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPlayCountOver = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeAttackDungeonRooms", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_TimeAttackDungeonRooms_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Weapons",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Exp = table.Column<long>(type: "INTEGER", nullable: false),
                    StarGrade = table.Column<int>(type: "INTEGER", nullable: false),
                    BoundCharacterServerId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Weapons", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Weapons_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeekDungeonStageHistories",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    StageUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    StarGoalRecord = table.Column<string>(type: "TEXT", nullable: false),
                    IsCleardEver = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeekDungeonStageHistories", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_WeekDungeonStageHistories_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorldRaidClearHistories",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    RewardReceiveDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldRaidClearHistories", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_WorldRaidClearHistories_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorldRaidLocalBosses",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsScenario = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCleardEver = table.Column<bool>(type: "INTEGER", nullable: false),
                    TacticMscSum = table.Column<long>(type: "INTEGER", nullable: false),
                    RaidBattleDB = table.Column<string>(type: "TEXT", nullable: true),
                    IsContinue = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldRaidLocalBosses", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_WorldRaidLocalBosses_Accounts_AccountServerId",
                        column: x => x.AccountServerId,
                        principalTable: "Accounts",
                        principalColumn: "ServerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Academies_AccountServerId",
                table: "Academies",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_AcademyLocations_AccountServerId",
                table: "AcademyLocations",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountAttachments_AccountServerId",
                table: "AccountAttachments",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLevelRewards_AccountServerId",
                table: "AccountLevelRewards",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceHistories_AccountServerId",
                table: "AttendanceHistories",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleSummaries_AccountServerId",
                table: "BattleSummaries",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Cafes_AccountServerId",
                table: "Cafes",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignChapterClearRewardHistories_AccountServerId",
                table: "CampaignChapterClearRewardHistories",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMainStageSaves_AccountServerId",
                table: "CampaignMainStageSaves",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStageHistories_AccountServerId",
                table: "CampaignStageHistories",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_AccountServerId",
                table: "Characters",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Costumes_AccountServerId",
                table: "Costumes",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_AccountServerId",
                table: "Currencies",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EchelonPresetGroups_AccountServerId",
                table: "EchelonPresetGroups",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EchelonPresets_AccountServerId",
                table: "EchelonPresets",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Echelons_AccountServerId",
                table: "Echelons",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EliminateRaidLobbyInfos_AccountServerId",
                table: "EliminateRaidLobbyInfos",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Emblems_AccountServerId",
                table: "Emblems",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipments_AccountServerId",
                table: "Equipments",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_EventContentPermanents_AccountServerId",
                table: "EventContentPermanents",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Furnitures_AccountServerId",
                table: "Furnitures",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Gears_AccountServerId",
                table: "Gears",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_IdCardBackgrounds_AccountServerId",
                table: "IdCardBackgrounds",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_AccountServerId",
                table: "Items",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Mails_AccountServerId",
                table: "Mails",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoryLobbies_AccountServerId",
                table: "MemoryLobbies",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionProgresses_AccountServerId",
                table: "MissionProgresses",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_MomoTalkChoices_AccountServerId",
                table: "MomoTalkChoices",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_MomoTalkOutLines_AccountServerId",
                table: "MomoTalkOutLines",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_MultiFloorRaids_AccountServerId",
                table: "MultiFloorRaids",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidBattles_AccountServerId",
                table: "RaidBattles",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Raids_AccountServerId",
                table: "Raids",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_RaidSummaries_AccountServerId",
                table: "RaidSummaries",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioGroupHistories_AccountServerId",
                table: "ScenarioGroupHistories",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ScenarioHistories_AccountServerId",
                table: "ScenarioHistories",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolDungeonStageHistories_AccountServerId",
                table: "SchoolDungeonStageHistories",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_SingleRaidLobbyInfos_AccountServerId",
                table: "SingleRaidLobbyInfos",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_StickerBooks_AccountServerId",
                table: "StickerBooks",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Stickers_AccountServerId",
                table: "Stickers",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyObjectHistories_AccountServerId",
                table: "StrategyObjectHistories",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeAttackDungeonBattleHistories_AccountServerId",
                table: "TimeAttackDungeonBattleHistories",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeAttackDungeonRooms_AccountServerId",
                table: "TimeAttackDungeonRooms",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Weapons_AccountServerId",
                table: "Weapons",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_WeekDungeonStageHistories_AccountServerId",
                table: "WeekDungeonStageHistories",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldRaidClearHistories_AccountServerId",
                table: "WorldRaidClearHistories",
                column: "AccountServerId");

            migrationBuilder.CreateIndex(
                name: "IX_WorldRaidLocalBosses_AccountServerId",
                table: "WorldRaidLocalBosses",
                column: "AccountServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Academies");

            migrationBuilder.DropTable(
                name: "AcademyLocations");

            migrationBuilder.DropTable(
                name: "AccountAttachments");

            migrationBuilder.DropTable(
                name: "AccountLevelRewards");

            migrationBuilder.DropTable(
                name: "AccountTutorials");

            migrationBuilder.DropTable(
                name: "AttendanceHistories");

            migrationBuilder.DropTable(
                name: "BattleSummaries");

            migrationBuilder.DropTable(
                name: "Cafes");

            migrationBuilder.DropTable(
                name: "CampaignChapterClearRewardHistories");

            migrationBuilder.DropTable(
                name: "CampaignMainStageSaves");

            migrationBuilder.DropTable(
                name: "CampaignStageHistories");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Costumes");

            migrationBuilder.DropTable(
                name: "Currencies");

            migrationBuilder.DropTable(
                name: "EchelonPresetGroups");

            migrationBuilder.DropTable(
                name: "EchelonPresets");

            migrationBuilder.DropTable(
                name: "Echelons");

            migrationBuilder.DropTable(
                name: "EliminateRaidLobbyInfos");

            migrationBuilder.DropTable(
                name: "Emblems");

            migrationBuilder.DropTable(
                name: "Equipments");

            migrationBuilder.DropTable(
                name: "EventContentPermanents");

            migrationBuilder.DropTable(
                name: "Furnitures");

            migrationBuilder.DropTable(
                name: "Gears");

            migrationBuilder.DropTable(
                name: "IdCardBackgrounds");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "Mails");

            migrationBuilder.DropTable(
                name: "MemoryLobbies");

            migrationBuilder.DropTable(
                name: "MissionProgresses");

            migrationBuilder.DropTable(
                name: "MomoTalkChoices");

            migrationBuilder.DropTable(
                name: "MomoTalkOutLines");

            migrationBuilder.DropTable(
                name: "MultiFloorRaids");

            migrationBuilder.DropTable(
                name: "RaidBattles");

            migrationBuilder.DropTable(
                name: "Raids");

            migrationBuilder.DropTable(
                name: "RaidSummaries");

            migrationBuilder.DropTable(
                name: "ScenarioGroupHistories");

            migrationBuilder.DropTable(
                name: "ScenarioHistories");

            migrationBuilder.DropTable(
                name: "SchoolDungeonStageHistories");

            migrationBuilder.DropTable(
                name: "SingleRaidLobbyInfos");

            migrationBuilder.DropTable(
                name: "StickerBooks");

            migrationBuilder.DropTable(
                name: "Stickers");

            migrationBuilder.DropTable(
                name: "StrategyObjectHistories");

            migrationBuilder.DropTable(
                name: "TimeAttackDungeonBattleHistories");

            migrationBuilder.DropTable(
                name: "TimeAttackDungeonRooms");

            migrationBuilder.DropTable(
                name: "UserAccounts");

            migrationBuilder.DropTable(
                name: "Weapons");

            migrationBuilder.DropTable(
                name: "WeekDungeonStageHistories");

            migrationBuilder.DropTable(
                name: "WorldRaidBossListInfos");

            migrationBuilder.DropTable(
                name: "WorldRaidClearHistories");

            migrationBuilder.DropTable(
                name: "WorldRaidLocalBosses");

            migrationBuilder.DropTable(
                name: "Accounts");
        }
    }
}
