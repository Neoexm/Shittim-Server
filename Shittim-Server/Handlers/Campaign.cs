using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Campaign
    {
        public class List : BaseHandler<CampaignListRequest, CampaignListResponse>
        {
            protected override async Task<CampaignListResponse> Handle(CampaignListRequest request)
            {
                return new CampaignListResponse
                {
                    StageHistoryDBs = new List<CampaignStageHistoryDB>()
                };
            }
        }
    }
}
