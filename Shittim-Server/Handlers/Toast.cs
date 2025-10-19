using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Toast
    {
        public class List : BaseHandler<ToastListRequest, ToastListResponse>
        {
            protected override async Task<ToastListResponse> Handle(ToastListRequest request)
            {
                return new ToastListResponse
                {
                    ToastDBs = new List<ToastDB>()
                };
            }
        }
    }
}
