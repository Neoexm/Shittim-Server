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

    // [HttpPost("character/add")]
    // public async Task<IActionResult> AddCharacter([FromBody] AddCharacterRequest request)
    // {
    //     try
    //     {
    //         var account = _context.Accounts.FirstOrDefault(a => a.ServerId == request.AccountServerId);
    //         if (account == null)
    //             return NotFound(new { error = "Account not found" });

    //         // TODO: Implement with new static CharacterGM
    //         // await _characterGM.AddCharacter(account, request.CharacterId, request.Quality ?? "");
            
    //         return Ok(new { success = true, message = $"Character {request.CharacterId} added with quality {request.Quality ?? "default"}" });
    //     }
    //     catch (Exception ex)
    //     {
    //         return BadRequest(new { error = ex.Message });
    //     }
    // }

    // [HttpPost("character/remove")]
    // public async Task<IActionResult> RemoveCharacter([FromBody] RemoveCharacterRequest request)
    // {
    //     try
    //     {
    //         var account = _context.Accounts.FirstOrDefault(a => a.ServerId == request.AccountServerId);
    //         if (account == null)
    //             return NotFound(new { error = "Account not found" });

    //         // TODO: Implement with new static CharacterGM
    //         // await _characterGM.RemoveCharacter(account, request.CharacterId);
            
    //         return Ok(new { success = true, message = $"Character {request.CharacterId} removed" });
    //     }
    //     catch (Exception ex)
    //     {
    //         return BadRequest(new { error = ex.Message });
    //     }
    // }

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

            var currencyType = (CurrencyTypes)request.CurrencyType;
            currencies.CurrencyDict[currencyType] = request.Amount;
            currencies.UpdateTimeDict[currencyType] = DateTime.Now;
            
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
            var accounts = _context.Accounts
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
