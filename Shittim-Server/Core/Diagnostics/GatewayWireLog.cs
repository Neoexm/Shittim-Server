using System.Text;
using Newtonsoft.Json;

namespace Shittim_Server.Core.Diagnostics
{
    // Appends every gateway exchange to logs/wire-{date}.txt in the same textual format as the reference captures under /captures, so a play session diffs line-for-line against official traffic.
    // Protocol-level faults surface as one of two opaque modal popups with no protocol name attached, and the server log only records that the request returned 200.
    // The leading # line - wall clock, protocol, whether the response went out AES-encrypted, key length the client sent - has no equivalent in the capture files, so it is a comment rather than part of the diffable body.
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

            // The envelope is rebuilt rather than reusing the one sent to the client: when the packet field is encrypted the real envelope carries base64 ciphertext, and a dump of that is useless for comparison.
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
                    // A wedged client relogs in a loop and an unbounded dump outgrows the disk overnight. Roll over rather than truncate, so the exchange that wedged it survives.
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
