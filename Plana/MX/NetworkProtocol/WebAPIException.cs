namespace Plana.MX.NetworkProtocol
{
    public class WebAPIException : Exception
    {
        public WebAPIErrorCode ErrorCode { get; }

        public WebAPIException()
        {
            ErrorCode = WebAPIErrorCode.InternalServerError;
        }

        public WebAPIException(WebAPIErrorCode errorCode)
        {
            ErrorCode = errorCode;
        }

        public WebAPIException(WebAPIErrorCode errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}