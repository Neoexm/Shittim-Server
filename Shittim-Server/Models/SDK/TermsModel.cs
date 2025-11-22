namespace Shittim.Models.SDK
{
    public class TermsRequest
    {
        public string gid { get; set; }
        public string locale { get; set; }
        public string method { get; set; }
        public string npsn { get; set; }
        public int termsApiVer { get; set; }
        public string uuid { get; set; }
    }

    public class Term
    {
        public int termID { get; set; }
        public List<object> type { get; set; }
        public int optional { get; set; }
        public string exposureType { get; set; }
        public string title { get; set; }
        public List<object> titleReplacements { get; set; }
    }

    public class TermResult
    {
        public List<Term> terms { get; set; }
    }

    public class TermsResponse
    {
        public int errorCode { get; set; }
        public TermResult result { get; set; }
        public string errorText { get; set; }
        public string errorDetail { get; set; }
    }
}
