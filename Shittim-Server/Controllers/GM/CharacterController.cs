using Microsoft.AspNetCore.Mvc;
using Shittim.GameMasters;
using Shittim.Models.GM;
using Shittim.Services.WebClient;
using Serilog;

namespace Shittim.Controllers.GM
{
    [ApiController]
    [Route("/dev/api")]
    public class CharacterController : ControllerBase
    {
        private readonly WebService webService;

        public CharacterController(WebService _webService)
        {
            webService = _webService;
        }

        [HttpPost]
        [Route("get_character")]
        public async Task<IResult> GetCharacterDefault(GetUserRequest request)
        {
            var conn = webService.GetClient(request.UserID);
            try
            {
                List<FullCharacterData> characterDataList = await CharacterGM.GetCharacters(conn);
                return BaseAPIResponse.Create(ResponseStatus.Success, characterDataList);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while fetching character data");
                return BaseAPIResponse.Create(ResponseStatus.Error, $"An error occurred while fetching character data: {ex.Message}");
            }
        }
        
        [HttpPost]
        [Route("modify_character")]
        public async Task<IResult> ModifyCharacterDefault(ModifyCharacterRequest characterReq)
        {
            var conn = webService.GetClient(characterReq.UserID);
            try
            {
                await CharacterGM.ModifyCharacter(conn, characterReq);
                return BaseAPIResponse.Create(ResponseStatus.Success, "Character saved successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while modifying character");
                return BaseAPIResponse.Create(ResponseStatus.Error, $"An error occurred while modifying character: {ex.Message}");
            }
        }

    }
}
