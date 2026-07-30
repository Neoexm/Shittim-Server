using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MappingProfiles;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Xunit;

namespace Shittim_Server.Tests;

public class MappingProfileTests
{
    // Official CurrencyDict, in order (live captures, client 1.90).
    private static readonly CurrencyTypes[] ExpectedCurrencyKeys =
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

    // The four currencies that do not regenerate carry no timestamp.
    private static readonly CurrencyTypes[] NonRegenerating =
    {
        CurrencyTypes.Gem, CurrencyTypes.GemPaid, CurrencyTypes.GemBonus, CurrencyTypes.Gold,
    };

    private static readonly Language[] ExpectedMailLanguages =
    {
        Language.Kr, Language.En, Language.Th, Language.Tw,
    };

    // currencies

    [Fact]
    public void CurrencyDict_CarriesOfficialKeysInOrder()
    {
        // the entity's constructor seeds nearly every CurrencyTypes value, so this also covers the
        // profile narrowing rather than passing the internal set straight through.
        var mapped = Mapper.Map<AccountCurrencyDB>(new AccountCurrencyDBServer(accountId: 1));

        Assert.NotNull(mapped.CurrencyDict);
        Assert.Equal(ExpectedCurrencyKeys, mapped.CurrencyDict!.Keys);
    }

    [Theory]
    // Values the server tracks internally but official never sends. Max is a sentinel; the two
    // WeekDungeon tickets are deprecated content.
    [InlineData(CurrencyTypes.Invalid)]
    [InlineData(CurrencyTypes.Max)]
    [InlineData(CurrencyTypes.WeekDungeonFindGiftTicket)]
    [InlineData(CurrencyTypes.WeekDungeonBloodTicket)]
    public void CurrencyDict_DropsKeysOfficialNeverSends(CurrencyTypes shouldBeAbsent)
    {
        var source = new AccountCurrencyDBServer(accountId: 1);
        source.CurrencyDict[shouldBeAbsent] = 99;

        var mapped = Mapper.Map<AccountCurrencyDB>(source);

        Assert.DoesNotContain(shouldBeAbsent, mapped.CurrencyDict!.Keys);
    }

    [Fact]
    public void CurrencyDict_MissingKeys_FilledWithZero()
    {
        // A key the client expects but the account has never held must still be present as 0 - the
        // client reads these unconditionally, so an omitted key is not the same as a zero one.
        var source = new AccountCurrencyDBServer(accountId: 1);
        source.CurrencyDict.Clear();
        source.CurrencyDict[CurrencyTypes.Gem] = 1200;

        var mapped = Mapper.Map<AccountCurrencyDB>(source);

        Assert.Equal(ExpectedCurrencyKeys, mapped.CurrencyDict!.Keys);
        Assert.Equal(1200, mapped.CurrencyDict[CurrencyTypes.Gem]);
        Assert.Equal(0, mapped.CurrencyDict[CurrencyTypes.ActionPoint]);
    }

    [Fact]
    public void UpdateTimeDict_OmitsNonRegenerating()
    {
        var mapped = Mapper.Map<AccountCurrencyDB>(new AccountCurrencyDBServer(accountId: 1));

        Assert.NotNull(mapped.UpdateTimeDict);
        foreach (var currency in NonRegenerating)
            Assert.DoesNotContain(currency, mapped.UpdateTimeDict!.Keys);

        // Everything else keeps its timestamp, and the order still follows the currency list.
        Assert.Equal(
            ExpectedCurrencyKeys.Where(c => !NonRegenerating.Contains(c)),
            mapped.UpdateTimeDict!.Keys);
    }

    [Fact]
    public void UpdateTimeDict_PreservesStoredTimestamps()
    {
        var stamp = new DateTime(2026, 7, 26, 4, 0, 0, DateTimeKind.Utc);
        var source = new AccountCurrencyDBServer(accountId: 1);
        source.UpdateTimeDict[CurrencyTypes.ActionPoint] = stamp;

        var mapped = Mapper.Map<AccountCurrencyDB>(source);

        Assert.Equal(stamp, mapped.UpdateTimeDict![CurrencyTypes.ActionPoint]);
    }

    // mail

    [Fact]
    public void LocalizedMailText_FourLanguagesInOrder()
    {
        // MailDBServer seeds every Language value; official GL sends only these four, and Jp is not
        // among them.
        var mapped = Mapper.Map<MailDB>(new MailDBServer());

        Assert.Equal(ExpectedMailLanguages, mapped.LocalizedSender!.Keys);
        Assert.Equal(ExpectedMailLanguages, mapped.LocalizedComment!.Keys);
    }

