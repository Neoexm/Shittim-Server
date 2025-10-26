namespace Schale.MX.NetworkProtocol
{
    public class WebAPIException : Exception
    {
        public WebAPIErrorCode ErrorCode { get; }

        public WebAPIException() : this(WebAPIErrorCode.InternalServerError)
        {
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



