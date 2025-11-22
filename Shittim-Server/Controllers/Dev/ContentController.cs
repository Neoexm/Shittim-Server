#if DEBUG
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shittim.GameMasters;
using Shittim.Services;
using Schale.Data;
using Schale.FlatData;
using BlueArchiveAPI.Services;

namespace Shittim.Controllers.Dev
{
    [ApiController]
    [Route("dev")]
    public class ContentController : ControllerBase
    {
        private readonly IDbContextFactory<SchaleDataContext> contextFactory;
        private readonly ExcelTableService excelTableService;

        public ContentController(IDbContextFactory<SchaleDataContext> _contextFactory, ExcelTableService _excelTableService)
        {
            contextFactory = _contextFactory;
            excelTableService = _excelTableService;
        }

        [Route("content")]
        public ContentResult GameContent()
        {
            var raidData = excelTableService.GetTable<RaidSeasonManageExcelT>();
            var timeAttackDungeonData = excelTableService.GetTable<TimeAttackDungeonSeasonManageExcelT>();
            var TADExcel = excelTableService.GetTable<TimeAttackDungeonExcelT>();
            var eliminateRaidData = excelTableService.GetTable<EliminateRaidSeasonManageExcelT>();
            var multiFloorRaidData = excelTableService.GetTable<MultiFloorRaidSeasonManageExcelT>();

            var html = @"
                <style>
                    .header-container {
                        display: flex;
                        justify-content: space-between;
                        align-items: center;
                        padding: 10px;
                        background-color: #f0f0f0;
                        border-bottom: 1px solid #ccc;
                    }
                    .header-container h1 {
                        margin: 0;
                    }
                    .control-group {
                        display: flex;
                        align-items: center;
                        gap: 10px;
                    }
                    input[type='number'], button {
                        padding: 5px;
                    }
                </style>
                <div class='header-container'>
                    <h1>Game Content Switcher</h1>
                    <div class='control-group'>
                        <button id='disable-button'>Disable Multi Floor Raid</button>
                        <label for='uid-input'>UID:</label>
                        <input type='number' id='uid-input' name='uid'>
                    </div>
                </div>
                <br>
                <table border='1'>
                    <tr>
                        <th>Raid Boss</th>
                        <th>Date</th>
                        <th>Action</th>
                        <th>Time Attack Dungeon Enemy</th>
                        <th>Date</th>
                        <th>Action</th>
                        <th>Eliminate Raid Boss</th>
                        <th>Date</th>
                        <th>Action</th>
                        <th>Multi Floor Raid Boss</th>
                        <th>Date</th>
                        <th>Action</th>
                    </tr>";

            int maxRows = Math.Max(raidData.Count, Math.Max(timeAttackDungeonData.Count, Math.Max(eliminateRaidData.Count, multiFloorRaidData.Count)));

            for (int i = 0; i < maxRows; i++)
            {
                html += "<tr>";

                // Raid Data
                if (i < raidData.Count)
                {
                    var raid = raidData[i];
                    html += $@"
                        <td>[{raid.SeasonId}] {raid.OpenRaidBossGroup.FirstOrDefault() ?? "N/A"}</td>
                        <td>{raid.SeasonStartData}</td>
                        <td><button onclick=""applyContent({raid.SeasonId}, 'raid')"">Apply</button></td>";
                }
                else
                {
                    html += "<td colspan='3'></td>";
                }

                // Time Attack Dungeon Data
                if (i < timeAttackDungeonData.Count)
                {
                    var dungeon = timeAttackDungeonData[i];
                    html += $@"
                        <td>[{dungeon.Id}] {TADExcel.FirstOrDefault(x => x.Id == dungeon.DungeonId).TimeAttackDungeonType}</td>
                        <td>{dungeon.StartDate}</td>
                        <td><button onclick=""applyContent({dungeon.Id}, 'timeattack')"">Apply</button></td>";
                }
                else
                {
                    html += "<td colspan='3'></td>";
                }

                // Eliminate Raid Data
                if (i < eliminateRaidData.Count)
                {
                    var eliminate = eliminateRaidData[i];
                    var groupedBosses = new List<string>
                    {
                        eliminate.OpenRaidBossGroup01,
                        eliminate.OpenRaidBossGroup02,
                        eliminate.OpenRaidBossGroup03
                    }
                    .Where(boss => !string.IsNullOrEmpty(boss))
                    .Select(boss => boss.Split('_'))
                    .GroupBy(parts => parts[0])
                    .Select(group => $"{group.Key} {group.Select(parts => parts[1]).First()} ({string.Join(", ", group.Select(parts => parts[2]))})")
                    .ToList();

                    html += $@"
                        <td>[{eliminate.SeasonId}] {string.Join(", ", groupedBosses)})</td>
                        <td>{eliminate.SeasonStartData}</td>
                        <td><button onclick=""applyContent({eliminate.SeasonId}, 'eliminate')"">Apply</button></td>";
                }
                else
                {
                    html += "<td colspan='3'></td>";
                }

                // Multi Floor Raid Data
                if (i < multiFloorRaidData.Count)
                {
                    var multiFloorRaid = multiFloorRaidData[i];
                    html += $@"
                        <td>[{multiFloorRaid.SeasonId}] {multiFloorRaid.OpenRaidBossGroupId ?? "N/A"}</td>
                        <td>{multiFloorRaid.SeasonStartDate}</td>
                        <td><button onclick=""applyContent({multiFloorRaid.SeasonId}, 'multifloor')"">Apply</button></td>";
                }
                else
                {
                    html += "<td colspan='3'></td>";
                }

                html += "</tr>";
            }

