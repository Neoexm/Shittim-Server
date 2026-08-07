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

    [ProtocolHandler(Protocol.TTS_GetFile)]
    public async Task<TTSGetFileResponse> GetFile(
        SchaleDataContext db,
        TTSGetFileRequest request,
        TTSGetFileResponse response)
    {
        // no synthesis backend here; never-ready just leaves the call button greyed out
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.IsFileReady = false;

        return response;
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
