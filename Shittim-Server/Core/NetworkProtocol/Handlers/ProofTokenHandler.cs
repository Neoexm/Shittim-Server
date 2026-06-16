using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ProofTokenHandler : ProtocolHandlerBase
{
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

        // Hint/Question are the proof-of-work challenge. 42 is the known-working value: the client
        // solves it and fires ProofToken_Submit, and login completes. (Tested Hint=0 to try to kill
        // the transient "cannot be processed" login popup -- it instead BROKE the client's solver so
        // Submit never fires and login stalls, so that popup is NOT caused by this difficulty.)
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
