using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

/// <summary>
/// Semantics of the in-memory NewMailArrived state (see MailNotificationService for the captured
/// evidence): a per-account set, reported exactly once by Mail_Check. The service is static, so
/// every test uses its own account ids to stay independent under xunit's parallel runner.
/// </summary>
public class NewMailArrivedTests
{
    [Fact]
    public void PendingMailIsReportedExactlyOnce()
    {
        MailNotificationService.MarkNewMail(910001);

        Assert.True(MailNotificationService.Consume(910001));
        Assert.False(MailNotificationService.Consume(910001));
    }

    [Fact]
    public void NothingPendingMeansNothingReported()
    {
        Assert.False(MailNotificationService.Consume(910002));
    }

    [Fact]
    public void AccountsDoNotShareTheFlag()
    {
        MailNotificationService.MarkNewMail(910003);

        Assert.False(MailNotificationService.Consume(910004));
        Assert.True(MailNotificationService.Consume(910003));
    }

    [Fact]
    public void TwoDeliveriesStillConsumeAsOne()
    {
        // A set, not a counter: official's capture mails rewards twice before the client checks,
        // and the single Mail_Check consumes everything — 12 once, then straight back to 8.
        MailNotificationService.MarkNewMail(910005);
        MailNotificationService.MarkNewMail(910005);

        Assert.True(MailNotificationService.Consume(910005));
        Assert.False(MailNotificationService.Consume(910005));
    }
}
