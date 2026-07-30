using System.Text;
using Newtonsoft.Json;

namespace Shittim_Server.Core.Diagnostics
{
    /// <summary>
    /// Appends every gateway exchange to <c>logs/wire-{date}.txt</c> in the same textual format as the
    /// reference captures under <c>/captures</c>, so a play session diffs line-for-line against official
    /// traffic. The client renders most protocol-level faults as one of two opaque modal popups with no
    /// protocol name attached, and the server log only records that the request returned 200, so having
    /// the exact response bytes beside official's is the one reliable way to tell a content mismatch
    /// from a transport one.
    ///
    /// Each exchange gets a leading <c>#</c> line with the wall clock, protocol and response transport
    /// (whether the packet went out AES-encrypted, and the key length the client sent). The capture
    /// files have no equivalent, so it is a comment rather than part of the diffable body.
    /// </summary>
    public static class GatewayWireLog
    {
        private const long MaxBytes = 64L * 1024 * 1024;

        private static readonly object Gate = new();
        private static bool _enabled;
        private static string? _path;

        public static void Configure(bool enabled)
        {
            lock (Gate)
            {
                _enabled = enabled;
                _path = null;
            }
        }

        public static bool Enabled => _enabled;

        public static void Write(string requestJson, string protocolName, string responseJson, bool responseEncrypted, int requestKeyLength)
        {
            if (!_enabled)
                return;

            // The envelope is rebuilt rather than reusing the one sent to the client: when the
            // packet field is encrypted, the real envelope carries base64 ciphertext, and a dump of
            // that is useless for comparison. Always record the plaintext the handler produced.
            var envelope = JsonConvert.SerializeObject(new { protocol = protocolName, packet = responseJson });

            var builder = new StringBuilder();
            builder.Append("# ").Append(DateTime.Now.ToString("HH:mm:ss.fff"))
                   .Append(' ').Append(protocolName)
                   .Append(" aes=").Append(responseEncrypted)
                   .Append(" reqKeyLen=").Append(requestKeyLength)
                   .AppendLine();
            builder.AppendLine("========== REQUEST ==========");
            builder.AppendLine(requestJson);
            builder.AppendLine();
            builder.AppendLine("========== RESPONSE ==========");
            builder.AppendLine(envelope);
            builder.AppendLine();

            try
            {
                lock (Gate)
                {
                    var path = ResolvePath();
                    // A wedged client relogs in a loop, so an unbounded dump can outgrow the disk
                    // during an overnight session. Roll over rather than truncate, so the exchange
                    // that caused the wedge is never the one that gets discarded.
                    if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                    {
                        File.Move(path, Path.ChangeExtension(path, $".{DateTime.Now:HHmmss}.txt"), overwrite: true);
                    }

                    File.AppendAllText(path, builder.ToString(), Encoding.UTF8);
                }
            }
            catch (IOException)
            {
                // Diagnostics must never take the gateway down with them.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string ResolvePath()
        {
            if (_path != null)
                return _path;

            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, $"wire-{DateTime.Now:yyyy-MM-dd}.txt");
            return _path;
        }
    }
}
