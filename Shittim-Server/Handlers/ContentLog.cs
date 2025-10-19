using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class ContentLog
    {
        public class UIOpenStatistics : BaseHandler<ContentLogUIOpenStatisticsRequest, ContentLogUIOpenStatisticsResponse>
        {
            protected override async Task<ContentLogUIOpenStatisticsResponse> Handle(ContentLogUIOpenStatisticsRequest request)
            {
                return new ContentLogUIOpenStatisticsResponse();
            }
        }
    }
}
