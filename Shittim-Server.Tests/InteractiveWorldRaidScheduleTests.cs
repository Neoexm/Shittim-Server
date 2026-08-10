using BlueArchiveAPI.Services;
using Google.FlatBuffers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.Data;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core.NetworkProtocol.Handlers;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// 854-style seasons keep a second layer of dates in InteractiveWorldRaidSeasonManageExcel - per-phase windows plus per-boss spawn/eliminate lists - and their pools live in InteractiveWorldRaidBossGroupExcel. Same throwaway SQLCipher db shape as the classic world raid tests.
[Collection("exceldb-paths")]
public class InteractiveWorldRaidScheduleTests : IDisposable
{
    private const string Key = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"shittim-iwr-{Guid.NewGuid():N}");
    private readonly string _clientDb;
    private readonly string _originalDumpedDir = ExcelTableService.DumpedDir;
    private readonly Dictionary<string, string?> _savedFiles = new();

    public InteractiveWorldRaidScheduleTests()
    {
        var dumped = Path.Combine(_dir, "Dumped");
        _clientDb = Path.Combine(_dir, "client", "ExcelDB.db");
        Directory.CreateDirectory(dumped);
        Directory.CreateDirectory(Path.GetDirectoryName(_clientDb)!);

        WriteDb(_clientDb);
        File.Copy(_clientDb, Path.Combine(dumped, "ExcelDB.db"));

        foreach (var name in new[] { "worldraid_manifest.json", "worldraid_state.json", "worldraid_pending.json", "event_schedule.json" })
        {
            var path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", name));
            _savedFiles[path] = File.Exists(path) ? File.ReadAllText(path) : null;
        }

        ExcelTableService.DumpedDir = dumped;
        Environment.SetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY", Key);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_EXCELDB_PATH", _clientDb);
        WorldRaidService.SetManifest(null, new ExcelTableService());
    }

    [Fact]
    public void TheManifestWindowLandsOnTheInteractiveSeasonRow()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Live854(), excel);
        EventScheduleService.Set([], excel);

        var row = ReadSeasons().Single(r => r.EventContentType == EventContentType.InteractiveWorldRaid);
        Assert.Equal(Local("2026-08-08 11:00:00"), row.BeforehandExposedTime);
        Assert.Equal(Local("2026-08-10 11:00:00"), row.EventContentOpenTime);
        Assert.Equal(Local("2026-09-14 10:59:59"), row.EventContentCloseTime);
        Assert.Equal(Local("2026-09-21 10:59:59"), row.ExtensionTime);
    }

    [Fact]
    public void TheRestOfTheSeasonMovesWithTheRaidRow()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Live854(), excel);
        EventScheduleService.Set([], excel);

        var shop = ReadSeasons().Single(r => r.EventContentType == EventContentType.Shop);
        Assert.Equal(Local("2026-08-10 11:00:00"), shop.EventContentOpenTime);
        Assert.Equal(Local("2026-09-14 10:59:59"), shop.EventContentCloseTime);
    }

    [Fact]
    public void ManifestBossWindowsLandOnThePhaseRows()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Live854(), excel);
        EventScheduleService.Set([], excel);

        var phases = ReadPhases();

        // no start condition means the phase opens with the season itself
        var opening = phases.Single(p => p.PhaseId == 85400);
        Assert.Equal(Local("2026-08-10 11:00:00"), opening.PhaseStartTime);
        Assert.Equal(Local("2026-08-24 10:59:59"), opening.PhaseEndTime);
        Assert.All(opening.BossSpawnTime, t => Assert.Equal(Local("2026-08-11 11:00:00"), t));
        Assert.All(opening.EliminateTime, t => Assert.Equal(Local("2026-08-24 10:59:59"), t));

        // conditioned phases open a day before their first boss, the lead day every shipped phase had
        var gated = phases.Single(p => p.PhaseId == 85401);
        Assert.Equal(DateTime.Parse(Local("2026-08-25 11:00:00")).AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss"), gated.PhaseStartTime);
        Assert.Equal(Local("2026-09-14 10:59:59"), gated.PhaseEndTime);
        Assert.All(gated.BossSpawnTime, t => Assert.Equal(Local("2026-08-25 11:00:00"), t));

        // the replay reopens everything once the season proper closes, and runs to the extension
        var replay = phases.Single(p => p.PhaseId == 85403);
        Assert.Equal(Local("2026-09-14 10:59:59"), replay.PhaseStartTime);
        Assert.Equal(Local("2026-09-21 10:59:59"), replay.PhaseEndTime);
        Assert.All(replay.BossSpawnTime, t => Assert.Equal(Local("2026-09-14 10:59:59"), t));
        Assert.All(replay.EliminateTime, t => Assert.Equal(Local("2026-09-21 10:59:59"), t));
    }

    [Fact]
    public void EndingTheRaidPutsTheShippedPhaseDatesBack()
    {
        var excel = new ExcelTableService();
        WorldRaidService.SetManifest(Live854(), excel);
        EventScheduleService.Set([], excel);
        WorldRaidService.SetManifest(null, excel);
        EventScheduleService.Set([], excel);

        var opening = ReadPhases().Single(p => p.PhaseId == 85400);
        Assert.Equal("2026-05-26 11:00:00", opening.PhaseStartTime);
        Assert.Equal("2026-06-09 10:59:59", opening.PhaseEndTime);
        Assert.Equal("2026-05-27 11:00:00", opening.BossSpawnTime[0]);

        var replay = ReadPhases().Single(p => p.PhaseId == 85403);
        Assert.Equal("2026-07-14 11:00:00", replay.PhaseStartTime);
        Assert.Equal("2999-12-31 23:59:59", replay.PhaseEndTime);
    }

    [Fact]
    public void ABareManifestSeedsPoolsOutOfTheInteractiveGroups()
    {
        var raid = Live854();
        raid.bosses = [];
        WorldRaidService.SetManifest(raid, new ExcelTableService());

        // the asia column, which is the one the client divides its world bar by - the rows carry jp and global too and neither is the pool
        Assert.Equal(6_880_000_000_000, WorldRaidService.RemainingHP(8540000));
        Assert.Equal(6_300_000_000_000, WorldRaidService.RemainingHP(8540100));
        // listed only in the second phase - the season's groups are the union of every phase's
        Assert.Equal(234_000_000_000_000, WorldRaidService.RemainingHP(8540800));
        Assert.Equal(234_000_000_000_000, WorldRaidService.RemainingHP(8540900));
    }

    [Fact]
    public async Task TheLiveSeasonComesBackAsALobbyBanner()
    {
        WorldRaidService.SetManifest(Live854(), new ExcelTableService());

        var banner = Assert.Single((await BannerList()).BannerDBs!);
        Assert.Equal(EventContentType.InteractiveWorldRaid, banner.BannerType);
        Assert.Equal(854, banner.LinkedLobbyBannerId);
        Assert.Equal("Event_Banner_854.png", banner.FileName);
        Assert.Equal(BannerDisplayType.Lobby, banner.BannerDisplayType);
        Assert.Equal(DateTime.Parse("2026-08-08 11:00:00").ToLocalTime(), banner.StartDate);
        Assert.Equal(DateTime.Parse("2026-09-21 10:59:59").ToLocalTime(), banner.EndDate);
    }

    [Fact]
    public async Task WithNoRaidRunningThereIsNothingToShow()
    {
        WorldRaidService.SetManifest(null, new ExcelTableService());

        Assert.Empty((await BannerList()).BannerDBs!);
    }

    [Fact]
    public async Task ASeasonWithoutPhaseRowsGetsNoBanner()
    {
        var classic = Live854();
        classic.seasonId = 814;
        WorldRaidService.SetManifest(classic, new ExcelTableService());

        Assert.Empty((await BannerList()).BannerDBs!);
    }

    [Fact]
    public void AFreshAccountLandsInTheOpeningPhaseOnTheBaseMaps()
    {
        WorldRaidService.SetManifest(Started854(-3, 3), new ExcelTableService());

        using var db = NewContext();
        var progress = Progress(db);

        Assert.Equal(854, progress!.SeasonId);
        Assert.Equal(85400, progress.PhaseId);
        Assert.Equal(85400000, progress.Maps![WorldRaidMapType.Carrier]);
        Assert.Equal(85400100, progress.Maps[WorldRaidMapType.WorldMap]);
        Assert.Empty(progress.ClearConditionIds!);
    }

    [Fact]
    public void OnceTheSecondPhasesBossesAreDueThatIsThePhaseTheServerNames()
    {
        WorldRaidService.SetManifest(Started854(-10, -2), new ExcelTableService());

        using var db = NewContext();
        var progress = Progress(db);

        Assert.Equal(85401, progress!.PhaseId);
        Assert.Equal(85401000, progress.Maps![WorldRaidMapType.Carrier]);
        Assert.Equal(85401100, progress.Maps[WorldRaidMapType.WorldMap]);
    }

    [Fact]
    public void ClearingTheFirstBossMovesTheCarrierUpALevel()
    {
        WorldRaidService.SetManifest(Started854(-3, 3), new ExcelTableService());

        using var db = NewContext();
        db.WorldRaidLocalBosses.Add(new WorldRaidLocalBossDBServer { AccountServerId = 1, SeasonId = 854, GroupId = 8540000, UniqueId = 8540001, IsCleardEver = true });
        db.SaveChanges();

        var progress = Progress(db);

        // the two-boss count condition is still short, so only the single-boss one comes back and the world map has no level behind either
        Assert.Equal([854001000L], progress!.ClearConditionIds);
        Assert.Equal(85400001, progress.Maps![WorldRaidMapType.Carrier]);
        Assert.Equal(85400100, progress.Maps[WorldRaidMapType.WorldMap]);
    }

    [Fact]
    public void ABossClearedInAnotherSeasonDoesNotCount()
    {
        WorldRaidService.SetManifest(Started854(-3, 3), new ExcelTableService());

        using var db = NewContext();
        db.WorldRaidLocalBosses.Add(new WorldRaidLocalBossDBServer { AccountServerId = 1, SeasonId = 823, GroupId = 8540000, UniqueId = 8540001, IsCleardEver = true });
        db.SaveChanges();

        Assert.Empty(Progress(db)!.ClearConditionIds!);
    }

    [Fact]
    public void AClassicSeasonHasNoPhaseToReport()
    {
        WorldRaidService.SetManifest(Live854(), new ExcelTableService());

        using var db = NewContext();
        Assert.Null(Progress(db, 823));
    }

    // the client looks its stages up by (ContentType, id) and takes the type off the row we sent, so an 854 stage stamped 17 is a lookup that finds nothing and throws inside the loading coroutine
    [Fact]
    public async Task AnInteractiveSeasonsBossRowsCarryItsOwnContentType()
    {
        using var db = NewContext();
        var rows = await Lobby(db, 854);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal(ContentType.InteractiveWorldRaid, row.ContentType));
    }

    [Fact]
    public async Task AClassicSeasonStaysOnTheWorldRaidType()
    {
        using var db = NewContext();
        var rows = await Lobby(db, 823);

        Assert.Equal(ContentType.WorldRaid, Assert.Single(rows).ContentType);
    }

    [Fact]
    public async Task ARowWrittenBeforeTheColumnExistedGetsItFilledIn()
    {
        using var db = NewContext();
        db.WorldRaidLocalBosses.Add(new WorldRaidLocalBossDBServer
        {
            AccountServerId = 1,
            SeasonId = 854,
            GroupId = 8540000,
            UniqueId = 8540001,
            RaidBattleDB = new RaidBattleDBServer { ContentType = ContentType.WorldRaid, RaidUniqueId = 8540001 }
        });
        db.SaveChanges();

        var row = (await Lobby(db, 854)).Single(x => x.UniqueId == 8540001);

        Assert.Equal(ContentType.InteractiveWorldRaid, row.ContentType);
        Assert.Equal(ContentType.InteractiveWorldRaid, row.RaidBattleDB!.ContentType);
    }

    private static Task<List<WorldRaidLocalBossDBServer>> Lobby(SchaleDataContext db, long seasonId)
    {
        return new WorldRaidManager(new ExcelTableService(), null!, null!).WorldRaidLobby(db, db.Accounts.First(), new WorldRaidLobbyRequest { SeasonId = seasonId });
    }

    // the phase pick runs against the wall clock, so the fixture dates move with it rather than sitting at fixed strings
    private static WorldRaidManifest Started854(double openDaysFromNow, double secondSpawnDaysFromNow)
    {
        var raid = Live854();
        raid.open = DateTime.UtcNow.AddDays(openDaysFromNow).ToString("yyyy-MM-dd HH:mm:ss");
        var secondSpawn = DateTime.UtcNow.AddDays(secondSpawnDaysFromNow).ToString("yyyy-MM-dd HH:mm:ss");
        raid.bosses =
        [
            new WorldRaidManifestBoss { groupId = 8540000, spawnTime = raid.open, eliminateTime = raid.close },
            new WorldRaidManifestBoss { groupId = 8540100, spawnTime = raid.open, eliminateTime = raid.close },
            new WorldRaidManifestBoss { groupId = 8540800, spawnTime = secondSpawn, eliminateTime = raid.close },
            new WorldRaidManifestBoss { groupId = 8540900, spawnTime = secondSpawn, eliminateTime = raid.close },
        ];
        return raid;
    }

    private static WorldRaidProgressDB? Progress(SchaleDataContext db, long seasonId = 854)
    {
        return new WorldRaidManager(new ExcelTableService(), null!, null!).Progress(db, db.Accounts.First(), seasonId);
    }

    private SchaleDataContext NewContext()
    {
        var db = new SchaleDataContext(new DbContextOptionsBuilder<SchaleDataContext>()
            .UseSqlite($"Data Source={Path.Combine(_dir, $"{Guid.NewGuid():N}.sqlite3")}").Options);
        db.Database.EnsureCreated();
        db.Accounts.Add(new AccountDBServer { ServerId = 1, Nickname = "Sensei" });
        db.SaveChanges();
        return db;
    }

    private static async Task<ManagementBannerListResponse> BannerList()
    {
        var handler = new ManagementHandler(null!, new AnyAccountSessionService(), new ExcelTableService());
        return await handler.BannerList(null!, new ManagementBannerListRequest(), new ManagementBannerListResponse());
    }

    // the banner list never reads the account back, so nothing here needs a database behind it
    private class AnyAccountSessionService : ISessionKeyService
    {
        public Task<AccountDBServer> GetAuthenticatedUser(SchaleDataContext context, SessionKey? sessionKey) => Task.FromResult(new AccountDBServer());

        public Task<SessionKey?> GenerateSession(long publisherAccountId, string? customToken = null) => throw new NotSupportedException();
        public bool ValidateRequest(RequestPacket request) => true;
        public void RevokeSession(long userId) { }
        public int PurgeExpiredSessions(TimeSpan maxInactivity) => 0;
    }

    // manifest times are utc; the rows land in local wall clock, so expectations convert the same way
    private static string Local(string utc) => DateTime.Parse(utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    private static WorldRaidManifest Live854()
    {
        return new WorldRaidManifest
        {
            seasonId = 854,
            name = "test",
            open = "2026-08-10 11:00:00",
            close = "2026-09-14 10:59:59",
            exposed = "2026-08-08 11:00:00",
            extension = "2026-09-21 10:59:59",
            bosses =
            [
                new WorldRaidManifestBoss { groupId = 8540000, spawnTime = "2026-08-11 11:00:00", eliminateTime = "2026-08-24 10:59:59" },
                new WorldRaidManifestBoss { groupId = 8540100, spawnTime = "2026-08-11 11:00:00", eliminateTime = "2026-08-24 10:59:59" },
                new WorldRaidManifestBoss { groupId = 8540800, spawnTime = "2026-08-25 11:00:00", eliminateTime = "2026-09-14 10:59:59" },
                new WorldRaidManifestBoss { groupId = 8540900, spawnTime = "2026-08-25 11:00:00", eliminateTime = "2026-09-14 10:59:59" },
            ],
        };
    }

    private static void WriteDb(string path)
    {
        SqliteProvider.EnsureInitialized();

        using var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        conn.Open();

        using (var key = conn.CreateCommand())
        {
            key.CommandText = $"PRAGMA key = \"x'{Key}'\";";
            key.ExecuteNonQuery();
        }

        using (var create = conn.CreateCommand())
        {
            // Apply widens the event item expiry out of the same file, so ItemDBSchema has to be there even with nothing in it.
            create.CommandText = "CREATE TABLE EventContentSeasonDBSchema (Bytes BLOB); CREATE TABLE ItemDBSchema (Bytes BLOB); " +
                "CREATE TABLE WorldRaidSeasonManageDBSchema (Bytes BLOB); CREATE TABLE WorldRaidBossGroupDBSchema (Bytes BLOB); " +
                "CREATE TABLE InteractiveWorldRaidSeasonManageDBSchema (Bytes BLOB); CREATE TABLE InteractiveWorldRaidBossGroupDBSchema (Bytes BLOB); " +
                "CREATE TABLE InteractiveWorldRaidCarrierMapDBSchema (Bytes BLOB); CREATE TABLE InteractiveWorldRaidConditionDBSchema (Bytes BLOB); " +
                "CREATE TABLE WorldRaidStageDBSchema (Bytes BLOB); CREATE TABLE InteractiveWorldRaidStageDBSchema (Bytes BLOB)";
            create.ExecuteNonQuery();
        }

        var season = new EventContentSeasonExcelT
        {
            EventContentId = 854,
            OriginalEventContentId = 854,
            Name = "Event_Name_854",
            EventContentType = EventContentType.InteractiveWorldRaid,
            BeforehandExposedTime = "2026-05-24 11:00:00",
            EventContentOpenTime = "2026-05-26 11:00:00",
            EventContentCloseNoteTime = "",
            EventContentCloseTime = "2026-06-30 10:59:59",
            ExtensionTime = "2026-07-14 10:59:59",
            BeforehandScenarioGroupId = [],
        };
        var sfbb = new FlatBufferBuilder(512);
        sfbb.Finish(EventContentSeasonExcel.Pack(sfbb, season).Value);
        Insert(conn, "EventContentSeasonDBSchema", sfbb.SizedByteArray());

        var shop = new EventContentSeasonExcelT
        {
            EventContentId = 854,
            OriginalEventContentId = 854,
            Name = "Event_Name_854",
            EventContentType = EventContentType.Shop,
            BeforehandExposedTime = "2026-05-24 11:00:00",
            EventContentOpenTime = "2026-05-26 11:00:00",
            EventContentCloseNoteTime = "",
            EventContentCloseTime = "2026-06-30 10:59:59",
            ExtensionTime = "2026-07-14 10:59:59",
            BeforehandScenarioGroupId = [],
        };
        var shopfbb = new FlatBufferBuilder(512);
        shopfbb.Finish(EventContentSeasonExcel.Pack(shopfbb, shop).Value);
        Insert(conn, "EventContentSeasonDBSchema", shopfbb.SizedByteArray());

        var phases = new[]
        {
            Phase(85400, 0, false, CurrencyTypes.WorldRaidTicketA, [8540000, 8540100], "2026-05-26 11:00:00", "2026-06-09 10:59:59", "2026-05-27 11:00:00", "2026-06-09 10:59:59"),
            Phase(85401, 854010000, false, CurrencyTypes.WorldRaidTicketB, [8540800, 8540900], "2026-06-09 11:00:00", "2026-06-23 10:59:59", "2026-06-10 11:00:00", "2026-06-23 10:59:59"),
            Phase(85403, 854030000, true, CurrencyTypes.WorldRaidTicketC, [8540000, 8540100, 8540800, 8540900], "2026-07-14 11:00:00", "2999-12-31 23:59:59", "2026-07-14 11:00:00", "2999-12-31 23:59:59"),
        };
        foreach (var phase in phases)
        {
            var pfbb = new FlatBufferBuilder(512);
            pfbb.Finish(InteractiveWorldRaidSeasonManageExcel.Pack(pfbb, phase).Value);
            Insert(conn, "InteractiveWorldRaidSeasonManageDBSchema", pfbb.SizedByteArray());
        }

        var groups = new[]
        {
            new InteractiveWorldRaidBossGroupExcelT { WorldRaidBossGroupId = 8540000, WorldBossHP = 8_250_000_000_000, WorldBossHPAsia = 6_880_000_000_000, WorldBossHPGlobal = 1_270_000_000_000 },
            new InteractiveWorldRaidBossGroupExcelT { WorldRaidBossGroupId = 8540100, WorldBossHP = 7_500_000_000_000, WorldBossHPAsia = 6_300_000_000_000, WorldBossHPGlobal = 1_160_000_000_000 },
            new InteractiveWorldRaidBossGroupExcelT { WorldRaidBossGroupId = 8540800, WorldBossHPLinkGroup = 8540800, WorldBossHP = 270_000_000_000_000, WorldBossHPAsia = 234_000_000_000_000, WorldBossHPGlobal = 43_200_000_000_000 },
            new InteractiveWorldRaidBossGroupExcelT { WorldRaidBossGroupId = 8540900, WorldBossHPLinkGroup = 8540800, WorldBossHP = 270_000_000_000_000, WorldBossHPAsia = 234_000_000_000_000, WorldBossHPGlobal = 43_200_000_000_000 },
        };
        foreach (var group in groups)
        {
            var gfbb = new FlatBufferBuilder(256);
            gfbb.Finish(InteractiveWorldRaidBossGroupExcel.Pack(gfbb, group).Value);
            Insert(conn, "InteractiveWorldRaidBossGroupDBSchema", gfbb.SizedByteArray());
        }

        var stages = new[]
        {
            new InteractiveWorldRaidStageExcelT { Id = 8540001, WorldRaidBossGroupId = 8540000, BossCharacterId = [] },
            new InteractiveWorldRaidStageExcelT { Id = 8540002, WorldRaidBossGroupId = 8540000, BossCharacterId = [] },
            new InteractiveWorldRaidStageExcelT { Id = 8540101, WorldRaidBossGroupId = 8540100, BossCharacterId = [] },
        };
        foreach (var stage in stages)
        {
            var stfbb = new FlatBufferBuilder(256);
            stfbb.Finish(InteractiveWorldRaidStageExcel.Pack(stfbb, stage).Value);
            Insert(conn, "InteractiveWorldRaidStageDBSchema", stfbb.SizedByteArray());
        }

        // one classic season alongside, so the content type the lobby stamps can be told apart from the type it defaults to
        var classic = new WorldRaidSeasonManageExcelT { SeasonId = 823, OpenRaidBossGroupId = [823000] };
        var cfb = new FlatBufferBuilder(256);
        cfb.Finish(WorldRaidSeasonManageExcel.Pack(cfb, classic).Value);
        Insert(conn, "WorldRaidSeasonManageDBSchema", cfb.SizedByteArray());

        var classicStage = new WorldRaidStageExcelT { Id = 823001, WorldRaidBossGroupId = 823000, BossCharacterId = [] };
        var csfbb = new FlatBufferBuilder(256);
        csfbb.Finish(WorldRaidStageExcel.Pack(csfbb, classicStage).Value);
        Insert(conn, "WorldRaidStageDBSchema", csfbb.SizedByteArray());

        var conditions = new[]
        {
            new InteractiveWorldRaidConditionExcelT { Id = 854001000, WorldRaidSeasonId = 854, WorldRaidPhaseId = 85400, MultipleConditionCheckType = MultipleConditionCheckType.And, ConditionType = [WorldRaidConditionType.BossClear], ConditionValue = [8540000] },
            new InteractiveWorldRaidConditionExcelT { Id = 854002000, WorldRaidSeasonId = 854, WorldRaidPhaseId = 85400, MultipleConditionCheckType = MultipleConditionCheckType.Count, MultipleConditionCheckParameter = 2, ConditionType = [WorldRaidConditionType.BossClear, WorldRaidConditionType.BossClear], ConditionValue = [8540000, 8540100] },
            new InteractiveWorldRaidConditionExcelT { Id = 854010000, WorldRaidSeasonId = 854, WorldRaidPhaseId = 85401, MultipleConditionCheckType = MultipleConditionCheckType.And, ConditionType = [WorldRaidConditionType.EventStageClear], ConditionValue = [8541302] },
        };
        foreach (var condition in conditions)
        {
            var cfbb = new FlatBufferBuilder(256);
            cfbb.Finish(InteractiveWorldRaidConditionExcel.Pack(cfbb, condition).Value);
            Insert(conn, "InteractiveWorldRaidConditionDBSchema", cfbb.SizedByteArray());
        }

        var levels = new[]
        {
            new InteractiveWorldRaidCarrierMapExcelT { Id = 85400000, WorldRaidSeasonId = 854, WorldRaidPhaseId = 85400, ChangeTarget = WorldRaidMapType.Carrier, Priority = 1 },
            new InteractiveWorldRaidCarrierMapExcelT { Id = 85400001, ConditionId = 854001000, WorldRaidSeasonId = 854, WorldRaidPhaseId = 85400, ChangeTarget = WorldRaidMapType.Carrier, Priority = 2 },
            new InteractiveWorldRaidCarrierMapExcelT { Id = 85400002, ConditionId = 854002000, WorldRaidSeasonId = 854, WorldRaidPhaseId = 85400, ChangeTarget = WorldRaidMapType.Carrier, Priority = 3 },
            new InteractiveWorldRaidCarrierMapExcelT { Id = 85400100, WorldRaidSeasonId = 854, WorldRaidPhaseId = 85400, ChangeTarget = WorldRaidMapType.WorldMap, Priority = 1 },
            new InteractiveWorldRaidCarrierMapExcelT { Id = 85401000, WorldRaidSeasonId = 854, WorldRaidPhaseId = 85401, ChangeTarget = WorldRaidMapType.Carrier, Priority = 1 },
            new InteractiveWorldRaidCarrierMapExcelT { Id = 85401100, WorldRaidSeasonId = 854, WorldRaidPhaseId = 85401, ChangeTarget = WorldRaidMapType.WorldMap, Priority = 1 },
        };
        foreach (var level in levels)
        {
            var lfbb = new FlatBufferBuilder(256);
            lfbb.Finish(InteractiveWorldRaidCarrierMapExcel.Pack(lfbb, level).Value);
            Insert(conn, "InteractiveWorldRaidCarrierMapDBSchema", lfbb.SizedByteArray());
        }

        SqliteConnection.ClearAllPools();
    }

    private static InteractiveWorldRaidSeasonManageExcelT Phase(long phaseId, long condition, bool replay, CurrencyTypes ticket, List<long> groups, string start, string end, string spawn, string eliminate)
    {
        return new InteractiveWorldRaidSeasonManageExcelT
        {
            SeasonId = 854,
            PhaseId = phaseId,
            PhaseStartCondition = condition,
            IsReplaySeason = replay,
            EnterTicket = ticket,
            PhaseStartTime = start,
            PhaseEndTime = end,
            OpenRaidBossGroupId = groups,
            BossSpawnTime = groups.Select(_ => spawn).ToList(),
            EliminateTime = groups.Select(_ => eliminate).ToList(),
        };
    }

    private static void Insert(SqliteConnection conn, string table, byte[] bytes)
    {
        using var insert = conn.CreateCommand();
        insert.CommandText = $"INSERT INTO {table} (Bytes) VALUES (@b)";
        insert.Parameters.Add("@b", SqliteType.Blob).Value = bytes;
        insert.ExecuteNonQuery();
    }

    private List<EventContentSeasonExcelT> ReadSeasons()
    {
        SqliteConnection.ClearAllPools();

        using var conn = ExcelTableService.OpenExcelDbConnection(_clientDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Bytes FROM EventContentSeasonDBSchema ORDER BY rowid";

        var rows = new List<EventContentSeasonExcelT>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(EventContentSeasonExcel.GetRootAsEventContentSeasonExcel(new ByteBuffer((byte[])reader[0])).UnPack());
        return rows;
    }

    private List<InteractiveWorldRaidSeasonManageExcelT> ReadPhases()
    {
        SqliteConnection.ClearAllPools();

        using var conn = ExcelTableService.OpenExcelDbConnection(_clientDb);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Bytes FROM InteractiveWorldRaidSeasonManageDBSchema ORDER BY rowid";

        var rows = new List<InteractiveWorldRaidSeasonManageExcelT>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            rows.Add(InteractiveWorldRaidSeasonManageExcel.GetRootAsInteractiveWorldRaidSeasonManageExcel(new ByteBuffer((byte[])reader[0])).UnPack());
        return rows;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY", null);
        Environment.SetEnvironmentVariable("SHITTIM_CLIENT_EXCELDB_PATH", null);
        ExcelTableService.DumpedDir = _originalDumpedDir;

        foreach (var (path, content) in _savedFiles)
        {
            if (content != null)
                File.WriteAllText(path, content);
            else
                File.Delete(path);
        }
        WorldRaidService.Load();

        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }
}
