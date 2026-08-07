using System.Text.Json;
using Schale.MX.NetworkProtocol;
using Serilog;

namespace Shittim_Server.Services;

/// <summary>
/// Operator-set ServerNotification bits and the maintenance gate, both driven from the Control Center rather than from game state.
/// The gateway ORs <see cref="Flags"/> onto the notification it computes per request, and answers everything except the crypto handshake with <see cref="GateError"/> while that is set.
/// </summary>
public static class ServerNoticeService
{
    private static string StatePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "server_notice.json");

    private class NoticeState
    {
        public int flags { get; set; }
        public int gate_error { get; set; }
        public string? gate_message { get; set; }
    }

    public static ServerNotificationFlag Flags { get; private set; }
    public static WebAPIErrorCode? GateError { get; private set; }
    public static string GateMessage { get; private set; } = "Server is under maintenance";

    public static void Load()
    {
        try
        {
            var path = Path.GetFullPath(StatePath);
            if (!File.Exists(path))
                return;

            var state = JsonSerializer.Deserialize<NoticeState>(File.ReadAllText(path));
            if (state == null)
                return;

            Flags = (ServerNotificationFlag)state.flags;
            GateError = state.gate_error == 0 ? null : (WebAPIErrorCode)state.gate_error;
            if (!string.IsNullOrEmpty(state.gate_message))
                GateMessage = state.gate_message;

            if (Flags != ServerNotificationFlag.None)
                Log.Information("Forcing ServerNotification bits {Flags} on every response", (int)Flags);
            if (GateError != null)
                Log.Warning("Maintenance gate is ACTIVE - every protocol but Queuing_GetCryptoKeys answers {ErrorCode}", GateError);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not read {Path}, notification overrides stay off", StatePath);
        }
    }

    public static void Set(ServerNotificationFlag flags, WebAPIErrorCode? gateError, string message)
    {
        Flags = flags;
        GateError = gateError;
        GateMessage = message;

        var state = new NoticeState { flags = (int)flags, gate_error = (int)(gateError ?? 0), gate_message = message };
        File.WriteAllText(Path.GetFullPath(StatePath), JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    // Everything the client can read after Queuing_GetCryptoKeys is fair game, but gating that one leaves it without the session material to decode the error, so the maintenance screen degrades into a connection failure.
    public static bool IsGated(Protocol protocol) => GateError != null && protocol != Protocol.Queuing_GetCryptoKeys;
}
