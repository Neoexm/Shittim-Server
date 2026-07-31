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
        // Official responses carry a top-level AccountId exactly when they carry a SessionKey (Account_CheckNexon / Account_Auth / ProofToken_*); everywhere else both are omitted.
        // Serialized: 0 (no session) is dropped by the gateway's DefaultValueHandling.Ignore.
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
        // Official ServerTimeTicks are whole-second precision (...0000000); mirror that.
        public long ServerTimeTicks { get; set; } = TruncateToSeconds(DateTimeOffset.Now.Ticks);
        // Official emits this whenever non-zero (e.g. 8 = HasUnreadMail on every response while unclaimed mail exists); None falls out of serialization like any other default.
        public ServerNotificationFlag ServerNotification { get; set; } = ServerNotificationFlag.None;

        public static long TruncateToSeconds(long ticks) => ticks - (ticks % TimeSpan.TicksPerSecond);
        public List<MissionProgressDB>? MissionProgressDBs { get; set; }
        public Dictionary<long, List<MissionProgressDB>>? EventMissionProgressDBDict { get; set; }
        public Dictionary<OpenConditionContent, OpenConditionLockReason>? StaticOpenConditions { get; set; }
    }
}




