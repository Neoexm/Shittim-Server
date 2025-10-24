using BlueArchiveAPI.NetworkModels;
using Protocol = Plana.MX.NetworkProtocol.Protocol;

namespace BlueArchiveAPI.Handlers
{
    public static class TTS
    {
        [ProtocolHandler(Protocol.TTS_GetKana)]
        public class GetKana : BaseHandler<TTSGetKanaRequest, TTSGetKanaResponse>
        {
            protected override async Task<TTSGetKanaResponse> Handle(TTSGetKanaRequest request)
            {
                return new TTSGetKanaResponse
                {
                    CallNameKatakana = request.CallName
                };
            }
        }
    }
}

