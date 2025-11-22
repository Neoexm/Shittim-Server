using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shittim.Models.GM;
using Shittim.Services.WebClient;
using Schale.Data;

namespace Shittim.Controllers.GM
{
    [ApiController]
    [Route("/dev/api")]
    public class SettingsController : ControllerBase
    {
        private readonly IDbContextFactory<SchaleDataContext> contextFactory;
        private readonly WebService webService;

        public SettingsController(IDbContextFactory<SchaleDataContext> _contextFactory, WebService _webService)
        {
            contextFactory = _contextFactory;
            webService = _webService;
        }

        [HttpPost]
        [Route("get_settings")]
        public async Task<IResult> GetSettings(GetUserRequest request)
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var account = context.Accounts.FirstOrDefault(x => x.ServerId == request.UserID);
            if (account == null)
                return BaseAPIResponse.Create(ResponseStatus.Error, "Account not found");
            int? offsetDays = null;
            if (account.GameSettings.ForceDateTime)
            {
                var now = DateTimeOffset.Now;
                var target = account.GameSettings.ForceDateTimeOffset;
                offsetDays = (int)(target - now).TotalDays;
            }
            var resp = new
            {
                trackpvp = account.GameSettings.EnableArenaTracker,
                usefinal = account.GameSettings.EnableMultiFloorRaid,
                bypassteam = account.GameSettings.BypassTeamDeployment,
                bypasssummon = account.GameSettings.BypassCafeSummon,
                changetime = new
                {
                    enabled = account.GameSettings.ForceDateTime,
                    offset = offsetDays
                }
            };
            return BaseAPIResponse.Create(ResponseStatus.Success, resp);
        }

        [HttpPost]
        [Route("set_settings")]
        public async Task<IResult> SetSettings(SetSettingsRequest request)
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var account = context.Accounts.FirstOrDefault(x => x.ServerId == request.UserID);
            if (account == null)
                return BaseAPIResponse.Create(ResponseStatus.Error, "Account not found");

            account.GameSettings.EnableArenaTracker =
                request.TrackPvp ?? account.GameSettings.EnableArenaTracker;
            account.GameSettings.EnableMultiFloorRaid =
                request.UseFinal ?? account.GameSettings.EnableMultiFloorRaid;
            account.GameSettings.BypassTeamDeployment =
                request.BypassTeam ?? account.GameSettings.BypassTeamDeployment;
            account.GameSettings.BypassCafeSummon =
                request.BypassSummon ?? account.GameSettings.BypassCafeSummon;

            if (request.Changetime != null)
            {
                var ct = request.Changetime;
                if (ct.Offset.HasValue)
                {
                    account.GameSettings.ForceDateTime = true;
                    account.GameSettings.ForceDateTimeOffset = DateTimeOffset.Now.AddDays(ct.Offset.Value);
                }

                if (ct.Enabled.HasValue)
                {
                    account.GameSettings.ForceDateTime = ct.Enabled.Value;
                    if (ct.Enabled.Value && !ct.Offset.HasValue)
                        account.GameSettings.CurrentDateTime = DateTimeOffset.Now;
                }
            }

            await context.SaveChangesAsync();
            return BaseAPIResponse.Create(ResponseStatus.Success);
        }
    }
}
