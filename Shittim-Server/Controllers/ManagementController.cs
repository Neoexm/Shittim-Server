using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.Models;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Shittim_Server.Services;
using Shittim.Commands;
using Shittim.Services.WebClient;

namespace Shittim_Server.Controllers;

// Always-compiled management API consumed by the Shittim Control Center desktop GUI.
// Deliberately kept separate from AdminController so the original endpoints
// (accounts, currency/set, mail/send, account/{id}/currencies) stay untouched and
// are reused by the GUI alongside the richer surface below.
[ApiController]
[Route("api/admin")]
public class ManagementController : ControllerBase
{
    private static readonly DateTime ProcessStart = Process.GetCurrentProcess().StartTime;

    private readonly IDbContextFactory<SchaleDataContext> _dbFactory;
    private readonly MailManager _mailManager;
    private readonly ExcelTableService _excel;
    private readonly WebService _webService;
    private readonly IMapper _mapper;

    public ManagementController(
        IDbContextFactory<SchaleDataContext> dbFactory,
        MailManager mailManager,
        ExcelTableService excel,
        WebService webService,
        IMapper mapper)
    {
        _dbFactory = dbFactory;
        _mailManager = mailManager;
        _excel = excel;
        _webService = webService;
        _mapper = mapper;
    }

    // ----------------------------------------------------------------- status

    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var cfg = Config.Instance.ServerConfiguration;
        var accountCount = await db.Accounts.CountAsync();

