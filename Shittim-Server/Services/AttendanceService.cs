using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.Core.Math;
using Schale.MX.Data;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Services;

/// <summary>
/// Attendance books ("stamp cards"), built to the wire model reconstructed from the captures.
/// Account_Auth carries AttendanceBookRewards as the full definitions of every open book with a
/// claimable day left today, and [] once everything is claimed - the pre-claim login at 04:12 shows
/// both books, later logins the same game day show none - while AttendanceHistoryDBs is always the
/// account's full history. Attendance_Reward repeats just the claimed book's definition plus its
/// updated history row and carries no ParcelResultDB, because the reward arrives as mail (Type 1,
/// UniqueId = book id, sender UI_MAILBOX_POST_SENDER_ARONA, comment
/// UI_MAILBOX_ATTENDANCE_REWARD_MESSAGE_NORMAL, expiry send+7d); hence ServerNotification 12 on the
/// response and bit 4 consumed by the next Mail_Check. History rows on the wire carry only ServerId,
/// AttendanceBookUniqueId and AttendedDay.
/// </summary>
public class AttendanceService
{
    // From the recovered post-claim Mail_List of the truncating official capture.
    private const string MailSender = "UI_MAILBOX_POST_SENDER_ARONA";
    private const string MailComment = "UI_MAILBOX_ATTENDANCE_REWARD_MESSAGE_NORMAL";

    private readonly ExcelTableService _excelService;

    public AttendanceService(ExcelTableService excelService)
    {
        _excelService = excelService;
    }

    /// <summary>
    /// Books whose window is open, whose level gate the account passes, and whose AccountType
    /// matches the account's state. Official only advertises matching books (a Normal account
    /// never sees the Comeback/Newbie variants); over-advertising desynchronises the client's
    /// claim loop into resubmitting already-claimed books (error 9000 on the wire).
    /// </summary>
    private List<AttendanceExcelT> OpenBooks(AccountDBServer account, DateTime now)
    {
        return _excelService.GetTable<AttendanceExcelT>()
            .Where(x => TryParse(x.StartDate) is { } start && start <= now
                && TryParse(x.EndDate) is { } end && now < end
                && account.Level >= x.AccountLevelLimit
                && (x.AccountType == AccountState.WaitingSignIn || x.AccountType == account.State))
            .ToList();
    }

    /// <summary>The books Account_Auth should carry: open books with an unclaimed day today.</summary>
    public List<AttendanceBookReward> BuildClaimableBooks(SchaleDataContext db, AccountDBServer account)
    {
        var now = account.GameSettings.ServerDateTime();
        var histories = db.GetAccountAttendanceHistories(account.ServerId).ToList();

        return OpenBooks(account, now)
            .Where(x =>
            {
                var history = histories.FirstOrDefault(h => h.AttendanceBookUniqueId == x.Id);
                return !ClaimedToday(history, now) && NextDay(x, history) > 0;
            })
            .Select(BuildBook)
            .ToList();
    }

    public List<AttendanceHistoryDB> BuildHistories(SchaleDataContext db, AccountDBServer account)
    {
        return db.GetAccountAttendanceHistories(account.ServerId)
            .ToList()
            .Select(ToWire)
            .ToList();
    }

    /// <summary>
    /// Claims a day: records the history, mails the reward, and returns the claimed book's
    /// definition plus the updated history row - exactly what the official response carries.
    /// </summary>
    public async Task<(AttendanceBookReward Book, AttendanceHistoryDB History)> Claim(
        SchaleDataContext db, AccountDBServer account, long bookUniqueId)
    {
        var now = account.GameSettings.ServerDateTime();
        var excel = OpenBooks(account, now).FirstOrDefault(x => x.Id == bookUniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.AttendanceInvalid, $"Attendance book {bookUniqueId} is not open");

        var history = db.GetAccountAttendanceHistories(account.ServerId)
            .FirstOrDefault(x => x.AttendanceBookUniqueId == bookUniqueId);

        // Idempotent: a client that re-submits an already-claimed (or completed) book gets the
        // current state back instead of an error popup - the claim loop can desync after menu
        // backouts and re-logins, and error 9000 there is purely user-hostile.
        if (ClaimedToday(history, now) || NextDay(excel, history) <= 0)
            return (BuildBook(excel), ToWire(history!));

        var day = NextDay(excel, history);

        if (history == null)
        {
            history = new AttendanceHistoryDBServer
            {
                AccountServerId = account.ServerId,
                AttendanceBookUniqueId = bookUniqueId,
                AttendedDay = new Dictionary<long, DateTime>(),
            };
            db.AttendanceHistories.Add(history);
        }

        // A completed cyclical book starts a fresh pass. The official veteran's basic book
        // (BookSize 10) shows only the current pass's days, so the dict resets rather than grows.
        if (day == 1 && history.AttendedDay is { Count: > 0 })
            history.AttendedDay = new Dictionary<long, DateTime>();

        history.AttendedDay![day] = now;
        history.LastAttendedDay = day;
        history.LastAttendedDate = now;

        var rewards = DayRewards(excel).GetValueOrDefault(day) ?? [];

        // The reward is delivered as mail, never as an in-response parcel. Official's mail:
        // Type = the book's MailType (1, Attendance), UniqueId = the book id, localisation keys
        // as sender/comment, expiry exactly 7 days after send.
        db.Mails.Add(new MailDBServer
        {
            AccountServerId = account.ServerId,
            Type = excel.MailType,
            UniqueId = bookUniqueId,
            Sender = MailSender,
            Comment = MailComment,
            LocalizedSender = Enum.GetValues<Language>().ToDictionary(x => x, _ => MailSender),
            LocalizedComment = Enum.GetValues<Language>().ToDictionary(x => x, _ => MailComment),
            SendDate = now,
            ExpireDate = now.AddDays(7),
            ParcelInfos = rewards,
            RemainParcelInfos = null
        });

        await db.SaveChangesAsync();

        // Attendance mail (unlike clan mail) leaves the NewMailArrived bit pending for the next
        // Mail_Check - off1's first Mail_Check after the claims reports 12, the second 8.
        MailNotificationService.MarkNewMail(account.ServerId);

        return (BuildBook(excel), ToWire(history));
    }

