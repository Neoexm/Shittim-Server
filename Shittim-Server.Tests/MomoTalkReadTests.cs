using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core.NetworkProtocol.Handlers;
using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

// The client rebuilds the transcript and walks new arrivals from LatestMessageGroupId entirely on its own (MomoTalkDBService), so the outline must mirror exactly the group the client says it displayed. A server-computed successor can park Latest on an unanswered Answer group, and from there the client resolves the branch off the first row and never shows the prompt.
public class MomoTalkReadTests
{
    [Fact]
    public async Task AFlushReadStopsAtTheGroupTheClientReported()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var messengers = Excels.GetTable<AcademyMessangerExcelT>();
        var beforeAnswer = messengers.First(x =>
            x.MessageCondition != AcademyMessageConditions.Answer && x.NextGroupId > 0 &&
            messengers.Any(a => a.MessageGroupId == x.NextGroupId && a.MessageCondition == AcademyMessageConditions.Answer));
        var character = GiveCharacter(db, account, beforeAnswer.CharacterId);

        var response = await Handler().Read(db,
            new MomoTalkReadRequest { SessionKey = Key(account), CharacterDBId = character.ServerId, LastReadMessageGroupId = beforeAnswer.MessageGroupId },
            new MomoTalkReadResponse());

        Assert.Equal(beforeAnswer.MessageGroupId, response.MomoTalkOutLineDB.LatestMessageGroupId);
        Assert.Equal(beforeAnswer.MessageGroupId, db.MomoTalkOutLines.Single().LatestMessageGroupId);
        Assert.Empty(db.MomoTalkChoices);
    }

    [Fact]
    public async Task AnsweringStoresTheChosenRowKeyedByTheAnswerGroup()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var answer = Excels.GetTable<AcademyMessangerExcelT>().First(x => x.MessageCondition == AcademyMessageConditions.Answer);
        var character = GiveCharacter(db, account, answer.CharacterId);

        var response = await Handler().Read(db,
            new MomoTalkReadRequest { SessionKey = Key(account), CharacterDBId = character.ServerId, LastReadMessageGroupId = answer.MessageGroupId, ChosenMessageId = answer.Id },
            new MomoTalkReadResponse());

        var choice = db.MomoTalkChoices.Single();
        Assert.Equal(answer.MessageGroupId, choice.MessageGroupId);
        Assert.Equal(answer.Id, choice.ChosenMessageId);
        Assert.Equal(answer.MessageGroupId, response.MomoTalkOutLineDB.LatestMessageGroupId);
    }

    [Fact]
    public async Task AttendingARelationshipEventNeedsNoScheduleTicket()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var schedule = Excels.GetTable<AcademyFavorScheduleExcelT>().First(x => x.FavorRank > 1);
        var character = GiveCharacter(db, account, schedule.CharacterId, (int)schedule.FavorRank);
        GiveOutline(db, account, character);

        var currency = db.Currencies.Single();
        currency.CurrencyDict[CurrencyTypes.AcademyTicket] = 0;
        db.SaveChanges();

        await Handler().FavorSchedule(db,
            new MomoTalkFavorScheduleRequest { SessionKey = Key(account), ScheduleId = schedule.Id },
            new MomoTalkFavorScheduleResponse());

        Assert.Contains(schedule.Id, db.MomoTalkOutLines.Single().ScheduleIds);
        Assert.Equal(0, db.Currencies.Single().CurrencyDict[CurrencyTypes.AcademyTicket]);
    }

    [Fact]
    public async Task ARelationshipEventAboveTheCharactersFavorRankIsRefused()
    {
        using var db = NewContext();
        var account = NewAccount(db);

        var schedule = Excels.GetTable<AcademyFavorScheduleExcelT>().First(x => x.FavorRank > 1);
        var character = GiveCharacter(db, account, schedule.CharacterId, (int)schedule.FavorRank - 1);
        GiveOutline(db, account, character);

        await Handler().FavorSchedule(db,
            new MomoTalkFavorScheduleRequest { SessionKey = Key(account), ScheduleId = schedule.Id },
            new MomoTalkFavorScheduleResponse());

        Assert.DoesNotContain(schedule.Id, db.MomoTalkOutLines.Single().ScheduleIds);
    }

    private static CharacterDBServer GiveCharacter(SchaleDataContext db, AccountDBServer account, long uniqueId, int favorRank = 1)
    {
        var character = new CharacterDBServer(account.ServerId, uniqueId) { FavorRank = favorRank };
        db.Characters.Add(character);
        db.SaveChanges();
        return character;
    }

    private static void GiveOutline(SchaleDataContext db, AccountDBServer account, CharacterDBServer character)
    {
        db.MomoTalkOutLines.Add(new MomoTalkOutLineDBServer
        {
            AccountServerId = account.ServerId,
            CharacterDBId = character.ServerId,
            CharacterId = character.UniqueId,
            LastUpdateDate = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static readonly ExcelTableService Excels = LoadExcels();

    private static ExcelTableService LoadExcels()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Shittim-Server")))
            dir = Path.GetDirectoryName(dir);

        ExcelTableService.DumpedDir = new[]
        {
            Path.Combine(dir!, "Shittim-Server", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Debug", "net10.0", "Resources", "Dumped"),
            Path.Combine(dir!, "Shittim-Server", "bin", "Release", "net10.0", "Resources", "Dumped"),
        }.First(Directory.Exists);

        return new ExcelTableService();
    }

    private static MomoTalkHandler Handler() => new(null!, new FixedSessionService(), Excels, Mapper, new ParcelHandler(Excels, Mapper));

    private static SessionKey Key(AccountDBServer account) => new() { AccountServerId = account.ServerId, MxToken = "t" };

    private class FixedSessionService : ISessionKeyService
    {
        public Task<AccountDBServer> GetAuthenticatedUser(SchaleDataContext context, SessionKey? sessionKey) =>
            Task.FromResult(context.Accounts.Single(x => x.ServerId == sessionKey!.AccountServerId));

        public Task<SessionKey?> GenerateSession(long publisherAccountId, string? customToken = null) => throw new NotSupportedException();
        public bool ValidateRequest(RequestPacket request) => true;
        public void RevokeSession(long userId) { }
        public int PurgeExpiredSessions(TimeSpan maxInactivity) => 0;
    }

    private static SchaleDataContext NewContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shittim-momotalktest-{Guid.NewGuid():N}.sqlite3");
        var context = new SchaleDataContext(
            new DbContextOptionsBuilder<SchaleDataContext>().UseSqlite($"Data Source={path}").Options);

        context.Database.EnsureCreated();
        return context;
    }

    private static AccountDBServer NewAccount(SchaleDataContext db)
    {
        var account = new AccountDBServer { ServerId = 1, Nickname = "Sensei1" };
        db.Accounts.Add(account);
        db.Currencies.Add(new AccountCurrencyDBServer(1));
        db.SaveChanges();
        return account;
    }

    private static readonly IMapper Mapper = BuildMapper();

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => { }, typeof(GameModelsMappingProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
