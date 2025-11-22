using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Shittim.Commands;
using Schale.Data;
using Serilog;
using BlueArchiveAPI.Services;

namespace Shittim.Services.IrcClient
{
    public class IrcServer
    {
        private ConcurrentDictionary<TcpClient, IrcClientConnection> clients = new ConcurrentDictionary<TcpClient, IrcClientConnection>();
        private ConcurrentDictionary<string, List<long>> channels = new ConcurrentDictionary<string, List<long>>();

        private readonly TcpListener listener;

        private readonly IDbContextFactory<SchaleDataContext> contextFactory;
        private readonly IMapper mapper;
        private readonly ExcelTableService excelTableService;

        public IrcServer(
            IPAddress host, int port,
            IDbContextFactory<SchaleDataContext> _contextFactory,
            IMapper _mapper,
            ExcelTableService _excelTableService)
        {
            contextFactory = _contextFactory;
            mapper = _mapper;
            excelTableService = _excelTableService;

            listener = new TcpListener(host, port);
        }

        public async Task StartAsync(CancellationToken stoppingToken)
        {
            listener.Start();
            Log.Debug("Irc Server Started");

            while (!stoppingToken.IsCancellationRequested)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();

                _ = HandleMessageAsync(tcpClient);
                Log.Debug("TcpClient is trying to connect...");
            }
        }

        public async Task HandleMessageAsync(TcpClient tcpClient)
        {
            using var reader = new StreamReader(tcpClient.GetStream());
            using var writer = new StreamWriter(tcpClient.GetStream()) { AutoFlush = true };

            string line;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                var splitLine = line.Split(' ', 2);
                var commandStr = splitLine[0].ToUpper().Trim();
                var parameters = splitLine.Length > 1 ? splitLine[1] : "";

                if (!Enum.TryParse<IrcCommand>(commandStr, out var command))
                {
                    command = IrcCommand.UNKNOWN;
                }

                string result = "";

                switch (command)
                {
                    case IrcCommand.NICK:
                        result = await HandleNick(parameters);
                        break;
                    case IrcCommand.USER:
                        await HandleUser(parameters, tcpClient, writer);
                        break;
                    case IrcCommand.JOIN:
                        await HandleJoin(parameters, tcpClient);
                        break;
                    case IrcCommand.PRIVMSG:
                        await HandlePrivMsg(parameters, tcpClient);
                        break;
                    case IrcCommand.PING:
                        result = await HandlePing(parameters);
                        break;
                }

                if (result != null || result != "")
                    await writer.WriteLineAsync(result);
            }

            tcpClient.Close();
        }

        public void Stop()
        {
            listener.Stop();
        }

        private async Task<string> HandleNick(string parameters) // welcomes
        {
            return new Reply()
            {
                Prefix = "server",
                ReplyCode = ReplyCode.RPL_WELCOME,
                Trailing = "Welcome, Sensei."
            }.ToString();
        }

        private async Task HandleUser(string parameters, TcpClient client, StreamWriter writer) // sends over account server id
        {
            string[] args = parameters.Split(' ');
            var user_serverId = long.Parse(args[0].Split("_")[1]);

            clients[client] = new IrcClientConnection(
                client,
                new CancellationTokenSource(),
                contextFactory,
                mapper,
                excelTableService,
                writer,
                null,
                user_serverId,
                "#schale"
            );

            Log.Debug($"{args[0].Split("_")[0]} {user_serverId} logged in");
        }

        private async Task HandleJoin(string parameters, TcpClient client) // sends over channel id
        {
            var channel = parameters;

            if (!channels.ContainsKey(channel))
            {
                channels[channel] = new List<long>();
            }

            var connection = clients[client];

            channels[channel].Add(connection.AccountServerId);
            connection.CurrentChannel = channel;

            Log.Debug($"User {connection.AccountServerId} joined {channel}");

            // custom welcome
            await connection.SendChatMessage("Welcome, Sensei.");
            await connection.SendChatMessage("Type /help for more information.");
            await connection.SendEmote(2);
        }

        private async Task HandlePrivMsg(string parameters, TcpClient client)
        {
            string[] args = parameters.Split(' ', 2);

            var channel = args[0];
            var payloadStr = args[1].TrimStart(':');

            //Log.Debug("payload: " + payloadStr);

            var payload = JsonSerializer.Deserialize(payloadStr, typeof(IrcMessage)) as IrcMessage;

            if (payload.Text.StartsWith('/'))
            {
                var cmdStrings = payload.Text.Split(" ");

                var cmdName = cmdStrings.First().Split('/').Last();
                var cmdArgs = cmdStrings[1..];

                var connection = clients[client];

                // Log.Debug($"Command Name: {cmdName}");
                // Log.Debug($"Command Arguments: {string.Join(" ", cmdArgs)}");

                try
                {
                    Command? cmd = CommandFactory.CreateCommand(cmdName, connection, cmdArgs);

                    if (cmd is null)
                    {
                        await connection.SendChatMessage($"Invalid command {cmdName}, try /help");
                        return;
                    }

                    await Task.Run(() => cmd?.Execute());
                    await connection.SendChatMessage($"Command {cmdName} executed sucessfully! Please relog for it to take effect.");
                }
                catch (Exception ex)
                {
                    var cmdAtr = (CommandHandlerAttribute?)Attribute.GetCustomAttribute(CommandFactory.commands[cmdName], typeof(CommandHandlerAttribute));

                    await connection.SendChatMessage($"Command {cmdName} failed to execute!, " + ex.ToString());
                    await connection.SendChatMessage($"Usage: {cmdAtr.Usage}");
                }
            }
        }

        private async Task<string> HandlePing(string parameters)
        {
            return new Reply().ToString();
        }
    }

    public enum IrcCommand
    {
        PASS,
        NICK,
        USER,
        JOIN,
        PRIVMSG,
        PING,
        PART,
        QUIT,
        UNKNOWN
    }

    public enum IrcMessageType
    {
        None,
        Notice,
        Sticker,
        Chat,
        HistoryCount
    }

    public class IrcMessage : EventArgs
    {
        [JsonPropertyName("MessageType")]
        public IrcMessageType MessageType { get; set; }

        [JsonPropertyName("CharacterId")]
        public long CharacterId { get; set; }

        [JsonPropertyName("AccountNickname")]
        public string AccountNickname { get; set; }

        [JsonPropertyName("StickerId")]
        public long StickerId { get; set; }

        [JsonPropertyName("Text")]
        public string Text { get; set; }

        [JsonPropertyName("SendTicks")]
        public long SendTicks { get; set; }

        [JsonPropertyName("EmblemId")]
        public int EmblemId { get; set; }
    }
}
