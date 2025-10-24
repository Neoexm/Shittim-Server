using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Mission
    {
        public class List : BaseHandler<MissionListRequest, MissionListResponse>
        {
            protected override async Task<MissionListResponse> Handle(MissionListRequest request)
            {
                // Match Atrahasis: use MissionHistoryUniqueIds instead of HistoryDBs
                return new MissionListResponse
                {
                    MissionHistoryUniqueIds = new List<long>(),
                    ProgressDBs = new List<MissionProgressDB>()
                };
            }
        }

        public class GuideMissionSeasonList : BaseHandler<MissionGuideMissionSeasonListRequest, MissionGuideMissionSeasonListResponse>
        {
            protected override async Task<MissionGuideMissionSeasonListResponse> Handle(MissionGuideMissionSeasonListRequest request)
            {
                // For fresh accounts, return empty response (no GuideMissionSeasonDBs field)
                // Only Protocol, ServerTimeTicks, ServerNotification, SessionKey, AccountId will be present
                return new MissionGuideMissionSeasonListResponse();
            }
        }
    }
}
