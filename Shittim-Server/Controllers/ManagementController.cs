using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMapper;
using Schale.Crypto;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.Models;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using ServerNotificationFlag = Schale.MX.NetworkProtocol.ServerNotificationFlag;
using WebAPIErrorCode = Schale.MX.NetworkProtocol.WebAPIErrorCode;
using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Shittim_Server.Services;
using Shittim.Commands;
using Shittim.Services.WebClient;

namespace Shittim_Server.Controllers;

// Always-compiled management API consumed by the Shittim Control Center desktop GUI. Kept separate from AdminController so the original endpoints (accounts, currency/set, mail/send, account/{id}/currencies) stay untouched and are reused by the GUI alongside the richer surface below.
[ApiController]
[Route("api/admin")]
[AdminAuth]
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

    public class UploadAccountDataRequest
    {
        public string Name { get; set; } = "";
        public string Content { get; set; } = "";
    }

    // Store a profile in the folder `accountdata load` reads. Takes content rather than a path so it works when the Control Center and the server are on different machines.
    [HttpPost("accountdata/upload")]
    public IActionResult UploadAccountData([FromBody] UploadAccountDataRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "name and content are required" });

        // Reject directory parts rather than stripping them, so the write cannot escape.
        var name = request.Name.Trim();
        if (name != Path.GetFileName(name) || name.Contains(".."))
            return BadRequest(new { error = "name must be a bare file name, without directories" });
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return BadRequest(new { error = "name contains characters that are not valid in a file name" });
        if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "name must be a .json file" });

        try { using var _ = JsonDocument.Parse(request.Content); }
        catch (JsonException e) { return BadRequest(new { error = $"not valid JSON: {e.Message}" }); }

        var dir = AccountDataCommand.accountDataDir;
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, name);
        System.IO.File.WriteAllText(dest, request.Content);

        return Ok(new { success = true, name });
    }

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

            // Full, client-loadable initialization (currencies, default parcels, default characters...).
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

    // SQL identifiers cannot be parameterized, so any name that is interpolated into a statement is required to be a bare identifier first.
    private static bool IsPlainSqlIdentifier(string name)
    {
        return !string.IsNullOrEmpty(name)
            && (char.IsLetter(name[0]) || name[0] == '_')
            && name.All(c => char.IsLetterOrDigit(c) || c == '_');
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

            // Cascade by hand: wipe every child table that carries an AccountServerId column, discovered from the SQLite catalogue so we never miss one.
            var tables = await db.Database
                .SqlQueryRaw<string>(
                    "SELECT m.name AS Value FROM sqlite_master m " +
                    "JOIN pragma_table_info(m.name) p ON 1=1 " +
                    "WHERE m.type='table' AND p.name='AccountServerId'")
                .ToListAsync();

            foreach (var table in tables.Distinct())
            {
                if (!IsPlainSqlIdentifier(table))
                    continue;

                await db.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM \"{table}\" WHERE AccountServerId = {{0}}", request.ServerId);
            }

            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"Accounts\" WHERE ServerId = {0}", request.ServerId);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM \"UserAccounts\" WHERE Uid = {0}", request.ServerId);

            if (Config.Instance.ServerConfiguration.SelectedAccountId == request.ServerId)
            {
                Config.Instance.ServerConfiguration.SelectedAccountId = 0;
                Config.Save();
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    [HttpGet("account/selected")]
    public IActionResult SelectedAccount()
    {
        return Ok(new { selectedAccountId = Config.Instance.ServerConfiguration.SelectedAccountId });
    }

    public class SelectAccountRequest { public long ServerId { get; set; } }

    // Which account the game logs into. Takes effect at the next Account_CheckNexon, no restart needed - GenerateSession reads it live.
    [HttpPost("account/select")]
    public async Task<IActionResult> SelectAccount([FromBody] SelectAccountRequest request)
    {
        if (request.ServerId > 0)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            if (!await db.Accounts.AnyAsync(x => x.ServerId == request.ServerId))
                return NotFound(new { error = "Account not found" });
        }

        Config.Instance.ServerConfiguration.SelectedAccountId = Math.Max(0, request.ServerId);
        Config.Save();
        return Ok(new { success = true, selectedAccountId = Config.Instance.ServerConfiguration.SelectedAccountId });
    }

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

    public class RunCommandRequest
    {
        public long Uid { get; set; }
        public string Command { get; set; } = "";
    }

    // Always-on bridge to the full console command set (give / max / giveall / unlockall / setseason / gacha / ...).
    // Mirrors the DEBUG-only /dev/execute-command but ships in every build so the GUI can drive it.
    [HttpPost("command")]
    public async Task<IActionResult> RunCommand([FromBody] RunCommandRequest request)
    {
        if (request == null || request.Uid <= 0 || string.IsNullOrWhiteSpace(request.Command))
            return BadRequest(new { error = "uid and command are required" });

        var parts = request.Command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = parts.First().TrimStart('/', '!', '.', '*').Split('/').Last();
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

    [HttpGet("events/seasons")]
    public async Task<IActionResult> EventSeasons([FromQuery] long uid = 0)
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

        await using var db = await _dbFactory.CreateDbContextAsync();
        var account = uid > 0 ? await db.Accounts.FirstOrDefaultAsync(a => a.ServerId == uid) : null;

        return Ok(new
        {
            total = total.OrderBy(x => x.seasonId),
            grand = grand.OrderBy(x => x.seasonId),
            drill = drill.OrderBy(x => x.seasonId),
            final = final.OrderBy(x => x.seasonId),
            current = account == null ? null : new
            {
                total = account.ContentInfo.RaidDataInfo.SeasonId,
                grand = account.ContentInfo.EliminateRaidDataInfo.SeasonId,
                drill = account.ContentInfo.TimeAttackDungeonDataInfo.SeasonId,
                final = account.ContentInfo.MultiFloorRaidDataInfo.SeasonId,
            },
        });
    }

    [HttpGet("notice")]
    public IActionResult GetNotice()
    {
        return Ok(new
        {
            flags = (int)ServerNoticeService.Flags,
            gateError = ServerNoticeService.GateError != null ? (int)ServerNoticeService.GateError : 0,
            gateMessage = ServerNoticeService.GateMessage,
            availableFlags = Enum.GetValues<ServerNotificationFlag>()
                .Where(f => f != ServerNotificationFlag.None)
                .Select(f => new { name = f.ToString(), value = (int)f }),
        });
    }

    public class SetNoticeRequest
    {
        public int Flags { get; set; }
        public int GateError { get; set; }
        public string? GateMessage { get; set; }
    }

    [HttpPost("notice")]
    public IActionResult SetNotice([FromBody] SetNoticeRequest request)
    {
        try
        {
            ServerNoticeService.Set(
                (ServerNotificationFlag)request.Flags,
                request.GateError == 0 ? null : (WebAPIErrorCode)request.GateError,
                string.IsNullOrWhiteSpace(request.GateMessage) ? "Server is under maintenance" : request.GateMessage);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    [HttpGet("events/schedule")]
    public IActionResult GetEventSchedule()
    {
        var loc = _excel.GetTable<LocalizeExcelT>()
            .GroupBy(l => l.Key)
            .ToDictionary(g => g.Key, g => g.First().En ?? g.First().Jp ?? g.First().Kr ?? "");

        var etc = LocalizeMap();
        var charNames = _excel.GetTable<CharacterExcelT>().GroupBy(c => c.Id).ToDictionary(g => g.Key, g => ResolveName(etc, g.First().LocalizeEtcId, null));
        var itemNames = _excel.GetTable<ItemExcelT>().GroupBy(i => i.Id).ToDictionary(g => g.Key, g => ResolveName(etc, g.First().LocalizeEtcId, null));
        var bonuses = _excel.GetTable<EventContentCharacterBonusExcelT>().GroupBy(b => b.EventContentId).ToDictionary(g => g.Key, g => g.ToList());
        var currencies = _excel.GetTable<EventContentCurrencyItemExcelT>().GroupBy(c => c.EventContentId).ToDictionary(g => g.Key, g => g.ToList());
        var stageCounts = _excel.GetTable<EventContentStageExcelT>().GroupBy(s => s.EventContentId).ToDictionary(g => g.Key, g => g.Count());

        var events = _excel.GetTable<EventContentSeasonExcelT>()
            .GroupBy(s => s.EventContentId)
            .Select(g =>
            {
                // The Stage row is the one that carries EventDisplay and the banner art; events that never had a stage (world raid entrances, mini events) fall back to whatever row came first.
                var head = g.FirstOrDefault(s => s.EventContentType == EventContentType.Stage) ?? g.First();
                var types = g.Select(s => s.EventContentType.ToString()).Distinct().ToList();

                using var hasher = XXHash32.Create();
                hasher.ComputeHash(Encoding.UTF8.GetBytes(head.Name ?? ""));
                loc.TryGetValue(hasher.HashUInt32, out var localized);

                // A rerun carries none of the original's stages, bonuses or currency of its own, so everything descriptive has to be read off the id it is a rerun of.
                var source = head.OriginalEventContentId > 0 ? head.OriginalEventContentId : g.Key;

                var students = (bonuses.GetValueOrDefault(source) ?? [])
                    .OrderByDescending(b => b.BonusPercentage.Count > 0 ? b.BonusPercentage.Max() : 0)
                    .ThenBy(b => b.CharacterId)
                    .Select(b => charNames.GetValueOrDefault(b.CharacterId))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Take(3)
                    .ToList();

                var currency = (currencies.GetValueOrDefault(source) ?? [])
                    .Select(c => itemNames.GetValueOrDefault(c.ItemUniqueId))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();

                return new
                {
                    id = g.Key,
                    // 21 of these have no LocalizeExcel row, and the mini events all share one Name key, so showing the raw key would label eighteen different events identically.
                    name = string.IsNullOrWhiteSpace(localized) ? $"Event #{g.Key}" : localized,
                    key = head.Name,
                    types,
                    minigames = types.Where(t => t.StartsWith("MiniGame") || t.StartsWith("Minigame")).ToList(),
                    releaseType = head.EventContentReleaseType.ToString(),
                    original = head.OriginalEventContentId,
                    isReturn = g.Any(s => s.IsReturn),
                    // the lobby icon rail draws one entry per season row that is both displayable and not a permanent unlock; the other events are reachable only from a menu
                    rail = g.Any(s => s.EventDisplay && s.EventContentReleaseType == EventContentReleaseType.None),
                    iconOrder = g.Max(s => s.IconOrder),
                    students,
                    currency,
                    stages = stageCounts.GetValueOrDefault(source),
                    open = head.EventContentOpenTime,
                    close = head.EventContentCloseTime,
                    enabled = EventScheduleService.Enabled.Contains(g.Key),
                };
            })
            .OrderBy(e => e.id)
            .ToList();

        return Ok(new { events, enabled = EventScheduleService.Enabled.OrderBy(x => x) });
    }

    public class SetEventScheduleRequest
    {
        public List<long>? Enabled { get; set; }
    }

    [HttpPost("events/schedule")]
    public IActionResult SetEventSchedule([FromBody] SetEventScheduleRequest request)
    {
        try
        {
            var written = EventScheduleService.Set(request.Enabled ?? new List<long>(), _excel);
            return Ok(new { success = true, rows = written, enabled = EventScheduleService.Enabled.OrderBy(x => x) });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    [HttpGet("events/{eventContentId:long}/unlocks")]
    public async Task<IActionResult> EventUnlocks(long eventContentId, [FromQuery] long uid = 0)
    {
        var itemNames = NameMap(_excel.GetTable<ItemExcelT>().GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First().LocalizeEtcId));

        var stages = _excel.GetTable<EventContentStageExcelT>().Where(x => x.EventContentId == eventContentId).ToList();
        var missionIds = MissionIdsFor(eventContentId);
        var shopIds = _excel.GetTable<EventContentShopExcelT>().Where(x => x.EventContentId == eventContentId).Select(x => x.Id).ToHashSet();
        var collectionIds = _excel.GetTable<EventContentCollectionExcelT>().Where(x => x.EventContentId == eventContentId).Select(x => x.Id).ToHashSet();
        var gates = MinigameGatesFor(eventContentId);
        var gateStageIds = gates.Select(x => x.CampaignStageId).Where(x => x != 0).ToHashSet();
        var play = MinigameStagesFor(eventContentId);

        // An event names the same item on more than one row when a token doubles as the shortcut currency, and the popup wants one amount box per item rather than one per row.
        var currency = _excel.GetTable<EventContentCurrencyItemExcelT>()
            .Where(x => x.EventContentId == eventContentId)
            .GroupBy(x => x.ItemUniqueId)
            .Select(g => new { itemId = g.Key, type = g.First().EventContentItemType.ToString(), name = itemNames.GetValueOrDefault(g.Key) })
            .ToList();

        // Handing out a token without saying what a run costs is how you end up with 23 of something the client wants 100 of, and the client blocks that entirely on its side so the server never gets a chance to explain.
        var enterCosts = stages.Where(x => x.StageEnterCostType == ParcelType.Item && x.StageEnterCostAmount > 0).Select(x => (x.StageEnterCostId, Amount: (long)x.StageEnterCostAmount))
            .Concat(_excel.GetTable<MiniGameDefenseStageExcelT>().Where(x => x.EventContentId == eventContentId && x.StageEnterCostType == ParcelType.Item && x.StageEnterCostAmount > 0).Select(x => (x.StageEnterCostId, Amount: (long)x.StageEnterCostAmount)))
            .GroupBy(x => x.StageEnterCostId)
            .ToDictionary(g => g.Key, g => (Min: g.Min(x => x.Amount), Max: g.Max(x => x.Amount)));

        await using var db = await _dbFactory.CreateDbContextAsync();
        var held = uid > 0
            ? await db.Items.Where(x => x.AccountServerId == uid).ToDictionaryAsync(x => x.UniqueId, x => x.StackCount)
            : [];

        var stageIds = stages.Select(x => x.Id).ToHashSet();
        var cleared = uid > 0 ? await db.CampaignStageHistories.CountAsync(x => x.AccountServerId == uid && x.IsClearedEver && stageIds.Contains(x.StageUniqueId)) : 0;
        var done = uid > 0 ? await db.MissionProgresses.CountAsync(x => x.AccountServerId == uid && x.Complete && missionIds.Contains(x.MissionUniqueId)) : 0;
        var bought = uid > 0 ? await db.ShopPurchaseHistories.CountAsync(x => x.AccountServerId == uid && shopIds.Contains(x.ShopUniqueId)) : 0;
        var owned = uid > 0 ? await db.EventContentCollections.CountAsync(x => x.AccountServerId == uid && x.EventContentId == eventContentId) : 0;
        var gatesCleared = uid > 0 && gateStageIds.Count > 0 ? await db.CampaignStageHistories.CountAsync(x => x.AccountServerId == uid && x.IsClearedEver && gateStageIds.Contains(x.StageUniqueId)) : 0;

        var playCleared = 0;
        if (uid > 0 && play.Total > 0)
        {
            var rhythmIds = play.Rhythm.Select(x => x.UniqueId).ToHashSet();
            var shootingIds = play.Shooting.Select(x => x.Id).ToHashSet();
            playCleared = await db.MiniGameDefenseStageHistories.CountAsync(x => x.AccountServerId == uid && x.EventContentId == eventContentId && x.Star1Flag && play.Defense.Contains(x.StageId))
                + await db.MiniGameHistories.CountAsync(x => x.AccountServerId == uid && x.EventContentId == eventContentId && x.HighScore > 0 && rhythmIds.Contains(x.UniqueId))
                + await db.MiniGameShootingHistories.CountAsync(x => x.AccountServerId == uid && x.EventContentId == eventContentId && x.ArriveSection > 0 && shootingIds.Contains(x.UniqueId));
        }

        return Ok(new
        {
            id = eventContentId,
            currency = currency.Select(c => new { c.itemId, c.type, c.name, held = held.GetValueOrDefault(c.itemId), costMin = enterCosts.GetValueOrDefault(c.itemId).Min, costMax = enterCosts.GetValueOrDefault(c.itemId).Max }),
            stages = new { total = stages.Count, cleared, difficulties = stages.GroupBy(x => x.StageDifficulty.ToString()).ToDictionary(g => g.Key, g => g.Count()) },
            missions = new { total = missionIds.Count, done },
            shop = new { total = shopIds.Count, bought },
            collections = new { total = collectionIds.Count, owned },
            minigame = new { total = gateStageIds.Count, cleared = gatesCleared, names = gates.Select(x => x.OpenConditionContentType.ToString()) },
            minigameStages = new { total = play.Total, cleared = playCleared, kinds = play.Kinds },
        });
    }

    public class EventUnlockRequest
    {
        public long AccountServerId { get; set; }
        public long EventContentId { get; set; }
        public bool ClearStages { get; set; }
        public bool CompleteMissions { get; set; }
        public bool ResetShop { get; set; }
        public bool UnlockCollections { get; set; }
        public bool UnlockMinigame { get; set; }
        public bool ClearMinigames { get; set; }
        public Dictionary<long, long>? Currency { get; set; }
    }

    [HttpPost("events/unlock")]
    public async Task<IActionResult> EventUnlock([FromBody] EventUnlockRequest request)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var account = await db.Accounts.FirstOrDefaultAsync(x => x.ServerId == request.AccountServerId);
            if (account == null)
                return NotFound(new { error = "Account not found" });

            var now = account.GameSettings.ServerDateTime();
            int stages = 0, missions = 0, shop = 0, collections = 0, items = 0, minigame = 0, minigameStages = 0;

            if (request.ClearStages)
            {
                var stageExcels = _excel.GetTable<EventContentStageExcelT>().Where(x => x.EventContentId == request.EventContentId).ToList();
                var ids = stageExcels.Select(x => x.Id).ToHashSet();
                var existing = await db.CampaignStageHistories
                    .Where(x => x.AccountServerId == account.ServerId && ids.Contains(x.StageUniqueId))
                    .ToDictionaryAsync(x => x.StageUniqueId);

                foreach (var excel in stageExcels)
                {
                    if (!existing.TryGetValue(excel.Id, out var row))
                    {
                        row = new CampaignStageHistoryDBServer { AccountServerId = account.ServerId, StageUniqueId = excel.Id };
                        db.CampaignStageHistories.Add(row);
                    }
                    else if (row.IsClearedEver)
                    {
                        continue;
                    }

                    row.IsClearedEver = true;
                    row.BestStarRecord = 3;
                    row.Star1Flag = true;
                    row.Star2Flag = true;
                    row.Star3Flag = true;
                    row.LastPlay = now;
                    if (row.ClearTurnRecord == 0) row.ClearTurnRecord = 1;
                    row.FirstClearRewardReceive ??= now;
                    row.StarRewardReceive ??= now;
                    stages++;
                }

                // Reruns carry a permanent record whose all-clear flag gates the character reward, and nothing in the stage-clear path ever sets it.
                var permanent = await db.EventContentPermanents.FirstOrDefaultAsync(
                    x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId);
                if (permanent != null && stageExcels.Count > 0)
                    permanent.IsStageAllClear = true;
            }

            if (request.UnlockMinigame)
            {
                var gateStageIds = MinigameGatesFor(request.EventContentId).Select(x => x.CampaignStageId).Where(x => x != 0).ToHashSet();
                var existing = await db.CampaignStageHistories
                    .Where(x => x.AccountServerId == account.ServerId && gateStageIds.Contains(x.StageUniqueId))
                    .ToDictionaryAsync(x => x.StageUniqueId);

                foreach (var stageId in gateStageIds)
                {
                    if (!existing.TryGetValue(stageId, out var row))
                    {
                        row = new CampaignStageHistoryDBServer { AccountServerId = account.ServerId, StageUniqueId = stageId };
                        db.CampaignStageHistories.Add(row);
                    }
                    else if (row.IsClearedEver)
                    {
                        continue;
                    }

                    row.IsClearedEver = true;
                    row.BestStarRecord = 3;
                    row.Star1Flag = true;
                    row.Star2Flag = true;
                    row.Star3Flag = true;
                    row.LastPlay = now;
                    if (row.ClearTurnRecord == 0) row.ClearTurnRecord = 1;
                    row.FirstClearRewardReceive ??= now;
                    row.StarRewardReceive ??= now;
                    minigame++;
                }
            }

            if (request.ClearMinigames)
            {
                var play = MinigameStagesFor(request.EventContentId);

                var defenseRows = await db.MiniGameDefenseStageHistories
                    .Where(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId)
                    .ToDictionaryAsync(x => x.StageId);

                foreach (var stageId in play.Defense)
                {
                    if (!defenseRows.TryGetValue(stageId, out var row))
                    {
                        row = new MiniGameDefenseStageHistoryDBServer { AccountServerId = account.ServerId, EventContentId = request.EventContentId, StageId = stageId };
                        db.MiniGameDefenseStageHistories.Add(row);
                    }
                    else if (row.Star1Flag && row.Star2Flag && row.Star3Flag)
                    {
                        continue;
                    }

                    row.Star1Flag = true;
                    row.Star2Flag = true;
                    row.Star3Flag = true;
                    row.FirstClearRewardReceive = true;
                    row.StarRewardReceive = true;
                    minigameStages++;
                }

                var rhythmRows = await db.MiniGameHistories
                    .Where(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId)
                    .ToDictionaryAsync(x => x.UniqueId);

                foreach (var chart in play.Rhythm)
                {
                    if (!rhythmRows.TryGetValue(chart.UniqueId, out var row))
                    {
                        row = new MiniGameHistoryDBServer { AccountServerId = account.ServerId, EventContentId = request.EventContentId, UniqueId = chart.UniqueId };
                        db.MiniGameHistories.Add(row);
                    }
                    else if (row.HighScore >= chart.MaxScore)
                    {
                        continue;
                    }

                    row.HighScore = chart.MaxScore;
                    // the later charts open on the event's summed AccumulatedScore rather than on any single clear, and a full-marks record on every chart is what takes the sum past the last OpenStageScoreAmount
                    row.AccumulatedScore = Math.Max(row.AccumulatedScore, chart.MaxScore);
                    row.IsFullCombo = true;
                    row.ClearDate = now;
                    minigameStages++;
                }

                var shootingRows = await db.MiniGameShootingHistories
                    .Where(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId)
                    .ToDictionaryAsync(x => x.UniqueId);

                foreach (var (stageId, sections) in play.Shooting)
                {
                    if (!shootingRows.TryGetValue(stageId, out var row))
                    {
                        row = new MiniGameShootingHistoryDBServer { AccountServerId = account.ServerId, EventContentId = request.EventContentId, UniqueId = stageId };
                        db.MiniGameShootingHistories.Add(row);
                    }
                    else if (row.ArriveSection >= sections)
                    {
                        continue;
                    }

                    // LastUpdateDate is left where it is on purpose - the lobby derives IsClearToday from it, so stamping now would spend today's run on a clear nobody played
                    row.ArriveSection = sections;
                    minigameStages++;
                }
            }

            if (request.CompleteMissions)
            {
                var ids = MissionIdsFor(request.EventContentId);
                var existing = await db.MissionProgresses
                    .Where(x => x.AccountServerId == account.ServerId && ids.Contains(x.MissionUniqueId))
                    .ToDictionaryAsync(x => x.MissionUniqueId);
                var conditions = _excel.GetTable<EventContentMissionExcelT>().Where(x => ids.Contains(x.Id))
                    .ToDictionary(x => x.Id, x => (x.CompleteConditionParameter, x.CompleteConditionCount));
                foreach (var m in _excel.GetTable<MiniGameMissionExcelT>().Where(x => ids.Contains(x.Id)))
                    conditions[m.Id] = (m.CompleteConditionParameter, m.CompleteConditionCount);

                foreach (var id in ids)
                {
                    var cond = conditions[id];
                    var progress = ForcedProgress(cond.CompleteConditionParameter, cond.CompleteConditionCount, request.EventContentId);
                    if (existing.TryGetValue(id, out var row))
                    {
                        if (row.Complete && row.ProgressParameters?.Count > 0) continue;
                        row.Complete = true;
                        row.ProgressParameters = progress;
                    }
                    else
                    {
                        db.MissionProgresses.Add(new MissionProgressDBServer
                        {
                            AccountServerId = account.ServerId,
                            MissionUniqueId = id,
                            Complete = true,
                            StartTime = now,
                            ProgressParameters = progress
                        });
                    }
                    missions++;
                }
            }

            if (request.ResetShop)
            {
                var ids = _excel.GetTable<EventContentShopExcelT>().Where(x => x.EventContentId == request.EventContentId).Select(x => x.Id).ToHashSet();
                var rows = await db.ShopPurchaseHistories
                    .Where(x => x.AccountServerId == account.ServerId && ids.Contains(x.ShopUniqueId))
                    .ToListAsync();
                shop = rows.Count;
                db.ShopPurchaseHistories.RemoveRange(rows);
            }

            if (request.UnlockCollections)
            {
                var owned = await db.EventContentCollections
                    .Where(x => x.AccountServerId == account.ServerId && x.EventContentId == request.EventContentId)
                    .Select(x => x.UniqueId)
                    .ToListAsync();
                var have = owned.ToHashSet();

                foreach (var excel in _excel.GetTable<EventContentCollectionExcelT>().Where(x => x.EventContentId == request.EventContentId && !have.Contains(x.Id)))
                {
                    db.EventContentCollections.Add(new EventContentCollectionDBServer
                    {
                        AccountServerId = account.ServerId,
                        EventContentId = request.EventContentId,
                        GroupId = excel.GroupId,
                        UniqueId = excel.Id,
                        ReceiveDate = now
                    });
                    collections++;
                }
            }

            foreach (var (itemId, amount) in request.Currency ?? [])
            {
                if (amount <= 0) continue;
                var row = await db.Items.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId && x.UniqueId == itemId);
                if (row != null)
                    row.StackCount += amount;
                else
                    db.Items.Add(new ItemDBServer { AccountServerId = account.ServerId, UniqueId = itemId, StackCount = amount });
                items++;
            }

            await db.SaveChangesAsync();
            return Ok(new { success = true, stages, missions, shop, collections, items, minigame, minigameStages });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.GetBaseException().Message });
        }
    }

    // Event missions and minigame missions are separate tables that both feed MissionProgress, and an event with a minigame has rows in both.
    private HashSet<long> MissionIdsFor(long eventContentId) =>
        _excel.GetTable<EventContentMissionExcelT>().Where(x => x.EventContentId == eventContentId).Select(x => x.Id)
            .Concat(_excel.GetTable<MiniGameMissionExcelT>().Where(x => x.EventContentId == eventContentId).Select(x => x.Id))
            .ToHashSet();

    // The client reads a mission's count out of ProgressParameters by the parameter it matched on, so a forced complete has to name the same key the play path would have picked - CompleteConditionParameter leads with the event id and then the subject, and a list that does not lead with the event id is the subject set itself and shares one bucket.
    private static Dictionary<long, long> ForcedProgress(List<long> declared, long count, long eventContentId)
    {
        declared ??= [];
        long key;
        if (declared.Count == 0)
            key = 0;
        else if (declared.Count == 1)
            key = declared[0];
        else
            key = declared[0] == eventContentId ? declared[1] : 0;

        return new Dictionary<long, long> { [key] = count };
    }

    // The minigame padlock is an OpenConditionExcel row keyed on the minigame's own content type, and its stage belongs to the rerun twin rather than to the event you turned on - 838's defense minigame wants 108381311, which only exists under 10838.
    private List<OpenConditionExcelT> MinigameGatesFor(long eventContentId)
    {
        var seasons = _excel.GetTable<EventContentSeasonExcelT>();
        var origin = seasons.Where(x => x.EventContentId == eventContentId).Select(x => x.OriginalEventContentId).FirstOrDefault(x => x != 0);
        if (origin == 0)
            origin = eventContentId;

        var family = seasons.Where(x => x.EventContentId == origin || x.OriginalEventContentId == origin).Select(x => x.EventContentId).ToHashSet();
        return _excel.GetTable<OpenConditionExcelT>().Where(x => family.Contains(x.ShortcutParam)).ToList();
    }

    // Only the three minigames that keep a per-stage record are here. TBG, CCG, DreamMaker and the road puzzle save a run in progress - a board, a deck, an ending - with no cleared flag to force, so there is nothing for a forced clear to write.
    private (List<long> Defense, List<MiniGameRhythmExcelT> Rhythm, List<(long Id, long Sections)> Shooting, int Total, List<string> Kinds) MinigameStagesFor(long eventContentId)
    {
        var defense = _excel.GetTable<MiniGameDefenseStageExcelT>().Where(x => x.EventContentId == eventContentId).Select(x => x.Id).ToList();

        var bgm = _excel.GetTable<MiniGameRhythmBgmExcelT>().ToDictionary(x => x.RhythmBgmId, x => x.EventContentId);
        var rhythm = _excel.GetTable<MiniGameRhythmExcelT>().Where(x => bgm.GetValueOrDefault(x.RhythmBgmId) == eventContentId).ToList();

        // Shooting stages carry no event column - the three of them are named by ConstMiniGameShooting and belong to whichever event carries the shooting missions.
        var shooting = new List<(long, long)>();
        var shootingConst = _excel.GetTable<ConstMiniGameShootingExcelT>().FirstOrDefault();
        if (shootingConst != null && _excel.GetTable<MiniGameMissionExcelT>().Any(x => x.EventContentId == eventContentId && x.CompleteConditionType == MissionCompleteConditionType.Reset_ClearCountShooting))
        {
            shooting.Add((shootingConst.NormalStageId, shootingConst.NormalSectionCount));
            shooting.Add((shootingConst.HardStageId, shootingConst.HardSectionCount));
            shooting.Add((shootingConst.FreeStageId, shootingConst.FreeSectionCount));
        }

        var kinds = new List<string>();
        if (defense.Count > 0) kinds.Add("Defense");
        if (rhythm.Count > 0) kinds.Add("Rhythm");
        if (shooting.Count > 0) kinds.Add("Shooting");

        return (defense, rhythm, shooting, defense.Count + rhythm.Count + shooting.Count, kinds);
    }

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
