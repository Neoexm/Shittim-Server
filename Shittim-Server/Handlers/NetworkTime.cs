using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public class NetworkTime
    {
        public class Sync : BaseHandler<NetworkTimeSyncRequest, NetworkTimeSyncResponse>
        {
            protected override async Task<NetworkTimeSyncResponse> Handle(NetworkTimeSyncRequest request)
            {
                var tick = DateTime.Now.Ticks;
                return new NetworkTimeSyncResponse
                {
                    SendTick = tick,
                    ReceiveTick = tick,
                    EchoSendTick = tick
                };
            }
        }
    }
}
