using Schale.Data;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shittim.Services.Client;
using AutoMapper;
using BlueArchiveAPI.Services;

namespace Shittim.Services.IrcClient
{
    public class IrcClientConnection : IClientConnection
    {
        public IDbContextFactory<SchaleDataContext> Context { get; set; }
        public IMapper Mapper { get; set; }
        public ExcelTableService ExcelTableService { get; set; }
        public long AccountServerId { get; set; }

        public TcpClient TcpClient { get; set; }
        public CancellationTokenSource CancellationToken { get; set; }
        public StreamWriter StreamWriter { get; set; }
        public StreamReader StreamReader { get; set; }
        public string CurrentChannel { get; set; }

        public IrcClientConnection(
            TcpClient _client,
            CancellationTokenSource _cancellationToken,
            IDbContextFactory<SchaleDataContext> _ctx,
            IMapper _mapper,
            ExcelTableService _excel,
            StreamWriter _writer,
            StreamReader _reader,
            long _accountServerId,
            string _currentChannel
        )
        {
            TcpClient = _client;
            CancellationToken = _cancellationToken;
            Context = _ctx;
            Mapper = _mapper;
            ExcelTableService = _excel;
            StreamWriter = _writer;
            StreamReader = _reader;
            AccountServerId = _accountServerId;
            CurrentChannel = _currentChannel;
        }

        public Task SendChatMessage(string text)
        {
            return SendChatMessage(text, "Schale", 19900006, 0, IrcMessageType.Chat);
        }

        public Task SendEmote(long stickerId)
        {
            return SendChatMessage("", "Schale", 19900006, stickerId, IrcMessageType.Sticker);
        }

        public Task SendChatMessage(string text, string nickname, long pfpCharacterId, long stickerId, IrcMessageType messageType)
        {
            var reply = new Reply()
            {
                Prefix = "mx_admin_bot!admin@netadmin.example.com",
                Command = $"PRIVMSG {CurrentChannel}",
                Trailing = JsonSerializer.Serialize(
                    new IrcMessage()
                    {
                        CharacterId = pfpCharacterId,
                        MessageType = messageType,
                        AccountNickname = nickname,
                        Text = text,
                        SendTicks = DateTimeOffset.Now.Ticks,
                        StickerId = stickerId,
                    },
                    typeof(IrcMessage)
                ),
            }.ToString();

            StreamWriter.WriteLine(reply);
            return Task.CompletedTask;
        }
    }
}
