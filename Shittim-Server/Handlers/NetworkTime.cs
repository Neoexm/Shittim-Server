using BlueArchiveAPI.NetworkModels;
using Protocol = Plana.MX.NetworkProtocol.Protocol;

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
                return new NetworkTimeSyncResponse
                {
                    ReceiveTick = tick,
                    EchoSendTick = tick
                };
            }
        }
    }
}

