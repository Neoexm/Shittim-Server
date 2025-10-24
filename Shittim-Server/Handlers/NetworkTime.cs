using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public class NetworkTime
    {
        [ProtocolHandler(Protocol.NetworkTime_Sync)]
        public class Sync : BaseHandler<NetworkTimeSyncRequest, NetworkTimeSyncResponse>
        {
            protected override async Task<NetworkTimeSyncResponse> Handle(NetworkTimeSyncRequest request)
            {
                var tick = DateTime.Now.Ticks;
                // Match Atrahasis: only ReceiveTick and EchoSendTick, no SendTick or EchoReceiveTick
                return new NetworkTimeSyncResponse
                {
                    ReceiveTick = tick,
                    EchoSendTick = tick
                };
            }
        }
    }
}
