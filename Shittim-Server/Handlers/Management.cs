using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Management
    {
        public class BannerList : BaseHandler<ManagementBannerListRequest, ManagementBannerListResponse>
        {
            protected override async Task<ManagementBannerListResponse> Handle(ManagementBannerListRequest request)
            {
                return new ManagementBannerListResponse
                {
                    BannerDBs = new List<BannerDB>()
                };
            }
        }
        public class ContentsLockList : BaseHandler<ManagementContentsLockListRequest, ManagementContentsLockListResponse>
        {
            protected override async Task<ManagementContentsLockListResponse> Handle(ManagementContentsLockListRequest request)
            {
                return new ManagementContentsLockListResponse
                {
                    ContentsLockDBs = new List<ContentsLockDB>()
                };
            }
        }
    }
    public static class Notification
    {
        public class EventContentReddotCheck : BaseHandler<NotificationEventContentReddotRequest, NotificationEventContentReddotResponse>
        {
            protected override async Task<NotificationEventContentReddotResponse> Handle(NotificationEventContentReddotRequest request)
            {
                return new NotificationEventContentReddotResponse
                {
                    Reddots = new Dictionary<long, List<NotificationEventReddot>>()
                };
            }
        }
    }
}
