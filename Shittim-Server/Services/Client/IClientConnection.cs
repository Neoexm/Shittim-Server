using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using BlueArchiveAPI.Services;
using Shittim.Services.IrcClient;

namespace Shittim.Services.Client
{
    public interface IClientConnection
    {
        public IDbContextFactory<SchaleDataContext> Context { get; set; }
        public IMapper Mapper { get; set; }
        public ExcelTableService ExcelTableService { get; set; }
        public long AccountServerId { get; set; }

        Task SendChatMessage(string text);
        Task SendChatMessage(string text, string nickname, long pfpCharacterId, long stickerId, IrcMessageType messageType);
        Task SendEmote(long stickerId);
    }
}