        return Ok(new
        {
            service = "Shittim-Server",
            gameVersion = cfg.GameVersion.ToString(),
            versionId = cfg.VersionId,
            apiPort = cfg.HostPort,
            gatewayPort = cfg.GatewayPort,
            gatewayEnabled = cfg.EnableGateway,
            useEncryption = cfg.UseEncryption,
            bypassAuthentication = cfg.BypassAuthentication,
            useCustomExcel = cfg.UseCustomExcel,
            accountCount,
            startedAtUtc = ProcessStart.ToUniversalTime(),
            uptimeSeconds = (long)(DateTime.Now - ProcessStart).TotalSeconds,
        });
    }

    // --------------------------------------------------------------- accounts

    [HttpGet("account/{serverId:long}/detail")]
    public async Task<IActionResult> AccountDetail(long serverId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var a = await db.Accounts.FirstOrDefaultAsync(x => x.ServerId == serverId);
        if (a == null) return NotFound(new { error = "Account not found" });

        var currencies = await db.Currencies.FirstOrDefaultAsync(c => c.AccountServerId == serverId);
        var itemCount = await db.Items.CountAsync(x => x.AccountServerId == serverId);
        var characterCount = await db.Characters.CountAsync(x => x.AccountServerId == serverId);
        var mailCount = await db.Mails.CountAsync(x => x.AccountServerId == serverId);

        return Ok(new
        {
            a.ServerId,
            a.Nickname,
            a.CallName,
            a.Level,
            a.Exp,
            a.Comment,
            State = a.State.ToString(),
            a.VIPLevel,
            a.PublisherAccountId,
            a.RepresentCharacterServerId,
            CreateDate = a.CreateDate,
            LastConnectTime = a.LastConnectTime,
            currencies = currencies?.CurrencyDict ?? new Dictionary<CurrencyTypes, long>(),
            itemCount,
            characterCount,
            mailCount,
        });
    }

    public class CreateAccountRequest
    {
        public string Nickname { get; set; } = "Sensei";
        public string? CallName { get; set; }
    }

    [HttpPost("account/create")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // Synthesize a unique publisher id the same way the login flow expects one.
            long publisherId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            while (await db.Accounts.AnyAsync(x => x.PublisherAccountId == publisherId))
                publisherId++;

            db.UserAccounts.Add(new UserAccount { Uid = -1, NpSN = publisherId, NpToken = "" });

            var account = new AccountDBServer(publisherId)
            {
                Nickname = string.IsNullOrWhiteSpace(request.Nickname) ? "Sensei" : request.Nickname.Trim(),
                CallName = string.IsNullOrWhiteSpace(request.CallName) ? request.Nickname?.Trim() : request.CallName.Trim(),
            };
            db.Accounts.Add(account);
            await db.SaveChangesAsync();

            account = await db.Accounts.FirstAsync(x => x.PublisherAccountId == publisherId);
            var user = await db.UserAccounts.FirstAsync(u => u.NpSN == publisherId);
            user.Uid = account.ServerId;

            // Full, client-loadable initialization (currencies, default parcels, default characters…).
            await AccountInitializationService.InitializeCompleteAccount(db, account);
            await db.SaveChangesAsync();

            return Ok(new { success = true, serverId = account.ServerId, nickname = account.Nickname });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    public class UpdateAccountRequest
    {
        public long ServerId { get; set; }
        public string? Nickname { get; set; }
        public string? CallName { get; set; }
        public string? Comment { get; set; }
        public int? Level { get; set; }
        public long? Exp { get; set; }
        public int? VIPLevel { get; set; }
    }

    [HttpPost("account/update")]
    public async Task<IActionResult> UpdateAccount([FromBody] UpdateAccountRequest request)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var a = await db.Accounts.FirstOrDefaultAsync(x => x.ServerId == request.ServerId);
            if (a == null) return NotFound(new { error = "Account not found" });

            if (!string.IsNullOrWhiteSpace(request.Nickname)) a.Nickname = request.Nickname.Trim();
            if (request.CallName != null) a.CallName = request.CallName.Trim();
            if (request.Comment != null) a.Comment = request.Comment;
            if (request.Level.HasValue) a.Level = request.Level.Value;
            if (request.Exp.HasValue) a.Exp = request.Exp.Value;
            if (request.VIPLevel.HasValue) a.VIPLevel = request.VIPLevel.Value;

            await db.SaveChangesAsync();
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    public class DeleteAccountRequest { public long ServerId { get; set; } }

    [HttpPost("account/delete")]
    public async Task<IActionResult> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            if (!await db.Accounts.AnyAsync(x => x.ServerId == request.ServerId))
                return NotFound(new { error = "Account not found" });

            // Cascade by hand: wipe every child table that carries an AccountServerId column,
            // discovered from the SQLite catalogue so we never miss one.
            var tables = await db.Database
                .SqlQueryRaw<string>(
                    "SELECT m.name AS Value FROM sqlite_master m " +
                    "JOIN pragma_table_info(m.name) p ON 1=1 " +
                    "WHERE m.type='table' AND p.name='AccountServerId'")
                .ToListAsync();

            foreach (var table in tables.Distinct())
                await db.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table}\" WHERE AccountServerId = {request.ServerId}");

            await db.Database.ExecuteSqlRawAsync($"DELETE FROM \"Accounts\" WHERE ServerId = {request.ServerId}");
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM \"UserAccounts\" WHERE Uid = {request.ServerId}");

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    // -------------------------------------------------------------- inventory

    [HttpGet("account/{serverId:long}/items")]
    public async Task<IActionResult> AccountItems(long serverId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Items.Where(x => x.AccountServerId == serverId).ToListAsync();
        var names = NameMap(_excel.GetTable<ItemExcelT>().ToDictionary(x => x.Id, x => x.LocalizeEtcId));
        var icons = _excel.GetTable<ItemExcelT>().ToDictionary(x => x.Id, x => x.Icon);

        return Ok(rows.Select(r => new
        {
            r.ServerId,
            r.UniqueId,
            r.StackCount,
            name = names.TryGetValue(r.UniqueId, out var n) ? n : $"Item {r.UniqueId}",
            icon = icons.TryGetValue(r.UniqueId, out var ic) ? ic : null,
        }));
    }

    public class GiveItemRequest
    {
        public long AccountServerId { get; set; }
        public long UniqueId { get; set; }
        public long Amount { get; set; } = 1;
        public bool SetExact { get; set; } = false;
    }

    [HttpPost("items/give")]
    public async Task<IActionResult> GiveItem([FromBody] GiveItemRequest request)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            if (!await db.Accounts.AnyAsync(x => x.ServerId == request.AccountServerId))
                return NotFound(new { error = "Account not found" });

            var existing = await db.Items.FirstOrDefaultAsync(
                x => x.AccountServerId == request.AccountServerId && x.UniqueId == request.UniqueId);

            if (existing != null)
                existing.StackCount = request.SetExact ? request.Amount : existing.StackCount + request.Amount;
            else
                db.Items.Add(new ItemDBServer
                {
                    AccountServerId = request.AccountServerId,
                    UniqueId = request.UniqueId,
                    StackCount = request.Amount,
                });

            await db.SaveChangesAsync();
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    public class RemoveItemRequest
    {
        public long AccountServerId { get; set; }
        public long UniqueId { get; set; }
    }

    [HttpPost("items/remove")]
    public async Task<IActionResult> RemoveItem([FromBody] RemoveItemRequest request)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var rows = db.Items.Where(x => x.AccountServerId == request.AccountServerId && x.UniqueId == request.UniqueId);
            db.Items.RemoveRange(rows);
            await db.SaveChangesAsync();
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    [HttpGet("account/{serverId:long}/characters")]
    public async Task<IActionResult> AccountCharacters(long serverId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Characters.Where(x => x.AccountServerId == serverId).ToListAsync();
        var chars = _excel.GetTable<CharacterExcelT>();
        var names = NameMap(chars.ToDictionary(x => x.Id, x => x.LocalizeEtcId));
        var dev = chars.ToDictionary(x => x.Id, x => x.DevName);

        return Ok(rows.Select(r => new
        {
            r.ServerId,
            r.UniqueId,
            r.StarGrade,
            r.Level,
            r.FavorRank,
            devName = dev.TryGetValue(r.UniqueId, out var dn) ? dn : null,
            name = names.TryGetValue(r.UniqueId, out var n) && !string.IsNullOrWhiteSpace(n)
                ? n
                : (dev.TryGetValue(r.UniqueId, out var d) ? d : $"Character {r.UniqueId}"),
        }));
    }

    // ------------------------------------------------------------------- mail

    [HttpGet("account/{serverId:long}/mails")]
    public async Task<IActionResult> AccountMails(long serverId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var rows = await db.Mails.Where(x => x.AccountServerId == serverId)
            .OrderByDescending(x => x.SendDate).ToListAsync();

        return Ok(rows.Select(m => new
        {
            m.ServerId,
            m.Sender,
            m.Comment,
            Type = m.Type.ToString(),
            m.SendDate,
            m.ReceiptDate,
            m.ExpireDate,
            collected = m.ReceiptDate != null,
            parcels = (m.ParcelInfos ?? new List<ParcelInfo>()).Select(p => new
            {
                type = p.Key.Type.ToString(),
                id = p.Key.Id,
                amount = p.Amount,
            }),
        }));
    }

    public class DeleteMailRequest
    {
        public long AccountServerId { get; set; }
        public long? MailServerId { get; set; }
        public bool ClearAll { get; set; } = false;
    }

    [HttpPost("mail/delete")]
    public async Task<IActionResult> DeleteMail([FromBody] DeleteMailRequest request)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            IQueryable<MailDBServer> query = db.Mails.Where(x => x.AccountServerId == request.AccountServerId);
            if (!request.ClearAll && request.MailServerId.HasValue)
                query = query.Where(x => x.ServerId == request.MailServerId.Value);

            db.Mails.RemoveRange(query);
            var n = await db.SaveChangesAsync();
            return Ok(new { success = true, deleted = n });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    // -------------------------------------------------------- command runner

    public class RunCommandRequest
    {
        public long Uid { get; set; }
        public string Command { get; set; } = "";
    }

    // Always-on bridge to the full console command set (give / max / giveall /
    // unlockall / setseason / gacha / …). Mirrors the DEBUG-only /dev/execute-command
    // but ships in every build so the GUI can drive it.
    [HttpPost("command")]
    public async Task<IActionResult> RunCommand([FromBody] RunCommandRequest request)
    {
        if (request == null || request.Uid <= 0 || string.IsNullOrWhiteSpace(request.Command))
            return BadRequest(new { error = "uid and command are required" });

        var parts = request.Command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = parts.First().TrimStart('/').Split('/').Last();
        var args = parts.Skip(1).ToArray();

        try
        {
            using var memory = new MemoryStream();
            await using var writer = new StreamWriter(memory) { AutoFlush = true };
            var connection = _webService.GetClient(request.Uid, writer);

            Command? cmd;
            try
            {
                cmd = CommandFactory.CreateCommand(name, connection, args);
            }
            catch (ArgumentException ave)
            {
                return BadRequest(new { error = ave.Message });
            }

            if (cmd == null)
                return BadRequest(new { error = $"Unknown command: {name}" });

            await cmd.Execute();

            memory.Position = 0;
            using var reader = new StreamReader(memory);
            var output = await reader.ReadToEndAsync();

            return Ok(new { success = true, output = string.IsNullOrWhiteSpace(output) ? $"'{name}' executed." : output });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    // ------------------------------------------------------- static pickers

    [HttpGet("static/items")]
    public IActionResult StaticItems([FromQuery] string? search = null, [FromQuery] int limit = 300)
    {
        var loc = LocalizeMap();
        var query = _excel.GetTable<ItemExcelT>().AsEnumerable();
        var results = query.Select(x => new
        {
            id = x.Id,
            name = ResolveName(loc, x.LocalizeEtcId, x.Icon),
            icon = x.Icon,
            quality = x.Quality,
            stackMax = x.StackableMax,
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            results = results.Where(x =>
                x.name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (x.icon?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.id.ToString().Contains(s));
        }

        return Ok(results.Take(Math.Clamp(limit, 1, 2000)));
    }

    [HttpGet("static/characters")]
    public IActionResult StaticCharacters([FromQuery] string? search = null, [FromQuery] int limit = 500)
    {
        var loc = LocalizeMap();
        var results = _excel.GetTable<CharacterExcelT>()
            .Where(x => x.IsPlayable && x.IsPlayableCharacter && !x.IsNPC && !x.IsDummy)
            .Select(x => new
            {
                id = x.Id,
                name = ResolveName(loc, x.LocalizeEtcId, x.DevName),
                devName = x.DevName,
                defaultStar = x.DefaultStarGrade,
                maxStar = x.MaxStarGrade,
            });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            results = results.Where(x =>
                x.name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (x.devName?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.id.ToString().Contains(s));
        }

        return Ok(results.Take(Math.Clamp(limit, 1, 2000)));
    }

    [HttpGet("static/equipment")]
    public IActionResult StaticEquipment([FromQuery] string? search = null, [FromQuery] int limit = 500)
    {
        var loc = LocalizeMap();
        var results = _excel.GetTable<EquipmentExcelT>().Select(x => new
        {
            id = x.Id,
            name = ResolveName(loc, x.LocalizeEtcId, x.Icon),
            icon = x.Icon,
            tier = x.TierInit,
            maxLevel = x.MaxLevel,
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            results = results.Where(x =>
                x.name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (x.icon?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                x.id.ToString().Contains(s));
        }

        return Ok(results.Take(Math.Clamp(limit, 1, 2000)));
    }

    [HttpGet("static/currencies")]
    public IActionResult StaticCurrencies()
    {
        var results = Enum.GetValues<CurrencyTypes>()
            .Where(c => c != CurrencyTypes.Invalid && c != CurrencyTypes.Max)
            .Select(c => new { id = (long)c, name = c.ToString() });
        return Ok(results);
    }

    [HttpGet("meta/parceltypes")]
    public IActionResult ParcelTypes()
    {
        var results = Enum.GetValues<ParcelType>()
            .Where(p => p != ParcelType.None)
            .Select(p => new { id = (int)p, name = p.ToString() });
        return Ok(results);
    }

    // ------------------------------------------------------------------ gacha

    private static string GachaConfigPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "gacha_config.json"));

    private class GachaConfigFile
    {
        public Dictionary<string, double>? custom_rates { get; set; }
        public long? guaranteed_character { get; set; }
    }

    [HttpGet("gacha/config")]
    public IActionResult GetGachaConfig()
    {
        double ssr = 0, sr = 0, r = 0;
        long? guaranteed = null;
        var path = GachaConfigPath;
        var exists = System.IO.File.Exists(path);

        if (exists)
        {
            try
            {
                var cfg = JsonSerializer.Deserialize<GachaConfigFile>(System.IO.File.ReadAllText(path));
                if (cfg?.custom_rates != null)
                {
                    cfg.custom_rates.TryGetValue("ssr", out ssr);
                    cfg.custom_rates.TryGetValue("sr", out sr);
                    cfg.custom_rates.TryGetValue("r", out r);
                }
                guaranteed = cfg?.guaranteed_character;
            }
            catch { /* fall through to defaults */ }
        }

        return Ok(new { path, exists, ssr, sr, r, guaranteed });
    }

    public class SetGachaConfigRequest
    {
        public double Ssr { get; set; }
        public double Sr { get; set; }
        public double R { get; set; }
        public long? Guaranteed { get; set; }
        public bool ClearRates { get; set; } = false;
    }

    [HttpPost("gacha/config")]
    public IActionResult SetGachaConfig([FromBody] SetGachaConfigRequest request)
    {
        try
        {
            var cfg = new GachaConfigFile
            {
                custom_rates = request.ClearRates
                    ? null
                    : new Dictionary<string, double> { ["ssr"] = request.Ssr, ["sr"] = request.Sr, ["r"] = request.R },
                guaranteed_character = request.Guaranteed.HasValue && request.Guaranteed.Value > 0
                    ? request.Guaranteed
                    : null,
            };

            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(GachaConfigPath, json);
            return Ok(new { success = true, path = GachaConfigPath });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    [HttpGet("gacha/banners")]
    public IActionResult GachaBanners()
    {
        var loc = LocalizeMap();
        var charNames = _excel.GetTable<CharacterExcelT>()
            .ToDictionary(x => x.Id, x => ResolveName(loc, x.LocalizeEtcId, x.DevName));

        var banners = _excel.GetTable<ShopRecruitExcelT>().Select(b => new
        {
            id = b.Id,
            displayOrder = b.DisplayOrder,
            bannerPath = b.GachaBannerPath,
            saleFrom = b.SalePeriodFrom,
            saleTo = b.SalePeriodTo,
            isNewbie = b.IsNewbie,
            isSelect = b.IsSelectRecruit,
            recruitCoinId = b.RecruitCoinId,
            featured = (b.InfoCharacterId ?? new List<long>())
                .Select(id => new { id, name = charNames.TryGetValue(id, out var n) ? n : $"Character {id}" }),
        }).OrderBy(b => b.displayOrder);

        return Ok(banners);
    }

    // ----------------------------------------------------------------- events

    [HttpGet("events/seasons")]
    public IActionResult EventSeasons()
    {
        var total = _excel.GetTable<RaidSeasonManageExcelT>().Select(s => new
        {
            type = "total",
            seasonId = s.SeasonId,
            start = s.SeasonStartData,
            end = s.SeasonEndData,
            settlement = s.SettlementEndDate,
            boss = string.Join(", ", s.OpenRaidBossGroup ?? new List<string>()),
        });

        var grand = _excel.GetTable<EliminateRaidSeasonManageExcelT>().Select(s => new
        {
            type = "grand",
            seasonId = s.SeasonId,
            start = s.SeasonStartData,
            end = s.SeasonEndData,
            settlement = s.SettlementEndDate,
            boss = string.Join(" / ", new[] { s.OpenRaidBossGroup01, s.OpenRaidBossGroup02, s.OpenRaidBossGroup03 }
                .Where(x => !string.IsNullOrWhiteSpace(x))),
        });

        var drill = _excel.GetTable<TimeAttackDungeonSeasonManageExcelT>().Select(s => new
        {
            type = "drill",
            seasonId = s.Id,
            start = s.StartDate,
            end = s.EndDate,
            settlement = (string?)null,
            boss = $"Dungeon {s.DungeonId}",
        });

        var final = _excel.GetTable<MultiFloorRaidSeasonManageExcelT>().Select(s => new
        {
            type = "final",
            seasonId = s.SeasonId,
            start = s.SeasonStartDate,
            end = s.SeasonEndDate,
            settlement = s.SettlementEndDate,
            boss = s.OpenRaidBossGroupId,
        });

        return Ok(new
        {
            total = total.OrderBy(x => x.seasonId),
            grand = grand.OrderBy(x => x.seasonId),
            drill = drill.OrderBy(x => x.seasonId),
            final = final.OrderBy(x => x.seasonId),
        });
    }

    // ----------------------------------------------------------------- helpers

    private Dictionary<uint, string> LocalizeMap() =>
        _excel.GetTable<LocalizeEtcExcelT>()
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First().NameEn ?? g.First().NameJp ?? g.First().NameKr ?? "");

    private static string ResolveName(Dictionary<uint, string> loc, uint localizeId, string? fallback)
    {
        if (loc.TryGetValue(localizeId, out var n) && !string.IsNullOrWhiteSpace(n))
            return n;
        return string.IsNullOrWhiteSpace(fallback) ? $"#{localizeId}" : fallback;
    }

    private Dictionary<long, string> NameMap(Dictionary<long, uint> idToLocalize)
    {
        var loc = LocalizeMap();
        return idToLocalize.ToDictionary(
            kv => kv.Key,
            kv => loc.TryGetValue(kv.Value, out var n) ? n : "");
    }
}
