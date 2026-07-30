using System.Collections.Concurrent;

namespace Shittim_Server.Services;

/// <summary>
/// Per-account "mail arrived mid-session" state behind ServerNotificationFlag.NewMailArrived (4).
/// Unlike HasUnreadMail the gateway does not stamp this on every response: only the delivering
/// response and the next Mail_Check carry it, and Mail_Check clears it even though the mail stays
/// unread. Clan attendance sets the bit on its own response instead and never calls MarkNewMail.
/// Mail predating the session never raises it, so keeping this in memory is fine - after a
/// restart pending mail is exactly that pre-existing case.
/// </summary>
public static class MailNotificationService
{
    private static readonly ConcurrentDictionary<long, byte> _pending = new();

    public static void MarkNewMail(long accountServerId) => _pending[accountServerId] = 1;

    /// <summary>Reports whether new mail is pending and clears it - Mail_Check semantics.</summary>
    public static bool Consume(long accountServerId) => _pending.TryRemove(accountServerId, out _);
}
