using System.Collections.Concurrent;

namespace Shittim_Server.Services;

/// <summary>
/// Per-account "mail arrived mid-session" state behind ServerNotificationFlag.NewMailArrived (4).
///
/// The official model, reconstructed from the two reference captures:
/// <list type="bullet">
/// <item>Only the delivering response and the next Mail_Check carry the bit. In the first capture
/// both Attendance_Reward responses report 12 (8|4) after mailing rewards, yet the
/// Notification_EventContentReddotCheck between them reports plain 8 — so unlike HasUnreadMail
/// this is not a baseline the gateway stamps on every response.</item>
/// <item>Mail_Check consumes it: the first Mail_Check after those deliveries reports 12, the next
/// reports 8 while the mail is still sitting unread in the box.</item>
/// <item>Not every mail source leaves it pending. The non-truncated capture's clan attendance
/// delivery carries 4 on the Clan_Lobby response itself, but the Mail_Check after it reports
/// plain 8 — so ClanHandler sets the bit on its own response and never calls
/// <see cref="MarkNewMail"/>; only attendance-reward mail leaves the bit for Mail_Check.</item>
/// <item>Mail that predates the session does not raise it either: both captures log in over a
/// mailbox with unread mail and every login-chain response carries plain 8. That is why this
/// state is deliberately in-memory — after a restart, pending mail becomes exactly that
/// pre-existing case — and the project has no migration path for a persisted column anyway.</item>
/// </list>
///
/// A set rather than a counter: the first capture delivers mail twice before the client checks,
/// and the single Mail_Check still consumes everything (12 once, then 8).
/// </summary>
public static class MailNotificationService
{
    private static readonly ConcurrentDictionary<long, byte> _pending = new();

    public static void MarkNewMail(long accountServerId) => _pending[accountServerId] = 1;

    /// <summary>Reports whether new mail is pending and clears it — Mail_Check semantics.</summary>
    public static bool Consume(long accountServerId) => _pending.TryRemove(accountServerId, out _);
}
