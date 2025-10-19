using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Mail
    {
        public class Check : BaseHandler<MailCheckRequest, MailCheckResponse>
        {
            protected override async Task<MailCheckResponse> Handle(MailCheckRequest request)
            {
                return new MailCheckResponse();
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
