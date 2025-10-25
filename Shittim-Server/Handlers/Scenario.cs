using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Scenario
    {
        public class Skip : BaseHandler<ScenarioSkipRequest, ScenarioSkipResponse>
        {
            protected override async Task<ScenarioSkipResponse> Handle(ScenarioSkipRequest request)
            {
                return new ScenarioSkipResponse();
            }
        }

        public class GroupHistoryUpdate : BaseHandler<ScenarioGroupHistoryUpdateRequest, ScenarioGroupHistoryUpdateResponse>
        {
            protected override async Task<ScenarioGroupHistoryUpdateResponse> Handle(ScenarioGroupHistoryUpdateRequest request)
            {
                return new ScenarioGroupHistoryUpdateResponse();
            }
        }
    }
}
