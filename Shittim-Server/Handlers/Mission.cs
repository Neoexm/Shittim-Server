using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Mission
    {
        public class List : BaseHandler<MissionListRequest, MissionListResponse>
        {
            protected override async Task<MissionListResponse> Handle(MissionListRequest request)
            {
                return new MissionListResponse
                {
                    ProgressDBs = new List<MissionProgressDB>(),
                    GuideMissionSeasonDBs = new List<GuideMissionSeasonDB>(),
                    DailySuddenMissionInfo = new MissionInfo
                    {
                        Id = 1305,
                        Category = MissionCategory.DailySudden,
                        ResetType = MissionResetType.Daily,
                        Description = "Mission_Get_Item_Tag_Material",
                        IsVisible = true,
                        AccountState = AccountState.Normal,
                        AccountLevel = 1,
                        PreMissionIds = new List<long>(),
                        SuddenMissionContentTypes = new SuddenMissionContentType[] { SuddenMissionContentType.CampaignNormalStage },
                        CompleteConditionType = MissionCompleteConditionType.GetItemWithTagCount,
                        CompleteConditionCount = 5,
                        CompleteConditionParameters = new List<long> { 21 },
                        Rewards = new List<ParcelInfo>()
                    }
                };
            }
        }
    }
}