    [Fact]
    public void LocalizedMailText_MissingLanguage_IsEmptyString()
    {
        var source = new MailDBServer();
        source.LocalizedSender.Clear();
        source.LocalizedSender[Language.En] = "Arona";

        var mapped = Mapper.Map<MailDB>(source);

        Assert.Equal(ExpectedMailLanguages, mapped.LocalizedSender!.Keys);
        Assert.Equal("Arona", mapped.LocalizedSender[Language.En]);
        Assert.Equal(string.Empty, mapped.LocalizedSender[Language.Kr]);
    }

    [Fact]
    public void LocalizedMailText_Null_FabricatesNoKeys()
    {
        // Absent is distinct from "present but blank" on the wire: a mail with no localized text
        // must not go out as four empty strings, which would read to the client as real content
        // that happens to be blank.
        //
        // Note this comes back as an empty dictionary rather than null. The profile's MapFrom
        // returns null, but AutoMapper runs with AllowNullCollections = false by default and
        // materialises an empty collection instead - the same behaviour the profile already
        // documents for AcademyDB.ZoneScheduleGroupRecords. What matters here is that no key/value
        // pair is invented.
        var source = new MailDBServer { LocalizedSender = null!, LocalizedComment = null! };

        var mapped = Mapper.Map<MailDB>(source);

        Assert.NotNull(mapped.LocalizedSender);
        Assert.NotNull(mapped.LocalizedComment);
        Assert.Empty(mapped.LocalizedSender);
        Assert.Empty(mapped.LocalizedComment);
    }

    [Fact]
    public void RemainParcelInfos_Empty_SerializesNothing()
    {
        // Official only sends RemainParcelInfos for partially-claimed mail. The profile maps an
        // empty list to null for that reason; AutoMapper turns that back into an empty list (see
        // above), and MailDB.RemainParcelInfos is [OmitWhenEmpty] so the gateway drops it on the
        // way out. Either representation is fine - a populated one would not be.
        var mapped = Mapper.Map<MailDB>(new MailDBServer { RemainParcelInfos = new List<ParcelInfo>() });

        Assert.NotNull(mapped.RemainParcelInfos);
        Assert.Empty(mapped.RemainParcelInfos);
    }

    [Fact]
    public void RemainParcelInfos_Populated_Preserved()
    {
        var source = new MailDBServer
        {
            RemainParcelInfos = ParcelInfo.CreateParcelInfo(ParcelType.Item, id: 1, amount: 5)
        };

        var mapped = Mapper.Map<MailDB>(source);

        Assert.NotNull(mapped.RemainParcelInfos);
        Assert.Single(mapped.RemainParcelInfos!);
    }

    // account

    [Fact]
    public void RetentionDays_AlwaysZero()
    {
        // Official emits RetentionDays: 0 even for a fresh account, and the client distinguishes 0
        // from null.
        var mapped = Mapper.Map<AccountDB>(new AccountDBServer { ServerId = 1, Nickname = "Sensei" });

        Assert.Equal(0, mapped.RetentionDays);
    }

    [Fact]
    public void ItemCanConsume_NotMappedFromEntity()
    {
        // CanConsume is decided per-request by the parcel layer, not stored, so the profile ignores
        // it. ItemDBServer.CanConsume is hardcoded to true, so without the Ignore() every mapped
        // ItemDB would go out claiming to be consumable.
        var mapped = Mapper.Map<ItemDB>(new ItemDBServer());

        Assert.True(new ItemDBServer().CanConsume);
        Assert.False(mapped.CanConsume);
    }

    // Built exactly the way the server builds it (GameServer.AddAutoMapper), so that a change in how
    // AutoMapper is registered shows up here rather than only at runtime.
    private static readonly IMapper Mapper = BuildMapper();

    private static IMapper BuildMapper()
    {
        var services = new ServiceCollection();

        // AutoMapper resolves an ILoggerFactory out of the container from 14.0 onwards. The server
        // gets one for free from WebApplicationBuilder; a bare ServiceCollection does not, and
        // without it AddAutoMapper's factory throws while building IMapper.
        services.AddLogging();
        services.AddAutoMapper(cfg => { }, typeof(GameModelsMappingProfile).Assembly);

        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
