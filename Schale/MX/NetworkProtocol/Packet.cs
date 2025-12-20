using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Services;
using Newtonsoft.Json;

namespace Schale.MX.NetworkProtocol
{
    public class SessionKey
    {
        public long AccountServerId { get; set; }
        public required string MxToken { get; set; }
    }

    public abstract class BasePacket
    {
        public SessionKey? SessionKey { get; set; }
        public abstract Protocol Protocol { get; }
        [JsonIgnore]
        public long AccountId => SessionKey?.AccountServerId ?? 0;
    }

    public abstract class RequestPacket : BasePacket
    {
	    private static int _counter;

        public int ClientUpTime { get; set; }
        public bool Resendable { get; set; } = true;
        public long Hash { get; set; }
        public bool IsTest { get; set; }
        public DateTime? ModifiedServerTime__DebugOnly { get; set; }

        public static long CreateHash(Protocol protocol)
        {
            return _counter++ | ((int)protocol << 32);
        }
    }

    public class ServerResponsePacket
    {
        public required string Protocol { get; set; }
        public required string Packet { get; set; }
    }

    public abstract class ResponsePacket : BasePacket
    {
        public long ServerTimeTicks { get; set; } = DateTimeOffset.Now.Ticks;
        [JsonIgnore]
        public ServerNotificationFlag ServerNotification { get; set; } = ServerNotificationFlag.None;
        public List<MissionProgressDB>? MissionProgressDBs { get; set; }
        public Dictionary<long, List<MissionProgressDB>>? EventMissionProgressDBDict { get; set; }
        public Dictionary<OpenConditionContent, OpenConditionLockReason>? StaticOpenConditions { get; set; }
    }
}




