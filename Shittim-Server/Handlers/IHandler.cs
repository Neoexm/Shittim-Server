using BlueArchiveAPI.NetworkModels;
using Protocol = Schale.MX.NetworkProtocol.Protocol;

namespace BlueArchiveAPI.Handlers
{
    public interface IHandler
    {
        Protocol RequestProtocol { get; }
        Protocol ResponseProtocol { get; }
        Task<byte[]> Handle(string packet);
    }
}
