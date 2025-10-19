using BlueArchiveAPI.NetworkModels;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlueArchiveAPI.Models
{
    public class ServerPacket
    {
        public Protocol protocol { get; set; }
        public string packet { get; set; }
        public ServerPacket(Protocol protocol, string packet)
        {
            this.packet = packet;
            this.protocol = protocol;
        }
    }

}
