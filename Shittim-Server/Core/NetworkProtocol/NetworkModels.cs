namespace BlueArchiveAPI.Core.NetworkProtocol
{
    public class ServerResponsePacket
    {
        public string Protocol { get; set; } = string.Empty;
        public string Packet { get; set; } = string.Empty;
    }

    public class ErrorPacket
    {
        public string Reason { get; set; } = string.Empty;
        public int ErrorCode { get; set; }
    }

    public enum WebAPIErrorCode
    {
        ServerFailedToHandleRequest = 1,
        ServerCacheFailedToHandleRequest = 2,
        InvalidSession = 3
    }

    public class WebAPIException : Exception
    {
        public WebAPIErrorCode ErrorCode { get; }
        
        public WebAPIException(string message, WebAPIErrorCode errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
