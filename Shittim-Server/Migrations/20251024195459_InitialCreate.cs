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
                name: "Academies",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ZoneVisitCharacterIds = table.Column<string>(type: "TEXT", nullable: true),
                    ZoneScheduleGroupRecords = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Academies", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "AcademyLocations",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    LocationId = table.Column<long>(type: "INTEGER", nullable: false),
                    Rank = table.Column<int>(type: "INTEGER", nullable: false),
                    Exp = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademyLocations", x => x.ServerId);
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
                });

            migrationBuilder.CreateTable(
                name: "Cafes",
                columns: table => new
                {
                    CafeDBId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    CafeId = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUpdate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSummonDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CafeRank = table.Column<int>(type: "INTEGER", nullable: false),
                    IsNew = table.Column<bool>(type: "INTEGER", nullable: false),
                    CafeVisitCharacterDBs = table.Column<string>(type: "TEXT", nullable: true),
                    ProductionData = table.Column<string>(type: "TEXT", nullable: true),
                    LastProductionCollectTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cafes", x => x.CafeDBId);
                });

            migrationBuilder.CreateTable(
                name: "CampaignChapterRewards",
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
                    table.PrimaryKey("PK_CampaignChapterRewards", x => x.ServerId);
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
                    Star1Flag = table.Column<bool>(type: "INTEGER", nullable: false),
                    Star2Flag = table.Column<bool>(type: "INTEGER", nullable: false),
                    Star3Flag = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastPlay = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TodayPlayCount = table.Column<long>(type: "INTEGER", nullable: false),
                    TodayPurchasePlayCountHardStage = table.Column<long>(type: "INTEGER", nullable: false),
                    FirstClearRewardReceive = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StarRewardReceive = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TodayPlayCountForUI = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignStageHistories", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "Costumes",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Costumes", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrencyType = table.Column<int>(type: "INTEGER", nullable: false),
                    TransactionTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AmountChange = table.Column<long>(type: "INTEGER", nullable: false),
                    NewBalance = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EchelonPresetGroups",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    GroupIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ExtensionType = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupLabel = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EchelonPresetGroups", x => x.ServerId);
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
                    ExtensionType = table.Column<int>(type: "INTEGER", nullable: false),
                    LeaderUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    TSSInteractionUniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    StrikerUniqueIds = table.Column<string>(type: "TEXT", nullable: true),
                    SpecialUniqueIds = table.Column<string>(type: "TEXT", nullable: true),
                    CombatStyleIndex = table.Column<string>(type: "TEXT", nullable: true),
                    MulliganUniqueIds = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EchelonPresets", x => x.ServerId);
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
                    LeaderServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    MainSlotServerIds = table.Column<string>(type: "TEXT", nullable: true),
                    SupportSlotServerIds = table.Column<string>(type: "TEXT", nullable: true),
                    TSSServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UsingFlag = table.Column<int>(type: "INTEGER", nullable: false),
                    SkillCardMulliganCharacterIds = table.Column<string>(type: "TEXT", nullable: true),
                    CombatStyleIndex = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Echelons", x => x.ServerId);
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
                });

            migrationBuilder.CreateTable(
                name: "Equipments",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    StackCount = table.Column<long>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Exp = table.Column<long>(type: "INTEGER", nullable: false),
                    Tier = table.Column<int>(type: "INTEGER", nullable: false),
                    BoundCharacterServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsNew = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipments", x => x.ServerId);
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
                });

            migrationBuilder.CreateTable(
                name: "Furnitures",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    CafeDBId = table.Column<long>(type: "INTEGER", nullable: false),
                    Location = table.Column<int>(type: "INTEGER", nullable: false),
                    PositionX = table.Column<float>(type: "REAL", nullable: false),
                    PositionY = table.Column<float>(type: "REAL", nullable: false),
                    Rotation = table.Column<float>(type: "REAL", nullable: false),
                    ItemDeploySequence = table.Column<long>(type: "INTEGER", nullable: false),
                    StackCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Furnitures", x => x.ServerId);
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
                    BoundCharacterServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gears", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    StackCount = table.Column<long>(type: "INTEGER", nullable: false),
                    IsNew = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.ServerId);
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
                    Sender = table.Column<string>(type: "TEXT", nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    SendDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpireDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRead = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsReceived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParcelInfos = table.Column<string>(type: "TEXT", nullable: true),
                    RemainParcelInfos = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mails", x => x.ServerId);
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
                });

            migrationBuilder.CreateTable(
                name: "MissionProgresses",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    MissionId = table.Column<long>(type: "INTEGER", nullable: false),
                    ProgressCount = table.Column<long>(type: "INTEGER", nullable: false),
                    Complete = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiryTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionProgresses", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "MomoTalkOutlines",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<long>(type: "INTEGER", nullable: false),
                    LatestMessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsNew = table.Column<bool>(type: "INTEGER", nullable: false),
                    FavorLevel = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MomoTalkOutlines", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "MultiFloorRaids",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SeasonId = table.Column<long>(type: "INTEGER", nullable: false),
                    Difficulty = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MultiFloorRaids", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "ScenarioGroupHistories",
                columns: table => new
                {
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    ScenarioGroupId = table.Column<long>(type: "INTEGER", nullable: false),
                    ClearDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClearCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioGroupHistories", x => new { x.AccountServerId, x.ScenarioGroupId });
                });

            migrationBuilder.CreateTable(
                name: "ScenarioHistories",
                columns: table => new
                {
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    ScenarioId = table.Column<long>(type: "INTEGER", nullable: false),
                    ClearDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScenarioHistories", x => new { x.AccountServerId, x.ScenarioId });
                });

            migrationBuilder.CreateTable(
                name: "StickerBooks",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UnusedStickerIds = table.Column<string>(type: "TEXT", nullable: true),
                    UsedStickerIds = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StickerBooks", x => x.ServerId);
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
                    IsPractice = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeAttackDungeonRooms", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    UserKey = table.Column<string>(type: "TEXT", nullable: true),
                    Gid = table.Column<string>(type: "TEXT", nullable: true),
                    Guid = table.Column<string>(type: "TEXT", nullable: true),
                    NpSN = table.Column<string>(type: "TEXT", nullable: true),
                    UmKey = table.Column<string>(type: "TEXT", nullable: true),
                    PlatformType = table.Column<string>(type: "TEXT", nullable: true),
                    PlatformUserId = table.Column<string>(type: "TEXT", nullable: true),
                    SteamId = table.Column<string>(type: "TEXT", nullable: true),
                    PublisherAccountId = table.Column<string>(type: "TEXT", nullable: true),
                    Nickname = table.Column<string>(type: "TEXT", nullable: true),
                    CallName = table.Column<string>(type: "TEXT", nullable: true),
                    CallNameKatakana = table.Column<string>(type: "TEXT", nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Attribute = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    LastLogin = table.Column<long>(type: "INTEGER", nullable: true),
                    IsGuest = table.Column<bool>(type: "INTEGER", nullable: false),
                    NeedsNameSetup = table.Column<bool>(type: "INTEGER", nullable: false),
                    ExtraData = table.Column<string>(type: "TEXT", nullable: true),
                    IsNew = table.Column<bool>(type: "INTEGER", nullable: false),
                    RepresentCharacterServerId = table.Column<int>(type: "INTEGER", nullable: false),
                    LastConnectTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GameSettings_EnableMultiFloorRaid = table.Column<bool>(type: "INTEGER", nullable: true),
                    GameSettings_ForceDateTime = table.Column<bool>(type: "INTEGER", nullable: true),
                    GameSettings_ForceDateTimeOffset = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GameSettings_CurrentDateTime = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    GameSettings_Id = table.Column<int>(type: "INTEGER", nullable: true),
                    ContentInfo_ArenaDataInfo_SeasonId = table.Column<long>(type: "INTEGER", nullable: true),
                    ContentInfo_MultiFloorRaidDataInfo_SeasonId = table.Column<long>(type: "INTEGER", nullable: true),
                    ContentInfo_Id = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Weapons",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    UniqueId = table.Column<long>(type: "INTEGER", nullable: false),
                    StarGrade = table.Column<int>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Exp = table.Column<long>(type: "INTEGER", nullable: false),
                    BoundCharacterServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Weapons", x => x.ServerId);
                });

            migrationBuilder.CreateTable(
                name: "AccountCurrencies",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccountLevel = table.Column<long>(type: "INTEGER", nullable: false),
                    AcademyLocationRankSum = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrencyDict = table.Column<string>(type: "TEXT", nullable: true),
                    UpdateTimeDict = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountCurrencies", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_AccountCurrencies_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountLevelRewards",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    RewardId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountLevelRewards", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_AccountLevelRewards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountTutorials",
                columns: table => new
                {
                    AccountServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TutorialIds = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTutorials", x => x.AccountServerId);
                    table.ForeignKey(
                        name: "FK_AccountTutorials_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
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
                    IsNew = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    EquipmentServerIds = table.Column<string>(type: "TEXT", nullable: true),
                    PotentialStats = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.ServerId);
                    table.ForeignKey(
                        name: "FK_Characters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountCurrencies_UserId",
                table: "AccountCurrencies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountLevelRewards_UserId",
                table: "AccountLevelRewards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTutorials_UserId",
                table: "AccountTutorials",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_UserId",
                table: "Characters",
                column: "UserId");
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
                name: "AccountCurrencies");

            migrationBuilder.DropTable(
                name: "AccountLevelRewards");

            migrationBuilder.DropTable(
                name: "AccountTutorials");

            migrationBuilder.DropTable(
                name: "Cafes");

            migrationBuilder.DropTable(
                name: "CampaignChapterRewards");

            migrationBuilder.DropTable(
                name: "CampaignStageHistories");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Costumes");

            migrationBuilder.DropTable(
                name: "CurrencyTransactions");

            migrationBuilder.DropTable(
                name: "EchelonPresetGroups");

            migrationBuilder.DropTable(
                name: "EchelonPresets");

            migrationBuilder.DropTable(
                name: "Echelons");

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
                name: "Items");

            migrationBuilder.DropTable(
                name: "Mails");

            migrationBuilder.DropTable(
                name: "MemoryLobbies");

            migrationBuilder.DropTable(
                name: "MissionProgresses");

            migrationBuilder.DropTable(
                name: "MomoTalkOutlines");

            migrationBuilder.DropTable(
                name: "MultiFloorRaids");

            migrationBuilder.DropTable(
                name: "ScenarioGroupHistories");

            migrationBuilder.DropTable(
                name: "ScenarioHistories");

            migrationBuilder.DropTable(
                name: "StickerBooks");

            migrationBuilder.DropTable(
                name: "StrategyObjectHistories");

            migrationBuilder.DropTable(
                name: "TimeAttackDungeonRooms");

            migrationBuilder.DropTable(
                name: "Weapons");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
