using BlueArchiveAPI.NetworkModels;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Protocol = Plana.MX.NetworkProtocol.Protocol;

namespace BlueArchiveAPI.Models
{
    public class ServerResponsePacket
    {
        public string Protocol { get; set; }
        public string Packet { get; set; }
    }
    
    // Alias for backwards compatibility
    public class ServerPacket : ServerResponsePacket
    {
        public ServerPacket(Protocol protocol, string packet)
        {
            this.Protocol = protocol.ToString();
            this.Packet = packet;
        }
    }

}

