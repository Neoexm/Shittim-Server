using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;

namespace Schale.Data.GameModel
{
    // The account's membership in the single emulated clan. HasClan defaults true so existing accounts
    // deserialize as already inside the stock Arona clan, which is what the shipped Clan_Lobby always showed.
    public class ClanStateDB
    {
        public bool HasClan { get; set; } = true;
        public bool IsPlayerOwned { get; set; }
        public string? ClanName { get; set; }
        public string? Notice { get; set; }
        public ClanJoinOption JoinOption { get; set; } = ClanJoinOption.Free;
        public DateTime JoinDate { get; set; }
    }

    public class AccountGameSettingDB
    {
        public bool BypassTeamDeployment { get; set; } = false;
        public bool EnableArenaTracker { get; set; } = false;
        public bool EnableMultiFloorRaid { get; set; } = false;
        public bool ForceDateTime { get; set; } = false;
        public bool BypassCafeSummon { get; set; } = false;
        public List<MultiSweepPresetDB> MultiSweepPresetDBs { get; set; } = [];
        public bool CheckAdultAgree { get; set; }
        public SkipHistoryDB? SkipHistory { get; set; }
        public List<OpenConditionDB> OpenConditions { get; set; } = [];
        public List<ResetableContentValueDB> ResetableContents { get; set; } = [];
        public int LastBirthdayMailYear { get; set; }
        public List<long> BlockedAccountIds { get; set; } = [];
        public FriendIdCardDB? FriendIdCard { get; set; }
        public Dictionary<long, long> PendingPurchaseOrders { get; set; } = [];
        public ClanStateDB Clan { get; set; } = new();
        public DateTimeOffset ForceDateTimeOffset { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset CurrentDateTime { get; set; } = DateTimeOffset.Now;

        public long ServerDateTimeTicks()
        {
            if (ForceDateTime)
            {
                var offset = DateTimeOffset.Now.Ticks - CurrentDateTime.Ticks;
                return ForceDateTimeOffset.Ticks + offset;
            }
            
            return DateTimeOffset.Now.Ticks;
        }

        public DateTime ServerDateTime()
        {
            return new DateTime(ServerDateTimeTicks());
        }

        public DateTimeOffset ServerDateTimeOffset()
        {
            return new DateTimeOffset(ServerDateTimeTicks(), TimeSpan.Zero);
        }
    }
}


