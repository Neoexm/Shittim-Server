using System.Text.Json.Serialization;

namespace Shittim.Models.SDK
{
    public class SignInWithTicketToyRequest
    {
        public string appId { get; set; }
        public string locale { get; set; }
        public string mk { get; set; }
        public string os { get; set; }
        public int termsApiVer { get; set; }
        public string uuid { get; set; }
    }

    public class SignInWithTicketToyResponse
    {
        public int errorCode { get; set; }
        public SignInTicketResult result { get; set; }
        public string errorText { get; set; }
        public string errorDetail { get; set; }
    }

    public class SignInTicketResult
    {
        public string npSN { get; set; }
        public string guid { get; set; }
        public string umKey { get; set; }
        public bool isSwap { get; set; }
        public List<TermAgreement> terms { get; set; }
        public string npaCode { get; set; }
    }

    public class TermAgreement
    {
        public int termID { get; set; }
        public List<object> type { get; set; }
        public int optional { get; set; }
        public string exposureType { get; set; }
        public string title { get; set; }
        public List<object> titleReplacements { get; set; }
        public int isAgree { get; set; }
        public int isUpdate { get; set; }
    }
}
