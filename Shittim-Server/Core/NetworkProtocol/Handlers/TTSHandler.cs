using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class TTSHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;

    public TTSHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService) : base(registry)
    {
        _sessionService = sessionService;
    }

    [ProtocolHandler(Protocol.TTS_GetKana)]
    public async Task<TTSGetKanaResponse> GetKana(
        SchaleDataContext db,
        TTSGetKanaRequest request,
        TTSGetKanaResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.CallNameKatakana = request.CallName;

        return response;
    }
}
