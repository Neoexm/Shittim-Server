using BlueArchiveAPI.NetworkModels;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
        public ServerPacket(NetworkModels.Protocol protocol, string packet)
        {
            this.Protocol = protocol.ToString();
            this.Packet = packet;
        }
    }

}
