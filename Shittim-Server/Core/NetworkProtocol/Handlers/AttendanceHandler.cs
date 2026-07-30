using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.Data;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class AttendanceHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly AttendanceService _attendanceService;

    public AttendanceHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        AttendanceService attendanceService) : base(registry)
    {
        _sessionService = sessionService;
        _attendanceService = attendanceService;
    }

    // Official's claim (both captured exchanges): the request names the book and day in DayByBookUniqueId {"<bookId>": day};
    // the response repeats just the claimed book's definition + that book's updated history, carries NO ParcelResultDB (the reward goes out as mail),
    // and reads ServerNotification 12 - the mailbox baseline plus NewMailArrived from the delivery itself.
    [ProtocolHandler(Protocol.Attendance_Reward)]
    public async Task<AttendanceRewardResponse> Reward(
        SchaleDataContext db,
        AttendanceRewardRequest request,
        AttendanceRewardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var books = new List<AttendanceBookReward>();
        var histories = new List<AttendanceHistoryDB>();

        // Officially one book per request; accept several defensively.
        foreach (var bookUniqueId in (request.DayByBookUniqueId?.Keys.ToList() ?? []))
        {
            var (book, history) = await _attendanceService.Claim(db, account, bookUniqueId);
            books.Add(book);
            histories.Add(history);
        }

        if (books.Count == 0)
            throw new WebAPIException(WebAPIErrorCode.AttendanceInvalid, "No attendance book named in the request");

        response.AttendanceBookRewards = books;
        response.AttendanceHistoryDBs = histories;
        response.ServerNotification |= ServerNotificationFlag.NewMailArrived;

        return response;
    }
}
