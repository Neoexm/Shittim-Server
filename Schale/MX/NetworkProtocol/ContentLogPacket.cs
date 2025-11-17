using Schale.MX.NetworkProtocol;

namespace Schale.MX.NetworkProtocol
{
    public class ContentLogUIOpenStatisticsRequest : RequestPacket
    {
        public override Protocol Protocol => Protocol.ContentLog_UIOpenStatistics;
    }

    public class ContentLogUIOpenStatisticsResponse : ResponsePacket
    {
        public override Protocol Protocol => Protocol.ContentLog_UIOpenStatistics;
    }
}
