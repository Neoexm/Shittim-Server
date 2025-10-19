using BlueArchiveAPI.NetworkModels;

namespace BlueArchiveAPI.Handlers
{
    public static class Character
    {
        public class SetFavorites : BaseHandler<CharacterSetFavoritesRequest, CharacterSetFavoritesResponse>
        {
            protected override async Task<CharacterSetFavoritesResponse> Handle(CharacterSetFavoritesRequest request)
            {
                return new CharacterSetFavoritesResponse
                {
                    CharacterDBs = new List<CharacterDB>()
                };
            }
        }
    }
}
