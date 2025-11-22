namespace Shittim.Models.SDK
{
    public class ImsAccountRequest
    {
        public string gid { get; set; }
        public string link_platform_token { get; set; }
        public string link_platform_type { get; set; }
    }

    public class ImsAccountResponse
    {
        public string web_token { get; set; }
        public string link_platform_user_id { get; set; }
        public List<object> links { get; set; }
    }
}
