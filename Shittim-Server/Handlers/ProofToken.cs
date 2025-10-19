using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class ProofToken
    {
        public class RequestQuestion : BaseHandler<ProofTokenRequestQuestionRequest, ProofTokenRequestQuestionResponse>
        {
            protected override async Task<ProofTokenRequestQuestionResponse> Handle(ProofTokenRequestQuestionRequest request)
            {
                return new ProofTokenRequestQuestionResponse
                {
                    Question = "2RGQXAKSDSXDSADID2QKTB5JYU",
                    Hint = 5861337060617289728
                };
            }
        }
        public class Submit : BaseHandler<ProofTokenSubmitRequest, ProofTokenSubmitResponse>
        {
            protected override async Task<ProofTokenSubmitResponse> Handle(ProofTokenSubmitRequest request)
            {
                return new ProofTokenSubmitResponse();
            }
        }
    }
}
