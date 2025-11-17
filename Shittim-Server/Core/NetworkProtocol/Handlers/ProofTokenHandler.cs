using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ProofTokenHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;

    public ProofTokenHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService) : base(registry)
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

        response.Hint = 42;
        response.Question = "proof";

        return response;
    }

    [ProtocolHandler(Protocol.ProofToken_Submit)]
    public async Task<ProofTokenSubmitResponse> Submit(
        SchaleDataContext db,
        ProofTokenSubmitRequest request,
        ProofTokenSubmitResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }
}
