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
    [InlineData(Protocol.Character_List)]
    [InlineData(Protocol.Attachment_Get)]
    [InlineData(Protocol.Arena_Login)]
    [InlineData(Protocol.ContentSweep_MultiSweepPresetList)]
    [InlineData(Protocol.ClearDeck_List)]
    [InlineData(Protocol.Campaign_Heal)]
    [InlineData(Protocol.Campaign_PurchasePlayCountHardStage)]
    [InlineData(Protocol.ContentSweep_SetMultiSweepPresetName)]
    [InlineData(Protocol.Character_FavorGrowth)]
    [InlineData(Protocol.Character_SetCostume)]
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
    [InlineData(Protocol.Campaign_Portal)]
    [InlineData(Protocol.Campaign_WithdrawEchelon)]
    [InlineData(Protocol.Clan_Login)]
    [InlineData(Protocol.Clan_Create)]
    [InlineData(Protocol.Clan_Join)]
    [InlineData(Protocol.Clan_AutoJoin)]
    [InlineData(Protocol.Clan_Quit)]
    [InlineData(Protocol.Clan_Dismiss)]
    [InlineData(Protocol.Clan_Search)]
    [InlineData(Protocol.Clan_MemberList)]
    [InlineData(Protocol.Clan_Member)]
    [InlineData(Protocol.Clan_Applicant)]
    [InlineData(Protocol.Clan_CancelApply)]
    [InlineData(Protocol.Clan_Permit)]
    [InlineData(Protocol.Clan_Kick)]
    [InlineData(Protocol.Clan_Confer)]
    [InlineData(Protocol.Clan_Setting)]
    [InlineData(Protocol.Clan_ChatLog)]
    [InlineData(Protocol.Conquest_GetInfo)]
    [InlineData(Protocol.Conquest_Check)]
    [InlineData(Protocol.Conquest_Conquer)]
    [InlineData(Protocol.Conquest_ConquerWithBattleStart)]
    [InlineData(Protocol.Conquest_ConquerWithBattleResult)]
    [InlineData(Protocol.Conquest_ManageBase)]
    [InlineData(Protocol.Conquest_UpgradeBase)]
    [InlineData(Protocol.Conquest_DeployEchelon)]
    [InlineData(Protocol.Conquest_NormalizeEchelon)]
    [InlineData(Protocol.Conquest_ReceiveCalculateRewards)]
    [InlineData(Protocol.Conquest_ErosionBattleStart)]
    [InlineData(Protocol.Conquest_ErosionBattleResult)]
    [InlineData(Protocol.Conquest_EventObjectBattleStart)]
    [InlineData(Protocol.Conquest_EventObjectBattleResult)]
    [InlineData(Protocol.Conquest_TakeEventObject)]
    [InlineData(Protocol.Conquest_MainStoryGetInfo)]
    [InlineData(Protocol.Conquest_MainStoryCheck)]
    [InlineData(Protocol.Conquest_MainStoryConquer)]
    [InlineData(Protocol.Conquest_MainStoryConquerWithBattleStart)]
    [InlineData(Protocol.Conquest_MainStoryConquerWithBattleResult)]
    [InlineData(Protocol.Craft_AutoBeginProcess)]
    [InlineData(Protocol.Craft_CompleteProcessAll)]
    [InlineData(Protocol.Craft_RewardAll)]
    [InlineData(Protocol.Craft_ShiftingBeginProcess)]
    [InlineData(Protocol.Craft_ShiftingCompleteProcess)]
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
