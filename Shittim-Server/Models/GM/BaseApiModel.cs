namespace Shittim.Models.GM
{
    public abstract class BaseAPIRequest
    {
        public long UserID { get; set; }
    }

    public class BaseAPIResponse
    {
        public string Status { get; set; } = ResponseStatus.Failure.ToString();
        public string? Message { get; set; }
        public object? Data { get; set; }

        public static IResult Create(ResponseStatus status)
        {
            return Results.Json(new BaseAPIResponse()
            {
                Status = status.ToString()
            });
        }

        public static IResult Create(ResponseStatus status, object? data = null)
        {
            return Results.Json(new BaseAPIResponse()
            {
                Status = status.ToString(),
                Data = data
            });
        }

        public static IResult Create(ResponseStatus status, string? message, object? data = null)
        {
            return Results.Json(new BaseAPIResponse()
            {
                Status = status.ToString(),
                Message = message,
                Data = data
            });
        }
    }

    public enum ResponseStatus
    {
        Success,
        Failure,
        Error
    }
}
