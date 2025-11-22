using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shittim.GameMasters;
using Shittim.Models.GM;
using Shittim.Services;
using Shittim.Services.WebClient;
using Schale.Data;
using Serilog;
using Shittim_Server.Services;
using Shittim_Server.GameClient;

namespace Shittim.Controllers.GM
{
    [ApiController]
    [Route("/dev/api")]
    public class ArenaController : ControllerBase
    {
        private readonly IDbContextFactory<SchaleDataContext> contextFactory;
        private readonly WebService webService;

        public ArenaController(IDbContextFactory<SchaleDataContext> _contextFactory, WebService _webService)
        {
            contextFactory = _contextFactory;
            webService = _webService;
        }

        [HttpGet]
        [Route("get_arena_dummy")]
        public async Task<IResult> GetArenaDummy()
        {
            try
            {
                await using var context = await contextFactory.CreateDbContextAsync();
                var echelonDefenseTeam = EchelonService.GetArenaDefenseEchelon(context, SchaleAI.AccountId);

                ArenaTeamData teamData = new() { Main = [], Support = [] };
                foreach (var characterId in echelonDefenseTeam.MainSlotServerIds)
                    teamData.Main.Add(await CharacterGM.GetCharacterCompleteInfo(context, SchaleAI.AccountId, characterId));
                foreach (var characterId in echelonDefenseTeam.SupportSlotServerIds)
                    teamData.Support.Add(await CharacterGM.GetCharacterCompleteInfo(context, SchaleAI.AccountId, characterId));

                return BaseAPIResponse.Create(ResponseStatus.Success, teamData);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while fetching arena defense team");
                return BaseAPIResponse.Create(ResponseStatus.Error, $"An internal error occurred while fetching the arena defense team. Error: {ex.Message}");
            }
        }

        [HttpGet]
        [Route("get_arena_records")]
        public IResult GetArenaRecords()
        {
            List<ArenaBattleStatEntry> arenaRecords = ArenaUtils.GetAllStats();
            return BaseAPIResponse.Create(ResponseStatus.Success, arenaRecords);
        }

        [HttpGet]
        [Route("get_arena_summaries")]
        public IResult GetArenaSummaries()
        {
            List<ArenaTeamSummary> arenaSummaries = ArenaUtils.GetSummaries();
            return BaseAPIResponse.Create(ResponseStatus.Success, arenaSummaries);
        }

        [HttpPost]
        [Route("set_arena_dummy")]
        public async Task<IResult> ModifyCharacterArena(ModifyCharacterRequest characterReq)
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var conn = webService.GetClient(SchaleAI.AccountId);
            try
            {
                await CharacterGM.ModifyCharacter(conn, characterReq);
                return BaseAPIResponse.Create(ResponseStatus.Success, (object)"Character saved successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred while modifying character");
                return BaseAPIResponse.Create(ResponseStatus.Error, $"An error occurred while modifying character: {ex.Message}");
            }
        }

        [HttpPost]
        [Route("delete_arena_record")]
        public IResult DeleteArenaRecord(ArenaDeleteRecordRequest request)
        {
            try
            {
                bool success = ArenaUtils.DeleteRecord(request.Record);
                if (success)
                    return BaseAPIResponse.Create(ResponseStatus.Success, "Record deleted successfully.");
                else
                    return BaseAPIResponse.Create(ResponseStatus.Failure, "Matching record not found to delete.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting arena record via API.");
                return BaseAPIResponse.Create(ResponseStatus.Error, "An internal error occurred while deleting the record.");
            }
        }

        [HttpPost]
        [Route("delete_arena_summary")]
        public IResult DeleteArenaSummary(ArenaDeleteSummaryRequest request)
        {
            try
            {
                int deletedCount = ArenaUtils.DeleteSummary(request.AttackingTeamIds, request.DefendingTeamIds);
                if (deletedCount > 0)
                    return BaseAPIResponse.Create(ResponseStatus.Success, $"{deletedCount} records for the summary were deleted successfully.");
                else
                    return BaseAPIResponse.Create(ResponseStatus.Failure, "No records found for this summary.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting arena summary via API.");
                return BaseAPIResponse.Create(ResponseStatus.Error, "An internal error occurred while deleting the summary.");
            }
        }
    }
}
