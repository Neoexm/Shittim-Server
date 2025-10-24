using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Mail
    {
        public class Check : BaseHandler<MailCheckRequest, MailCheckResponse>
        {
            protected override async Task<MailCheckResponse> Handle(MailCheckRequest request)
            {
                // Match Atrahasis: return realistic count (2 for fresh accounts)
                return new MailCheckResponse
                {
                    Count = 2
                };
            }
        }
    }
    public static class Clan
    {
        public class Check : BaseHandler<ClanCheckRequest, ClanCheckResponse>
        {
            protected override async Task<ClanCheckResponse> Handle(ClanCheckRequest request)
            {
                return new ClanCheckResponse();
            }
        }
    }
    public static class Friend
    {
        public class Check : BaseHandler<FriendCheckRequest, FriendCheckResponse>
        {
            protected override async Task<FriendCheckResponse> Handle(FriendCheckRequest request)
            {
                return new FriendCheckResponse();
            }
        }
    }
}
