using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class ContentSave
    {
        public class Get : BaseHandler<ContentSaveGetRequest, ContentSaveGetResponse>
        {
            protected override async Task<ContentSaveGetResponse> Handle(ContentSaveGetRequest request)
            {
                // Match Atrahasis: HasValidData false for fresh accounts
                return new ContentSaveGetResponse
                {
                    HasValidData = false
                };
            }
        }
    }
}
