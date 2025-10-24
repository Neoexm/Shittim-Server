using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class OpenCondition
    {
        [ProtocolHandler(Protocol.OpenCondition_EventList)]
        public class EventList : BaseHandler<OpenConditionEventListRequest, OpenConditionEventListResponse>
        {
            protected override async Task<OpenConditionEventListResponse> Handle(OpenConditionEventListRequest request)
            {
                return new OpenConditionEventListResponse
                {
                    ConquestTiles = new Dictionary<long, List<ConquestTileDB>>(),
                    WorldRaidLocalBossDBs = new Dictionary<long, List<WorldRaidLocalBossDB>>(),
                    StaticOpenConditions = Enum.GetValues(typeof(OpenConditionContent))
                        .Cast<OpenConditionContent>()
                        .ToDictionary(key => key.ToString(), key => 0)  // String keys, integer 0 values
                };
            }
        }
    }
}
