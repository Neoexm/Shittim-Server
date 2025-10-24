using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlueArchiveAPI.Handlers
{
    public static class Academy
    {
        /// <summary>
        /// Returns academy info including locations and visiting characters
        /// Protocol: Academy_GetInfo
        /// </summary>
        public class GetInfo : BaseHandler<AcademyGetInfoRequest, AcademyGetInfoResponse>
        {
            private readonly BAContext _dbContext;

            public GetInfo(BAContext dbContext)
            {
                _dbContext = dbContext;
            }

            protected override async Task<AcademyGetInfoResponse> Handle(AcademyGetInfoRequest request)
            {
                var user = await _dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.SessionKey.AccountServerId);

                if (user == null)
                    throw new Exception("User not found");

                // Get academy
                var academy = await _dbContext.Academies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.AccountServerId == user.Id);

                if (academy == null)
                    throw new Exception("Academy not found - account initialization incomplete");

                // Get academy locations
                var academyLocations = await _dbContext.AcademyLocations
                    .AsNoTracking()
                    .Where(l => l.AccountServerId == user.Id)
                    .ToListAsync();

                // Load zone visitors from JSON
                var zoneVisitors = new Dictionary<long, List<VisitingCharacterDB>>();
                if (!string.IsNullOrEmpty(academy.ZoneVisitCharacterIds))
                {
                    var visitorDict = JsonSerializer.Deserialize<Dictionary<long, List<long>>>(academy.ZoneVisitCharacterIds);
                    if (visitorDict != null)
                    {
                        var characters = await _dbContext.Characters
                            .AsNoTracking()
                            .Where(c => c.AccountServerId == user.Id)
                            .ToListAsync();

                        foreach (var kvp in visitorDict)
                        {
                            var visitorsForZone = new List<VisitingCharacterDB>();
                            foreach (var charUniqueId in kvp.Value)
                            {
                                var character = characters.FirstOrDefault(c => c.UniqueId == charUniqueId);
                                if (character != null)
                                {
                                    visitorsForZone.Add(new VisitingCharacterDB
                                    {
                                        UniqueId = character.UniqueId,
                                        ServerId = character.ServerId
                                    });
                                }
                            }
                            zoneVisitors[kvp.Key] = visitorsForZone;
                        }
                    }
                }

                // Create AcademyDB response
                var academyDB = new AcademyDB
                {
                    AccountId = user.Id,
                    LastUpdate = DateTime.UtcNow,
                    ZoneVisitCharacterDBs = zoneVisitors,
                    ZoneScheduleGroupRecords = new Dictionary<long, List<long>>()
                };

                // Create AcademyLocationDB list (without AccountId field)
                var academyLocationDBs = academyLocations.Select(l => new AcademyLocationDB
                {
                    LocationId = l.LocationId,
                    Rank = l.Rank,
                    Exp = l.Exp
                }).ToList();

                return new AcademyGetInfoResponse
                {
                    ServerTimeTicks = DateTime.Now.Ticks,
                    AcademyDB = academyDB,
                    AcademyLocationDBs = academyLocationDBs
                };
            }
        }
    }
}
