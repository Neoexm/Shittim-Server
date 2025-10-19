using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Event
    {
        public class RewardIncrease : BaseHandler<EventRewardIncreaseRequest, EventRewardIncreaseResponse>
        {
            protected override async Task<EventRewardIncreaseResponse> Handle(EventRewardIncreaseRequest request)
            {
                return new EventRewardIncreaseResponse
                {
                    EventRewardIncreaseDBs = new List<EventRewardIncreaseDB>()
                };
            }
        }
    }
}
