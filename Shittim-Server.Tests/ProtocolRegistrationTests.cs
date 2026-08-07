using System.Reflection;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Xunit;

namespace Shittim_Server.Tests;

public class ProtocolRegistrationTests
{
    // An unregistered protocol is answered with ServerFailedToHandleRequest, which the client shows as
    // "Server failed to process request. Returning to the title screen." These are the ones a normal session
    // cannot get past, so a handler going missing should fail here rather than in someone's play session.
    [Theory]
    [InlineData(Protocol.Scenario_Enter)]
    [InlineData(Protocol.Scenario_GroupHistoryUpdate)]
    [InlineData(Protocol.Scenario_Clear)]
    [InlineData(Protocol.MomoTalk_OutLine)]
    [InlineData(Protocol.MomoTalk_Read)]
    [InlineData(Protocol.MomoTalk_FavorSchedule)]
    [InlineData(Protocol.Shop_BuyMerchandise)]
    [InlineData(Protocol.Mission_Reward)]
    [InlineData(Protocol.Mission_MultipleReward)]
    [InlineData(Protocol.Craft_List)]
    [InlineData(Protocol.Craft_SelectNode)]
    [InlineData(Protocol.Account_Auth2)]
    [InlineData(Protocol.Account_CurrencySync)]
    [InlineData(Protocol.Account_BirthDay)]
    [InlineData(Protocol.Account_SetCheckAdultAgree)]
    [InlineData(Protocol.Account_VerifyCheckAdultAgree)]
    [InlineData(Protocol.Account_DismissRepurchasablePopup)]
    [InlineData(Protocol.Account_ReportXignCodeCheater)]
    [InlineData(Protocol.Attachment_Get)]
    [InlineData(Protocol.Arena_Login)]
    [InlineData(Protocol.Cafe_Travel)]
    [InlineData(Protocol.Audit_GachaStatistics)]
    [InlineData(Protocol.Account_CheckYostar)]
    [InlineData(Protocol.Account_PassCheck)]
    [InlineData(Protocol.Account_DetachNexon)]
    [InlineData(Protocol.Account_RequestBirthdayMail)]
    [InlineData(Protocol.Account_Reset)]
    [InlineData(Protocol.Billing_PurchaseListByYostar)]
    [InlineData(Protocol.Billing_TransactionStartByYostar)]
    [InlineData(Protocol.Billing_TransactionEndByYostar)]
    [InlineData(Protocol.BattlePass_MissionSingleReward)]
    [InlineData(Protocol.BattlePass_MissionMultipleReward)]
    [InlineData(Protocol.Arena_EnterBattle)]
    [InlineData(Protocol.Campaign_ConfirmTutorialStage)]
    public void TheProtocolHasAHandler(Protocol protocol)
    {
        Assert.Contains(protocol, HandledProtocols);
    }

    private static readonly HashSet<Protocol> HandledProtocols = typeof(ProtocolHandlerBase).Assembly
        .GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.IsAssignableTo(typeof(ProtocolHandlerBase)))
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        .SelectMany(m => m.GetCustomAttributes<ProtocolHandlerAttribute>())
        .Select(a => a.Protocol)
        .Where(p => p != Protocol.None)
        .ToHashSet();
}
