using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Academy
    {
        public class GetInfo : BaseHandler<AcademyGetInfoRequest, AcademyGetInfoResponse>
        {
            protected override async Task<AcademyGetInfoResponse> Handle(AcademyGetInfoRequest request)
            {
                var academy = new AcademyDB
                {
                    AccountId = request.SessionKey.AccountServerId
                };
                return new AcademyGetInfoResponse
                {
                    AcademyDB = academy
                };
            }
        }
    }
}