    // The wire history carries only these three members - LastAttendedDay/LastAttendedDate stay
    // server-side (official never emits them).
    private static AttendanceHistoryDB ToWire(AttendanceHistoryDBServer history) => new()
    {
        ServerId = history.ServerId,
        AttendanceBookUniqueId = history.AttendanceBookUniqueId,
        AttendedDay = history.AttendedDay,
    };

    /// <summary>Next unclaimed day, cycling completed books back to day 1; 0 when nothing is claimable.</summary>
    private static long NextDay(AttendanceExcelT excel, AttendanceHistoryDBServer? history)
    {
        var attended = history?.AttendedDay?.Count ?? 0;
        if (attended < excel.BookSize)
            return attended + 1;
        // Book full: the basic book cycles forever (the official veteran's BookSize-10 history
        // holds only the current pass's days); event books are one pass.
        return excel.Type == AttendanceType.Basic ? 1 : 0;
    }

    private static bool ClaimedToday(AttendanceHistoryDBServer? history, DateTime now)
    {
        if (history?.AttendedDay is not { Count: > 0 } days)
            return false;
        var reset = DailyResetTime(now);
        return days.Values.Any(x => x >= reset);
    }

    // The game day rolls over at 04:00, like everything else on this server.
    private static DateTime DailyResetTime(DateTime now)
    {
        var todaysReset = now.Date.AddHours(4);
        return now < todaysReset ? todaysReset.AddDays(-1) : todaysReset;
    }

    private AttendanceBookReward BuildBook(AttendanceExcelT excel)
    {
        var rewards = DayRewards(excel);
        var icons = _excelService.GetTable<AttendanceRewardExcelT>()
            .Where(x => x.AttendanceId == excel.Id)
            .ToDictionary(x => x.Day, x => x.RewardIcon ?? "");

        return new AttendanceBookReward
        {
            UniqueId = excel.Id,
            Type = excel.Type,
            // AccountType deliberately left at default: official never emits it on the wire.
            DisplayOrder = excel.DisplayOrder,
            AccountLevelLimit = excel.AccountLevelLimit,
            // Official emits '' rather than omitting the title on books with no localisation key.
            Title = excel.Title ?? "",
            TitleImagePath = excel.TitleImagePath ?? "",
            CountRule = excel.CountRule,
            CountReset = excel.CountReset,
            // Not present in the generated AttendanceExcel model; official sends 1 on the basic
            // book and 3 on the Event20Days book, so it is inferred from the book type.
            TargetGroup = excel.Type == AttendanceType.Basic ? 1 : 3,
            BookSize = excel.BookSize,
            StartDate = TryParse(excel.StartDate) ?? DateTime.MinValue,
            StartableEndDate = TryParse(excel.StartableEndDate) ?? DateTime.MaxValue,
            EndDate = TryParse(excel.EndDate) ?? DateTime.MaxValue,
            ExpiryDate = excel.ExpiryDate,
            MailType = excel.MailType,
            DailyRewardIcons = icons,
            DailyRewards = rewards,
        };
    }

    private Dictionary<long, List<ParcelInfo>> DayRewards(AttendanceExcelT excel)
    {
        return _excelService.GetTable<AttendanceRewardExcelT>()
            .Where(x => x.AttendanceId == excel.Id)
            .ToDictionary(
                x => x.Day,
                x => Enumerable.Range(0, x.RewardParcelType?.Count ?? 0)
                    .Select(i => new ParcelInfo
                    {
                        Key = new ParcelKeyPair { Type = x.RewardParcelType![i], Id = x.RewardId![i] },
                        Amount = x.RewardAmount![i],
                        // Official carries both at 1x on every attendance parcel.
                        Multiplier = BasisPoint.One,
                        Probability = BasisPoint.One
                    })
                    .ToList());
    }

    private static DateTime? TryParse(string? value) =>
        DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null;
}
