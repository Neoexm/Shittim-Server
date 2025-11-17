// This file is part of FlatData, since some of enum flatdata doesnt get included.
namespace Schale.FlatData
{
    public enum NotificationEventReddot
    {
        StagePointReward,
        MissionComplete,
        MiniGameMissionComplete,
        WorldRaidReward,
        ConquestCalculateReward,
        DiceRaceLapReward
    }

    public enum RaidSeasonType
    {
        None,
        Open,
        Close,
        Settlement
    }

    public enum RankingSearchType
    {
        None,
        Rank,
        Score
    }

    public enum FriendSearchLevelOption
    {
        Recommend,
        All,
        Level1To30,
        Level31To40,
        Level41To50,
        Level51To60,
        Level61To70,
        Level71To80,
        Level81To90,
        Level91To100
    }

    public enum ClanSocialGrade
    {
        None,
        President,
        Manager,
        Member,
        Applicant,
        Refused,
        Kicked,
        Quit,
        VicePredisident
    }

    public enum ClanJoinOption
    {
        Free,
        Permission,
        All
    }

    public enum PurchaseServerTag
    {
        Audit,
        PreAudit,
        Production,
        Hotfix,
        Standby2,
        Standby1,
        Major,
        Minor,
        Temp,
        Test,
        TestIn
    }

    public enum BillingTransactionEndType
    {
        None,
        Success,
        Cancel
    }

    public enum ContentsChangeType
    {
        None,
        WorldRaidBossDamageRatio,
        WorldRaidBossGroupDate
    }

    public enum PurchaseStatusCode
    {
        None,
        Start,
        PublishSuccess,
        End,
        Error,
        DuplicateOrder,
        Refund
    }

    public enum RaidStatus
    {
        None,
        Playing,
        Clear,
        Close
    }

    public enum ResetContentType
    {
        None,
        HardStagePlay,
        StarategyMapHeal,
        ShopRefresh,
        ArenaDefenseVictoryReward,
        WeeklyMasterCoin,
        WorldRaidGemEnterCount,
        ConquestDailyErosionCheck,
        MiniEventToken
    }

    public enum TileState
    {
        None,
        PartiallyConquested,
        FullyConquested
    }

    public enum MailSortingRule
    {
        ReceiptDate,
        ExpireDate
    }
    
    public enum Language
	{
		Kr,
		Jp,
		Th,
		Tw,
		En
	}
}





