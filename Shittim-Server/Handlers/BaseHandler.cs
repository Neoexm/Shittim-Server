using BlueArchiveAPI.NetworkModels;
using BlueArchiveAPI.Services;
using Newtonsoft.Json;

namespace BlueArchiveAPI.Handlers
{

    public abstract class BaseHandler<TRequest, TResponse> : IHandler
        where TRequest : RequestPacket, IRequest<TResponse>
        where TResponse : ResponsePacket, IResponse<TRequest>
    {
        protected abstract Task<TResponse> Handle(TRequest request);

        public Protocol RequestProtocol { get; }
        public Protocol ResponseProtocol { get; }
        
        protected BaseHandler()
        {
            RequestProtocol = Activator.CreateInstance<TRequest>().Protocol;
            ResponseProtocol = Activator.CreateInstance<TResponse>().Protocol;
        }

        public async Task<byte[]> Handle(string packet)
        {
            var req = JsonConvert.DeserializeObject<TRequest>(packet);
            
            ResponsePacket res = await Handle(req);
            
            res.ServerTimeTicks = DateTime.Now.Ticks;
            res.SessionKey ??= req.SessionKey;
            
            if (res.AccountId == 0 && req.SessionKey != null)
            {
                res.AccountId = req.SessionKey.AccountServerId;
            }
            else if (res.AccountId == 0)
            {
                res.AccountId = req.AccountId;
            }

            // Log wire contract for debugging (when enabled)
            ContractLogger.LogResponse(ResponseProtocol, res);

            return Utils.EncryptResponsePacket(res, ResponseProtocol);
        }
    }
}
