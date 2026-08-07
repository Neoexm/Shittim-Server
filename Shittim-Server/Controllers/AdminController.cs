using Microsoft.AspNetCore.Mvc;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.Parcel;
using Shittim_Server.Services;
using Shittim.GameMasters;
using AutoMapper;

namespace Shittim_Server.Controllers;

[ApiController]
[Route("api/admin")]
[AdminAuth]
public class AdminController : ControllerBase
{
    private readonly SchaleDataContext _context;
    private readonly MailManager _mailManager;
    private readonly IMapper _mapper;

    public AdminController(
        SchaleDataContext context,
        MailManager mailManager,
        IMapper mapper)
    {
        _context = context;
        _mailManager = mailManager;
        _mapper = mapper;
    }

    [HttpPost("mail/send")]
    public async Task<IActionResult> SendMail([FromBody] SendMailRequest request)
    {
        try
        {
            var account = _context.Accounts.FirstOrDefault(a => a.ServerId == request.AccountServerId);
            if (account == null)
                return NotFound(new { error = "Account not found" });

            var parcels = request.Parcels.Select(p => (
                type: Enum.Parse<ParcelType>(p.Type),
                id: p.Id,
                amount: p.Amount
            )).ToList();

            await _mailManager.SendSystemMailMultipleParcels(
                account,
                request.Sender ?? "Plana",
                request.Comment,
                parcels,
                request.ExpireDate
            );
            
            return Ok(new { success = true, message = "Mail sent successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("currency/set")]
    public async Task<IActionResult> SetCurrency([FromBody] SetCurrencyRequest request)
    {
        try
        {
            var currencies = _context.Currencies.FirstOrDefault(c => c.AccountServerId == request.AccountServerId);
            if (currencies == null)
                return NotFound(new { error = "Account currencies not found" });

            var account = _context.Accounts.FirstOrDefault(a => a.ServerId == request.AccountServerId);
            if (account == null)
                return NotFound(new { error = "Account not found" });

            var currencyType = (CurrencyTypes)request.CurrencyType;
            var serverNow = account.GameSettings.ServerDateTime();

            // Gem is derived (Gem = GemBonus + GemPaid, recomputed by UpdateGem on every parcel update), so a direct Gem write would be discarded; set the sources.
            if (currencyType == CurrencyTypes.Gem)
            {
                currencies.CurrencyDict[CurrencyTypes.GemBonus] = request.Amount;
                currencies.CurrencyDict[CurrencyTypes.GemPaid] = 0;
                currencies.UpdateTimeDict[CurrencyTypes.GemBonus] = serverNow;
                currencies.UpdateTimeDict[CurrencyTypes.GemPaid] = serverNow;
            }
            currencies.CurrencyDict[currencyType] = request.Amount;

            // Must be the account's own clock, not the wall clock: AP regeneration is computed from the delta between now and this timestamp,
            // so stamping a ForceDateTime account with real time hands it a bogus amount of regenerated AP on the next read.
            currencies.UpdateTimeDict[currencyType] = serverNow;
            
            await _context.SaveChangesAsync();
            
            return Ok(new { success = true, message = $"Currency {currencyType} set to {request.Amount}" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("accounts")]
    public IActionResult GetAccounts()
    {
        try
        {
            // AI clients (the Schale assist bot) carry a DevId; they are server-owned and stay out of the roster.
            var accounts = _context.Accounts
                .Where(a => a.DevId == null)
                .Select(a => new
                {
                    a.ServerId,
                    a.Nickname,
                    a.Level,
                    a.Exp,
                    a.Comment
                })
                .ToList();
            
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("account/{serverId}/currencies")]
    public IActionResult GetAccountCurrencies(long serverId)
    {
        try
        {
            var currencies = _context.Currencies.FirstOrDefault(c => c.AccountServerId == serverId);
            if (currencies == null)
                return NotFound(new { error = "Currencies not found" });
            
            return Ok(currencies.CurrencyDict);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class AddCharacterRequest
{
    public long AccountServerId { get; set; }
    public long CharacterId { get; set; }
    public string? Quality { get; set; }
}

public class RemoveCharacterRequest
{
    public long AccountServerId { get; set; }
    public long CharacterId { get; set; }
}

public class SendMailRequest
{
    public long AccountServerId { get; set; }
    public string? Sender { get; set; }
    public string Comment { get; set; } = "";
    public List<ParcelRequest> Parcels { get; set; } = new();
    public DateTime? ExpireDate { get; set; }
}

public class ParcelRequest
{
    public string Type { get; set; } = "";
    public long Id { get; set; }
    public long Amount { get; set; }
}

public class SetCurrencyRequest
{
    public long AccountServerId { get; set; }
    public long CurrencyType { get; set; }
    public long Amount { get; set; }
}
