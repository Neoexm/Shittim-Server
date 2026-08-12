using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BlueArchiveAPI;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Serilog;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ProofTokenHandler : ProtocolHandlerBase
{
    private const int ProofBits = 16;

    private static readonly ConcurrentDictionary<long, long> _pendingAnswers = new();

    private readonly ISessionKeyService _sessionService;

    public ProofTokenHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.ProofToken_RequestQuestion)]
    public async Task<ProofTokenRequestQuestionResponse> RequestQuestion(
        SchaleDataContext db,
        ProofTokenRequestQuestionRequest request,
        ProofTokenRequestQuestionResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // ProofTokenHelper.Solve sweeps offset over [0, 2^trailingZeros(Hint)) and keeps the first candidate whose base32(md5(utf16le(candidate.ToString()))) equals Question, so Question has to be a real hash of the answer and Hint has to be that same answer with its low bits cleared. Anything else and the sweep matches nothing, Solve returns its not-found 0, and the client reports the token as never submitted.
        // Forcing bit ProofBits on pins the sweep at exactly 2^ProofBits candidates and keeps Hint off zero, which spins the client's trailing-zero loop forever.
        var answer = Random.Shared.NextInt64(1L << 32) | (1L << ProofBits);
        _pendingAnswers[account.ServerId] = answer;

        using var md5 = MD5.Create();
        response.Hint = answer & ~((1L << ProofBits) - 1);
        response.Question = Utils.Base32Encode(md5.ComputeHash(Encoding.Unicode.GetBytes(answer.ToString())));

        return response;
    }

    [ProtocolHandler(Protocol.ProofToken_Submit)]
    public async Task<ProofTokenSubmitResponse> Submit(
        SchaleDataContext db,
        ProofTokenSubmitRequest request,
        ProofTokenSubmitResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (_pendingAnswers.TryRemove(account.ServerId, out var expected) && request.Answer != expected)
            Log.Warning("[ProofTokenHandler] Account {AccountServerId} submitted answer {Answer}, expected {Expected}", account.ServerId, request.Answer, expected);

        return response;
    }
}