            html += @"
                </table>
                <script>
                    async function postContent(url, data) {
                        try {
                            const response = await fetch(url, {
                                method: 'POST',
                                headers: {
                                    'Content-Type': 'application/json'
                                },
                                body: JSON.stringify(data)
                            });

                            if (response.ok) {
                                const message = await response.text();
                                alert('Success: ' + message);
                            } else {
                                const error = await response.text();
                                alert('Error: ' + error);
                            }
                        } catch (error) {
                            alert('An unexpected error occurred.');
                            console.error('Fetch Error:', error);
                        }
                    }

                    function applyContent(id, type) {
                        const uid = document.getElementById('uid-input').value;
                        if (!uid) {
                            alert('Please enter a UID.');
                            return;
                        }
                        
                        const data = { uid: parseInt(uid), id: id, type: type };
                        postContent('/dev/applycontent', data);
                    }

                    document.getElementById('disable-button').addEventListener('click', function() {
                        const uid = document.getElementById('uid-input').value;
                        if (!uid) {
                            alert('Please enter a UID to disable content.');
                            return;
                        }
                        
                        const data = { uid: parseInt(uid) };
                        postContent('/dev/disable-mfr', data);
                    });
                </script>
            ";

            return new ContentResult
            {
                Content = html,
                ContentType = "text/html",
                StatusCode = 200
            };
        }

        [HttpPost("applycontent")]
        public async Task<IActionResult> ApplyContent([FromBody] ApplyContentRequest request)
        {
            if (request.uid <= 0)
                return BadRequest("UID is required and must be a positive number.");

            using var context = await contextFactory.CreateDbContextAsync();
            var userAccount = context.Accounts.FirstOrDefault(x => x.ServerId == request.uid);

            if (userAccount is null)
                return BadRequest($"User with UID '{request.uid}' not found.");

            switch (request.type.ToLower())
            {
                case "raid":
                    await ContentGM.SetRaidSeason(context, userAccount, excelTableService, request.id);
                    break;
                case "timeattack":
                    await ContentGM.SetTimeAttackDungeonSeason(context, userAccount, request.id);
                    break;
                case "eliminate":
                    await ContentGM.SetEliminateRaidSeason(context, userAccount, excelTableService, request.id);
                    break;
                case "multifloor":
                    await ContentGM.SetMultiFloorRaidSeason(context, userAccount, request.id);
                    break;
                default:
                    return BadRequest("Invalid content type provided.");
            }

            return Ok($"Content type '{request.type}' with ID '{request.id}' applied successfully for UID '{request.uid}'.");
        }

        [HttpPost("disable-mfr")]
        public async Task<IActionResult> DisableMultiFloorRaid([FromBody] DisableRequest request)
        {
            if (request.uid <= 0)
                return BadRequest("UID is required and must be a positive number.");

            using var context = await contextFactory.CreateDbContextAsync();
            var userAccount = context.Accounts.FirstOrDefault(x => x.ServerId == request.uid);

            if (userAccount is null)
                return BadRequest($"User with UID '{request.uid}' not found.");

            userAccount.GameSettings.EnableMultiFloorRaid = false;
            await context.SaveChangesAsync();

            return Ok($"Multi Floor Raid disabled successfully for UID '{request.uid}'.");
        }

        public class ApplyContentRequest
        {
            public long uid { get; set; }
            public int id { get; set; }
            public string type { get; set; }
        }

        public class DisableRequest
        {
            public long uid { get; set; }
        }
    }
}
#endif
