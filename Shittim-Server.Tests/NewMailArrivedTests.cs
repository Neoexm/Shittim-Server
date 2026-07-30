using Shittim_Server.Services;
using Xunit;

namespace Shittim_Server.Tests;

public class NewMailArrivedTests
{
    [Fact]
    public void ReportedOnce()
    {
        MailNotificationService.MarkNewMail(910001);

        Assert.True(MailNotificationService.Consume(910001));
        Assert.False(MailNotificationService.Consume(910001));
    }

    [Fact]
    public void NothingPendingNothingReported()
    {
        Assert.False(MailNotificationService.Consume(910002));
    }

    [Fact]
    public void FlagIsPerAccount()
    {
        MailNotificationService.MarkNewMail(910003);

        Assert.False(MailNotificationService.Consume(910004));
        Assert.True(MailNotificationService.Consume(910003));
    }

    [Fact]
    public void TwoDeliveriesOneReport()
    {
        // A set, not a counter: official's capture mails rewards twice before the client checks,
        // and the single Mail_Check consumes everything - 12 once, then straight back to 8.
        MailNotificationService.MarkNewMail(910005);
        MailNotificationService.MarkNewMail(910005);

        Assert.True(MailNotificationService.Consume(910005));
        Assert.False(MailNotificationService.Consume(910005));
    }
}
