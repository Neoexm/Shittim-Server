using BlueArchiveAPI.NetworkModels;
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
            var req = JsonConvert.DeserializeObject<TRequest>(
                Utils.DecryptRequestPacket(packet));
            
            ResponsePacket res = await Handle(req);
            
            res.ServerTimeTicks = DateTime.Now.Ticks;
            res.SessionKey ??= req.SessionKey;

            return Utils.EncryptResponsePacket(res, ResponseProtocol);
        }
    }
}
