using System.Text;
using System.Text.Json.Nodes;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    // "Region : asia" on the title screen is UITitle.SetInformationLabel concatenating a string literal with GameConfig's LastRegion, and the country lookup rewrites LastRegion from the name of the ServerRegion member it picks before the title screen ever appears, so LocalConfig.json only ever holds the text until the next boot and nothing on the wire moves the label.
    // Renaming that member is what actually moves it, and it has to be the member the lookup picks - asia here - because parking the text on an unused member gets overwritten the moment the lookup runs.
    // The name has to match everywhere else the region gets spelled out or routing breaks with it: the standalone literal co_SetupServerData compares against, and the override connection group name in the server_config it fetched. Miss either and the gateway url comes back empty and Queuing_GetTicket dies in the Uri constructor with the client sitting on the loading screen.
    public class ClientRegionLabelPatchService : IHostedService
    {
        private const int FieldRecordSize = 12;
        private const int LiteralRecordSize = 8;
        private const int RoutedRegionField = 3;
        private const string StockRegionName = "asia";

        private static readonly string[] ServerRegionFields = { "value__", "kr", "tw", "asia", "na", "global" };

        private readonly ILogger<ClientRegionLabelPatchService> logger;

        public ClientRegionLabelPatchService(ILogger<ClientRegionLabelPatchService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                Apply(Config.Instance.ServerConfiguration.RegionDisplayText);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to patch the client region label");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                Apply("");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restore the client region label");
            }

            return Task.CompletedTask;
        }

        private void Apply(string text)
        {
            var name = RegionName(text);
            var custom = name != StockRegionName;

            // A restore has to clear the saved region before any of the scans below get their chance to bail out, since the name they were pointing at is gone from the metadata either way and the client sits on the loading screen instead of saying so.
            if (!custom)
                WriteLastRegion(name);

            var path = ClientMetadataPatchService.GetMetadataPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            var bytes = File.ReadAllBytes(path);

            var literalOffset = BitConverter.ToInt32(bytes, 8);
            var literalSize = BitConverter.ToInt32(bytes, 12);
            var literalData = BitConverter.ToInt32(bytes, 16);
            var literalDataSize = BitConverter.ToInt32(bytes, 20);
            var stringOffset = BitConverter.ToInt32(bytes, 24);
            var stringSize = BitConverter.ToInt32(bytes, 28);
            var fieldsOffset = BitConverter.ToInt32(bytes, 96);
            var fieldsSize = BitConverter.ToInt32(bytes, 100);

            var scratch = ScratchOffset(bytes);
            var regionName = Encoding.UTF8.GetBytes(name);

            var regionFields = FindServerRegionFields(bytes, stringOffset, stringSize, fieldsOffset, fieldsSize);
            if (regionFields < 0)
            {
                logger.LogWarning("Could not find the ServerRegion enum in {MetadataPath}; leaving the region label alone", path);
                return;
            }

            var regionLiteral = FindRegionLiteral(bytes, literalOffset, literalSize, literalData, literalDataSize, scratch - literalData);
            if (regionLiteral < 0)
            {
                logger.LogWarning("Could not find the region literal in {MetadataPath}; leaving the region label alone", path);
                return;
            }

            var nameIndex = scratch - stringOffset;
            var dataIndex = scratch - literalData;
            if (!custom)
            {
                var stock = StringOffsets(bytes, stringOffset, stringSize, StockRegionName);
                // Whichever copy turns up first will do, the literal record carries its own length and nothing reads it as a c string, so it does not have to be the standalone one.
                var inSection = bytes.AsSpan(literalData, literalDataSize).IndexOf(regionName);
                if (stock.Count == 0 || inSection < 0)
                {
                    logger.LogWarning("Could not find the stock region name in {MetadataPath}; leaving the region label alone", path);
                    return;
                }

                nameIndex = stock.Min();
                dataIndex = inSection;
            }

            using (var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                if (custom)
                {
                    stream.Position = scratch;
                    stream.Write(regionName, 0, regionName.Length);
                    stream.WriteByte(0);
                }

                stream.Position = regionLiteral;
                stream.Write(BitConverter.GetBytes(regionName.Length), 0, 4);
                stream.Write(BitConverter.GetBytes(dataIndex), 0, 4);

                stream.Position = regionFields + RoutedRegionField * FieldRecordSize;
                stream.Write(BitConverter.GetBytes(nameIndex), 0, 4);
                stream.Flush(true);
            }

            WriteLastRegion(name);

            if (custom)
                logger.LogInformation("Client region label set to \"{RegionText}\": {MetadataPath}", name, path);
            else
                logger.LogInformation("Restored the client region label: {MetadataPath}", path);
        }

        // Blank falls back to the stock name because SetInformationLabel drops the label entirely when LastRegion is empty.
        private static string RegionName(string text)
        {
            text = (text ?? "").Trim();
            return text.Length > 0 ? text : StockRegionName;
        }

        // The label itself. GameConfig loads this table at startup and FindLastRegion reads it straight back; the login coroutine overwrites the entry in memory without saving it, so the file keeps the configured text until the next server start.
        // LastConnectSaveData spells it a second time for the reconnect path, and neither file lives under the install directory, so reinstalling the client leaves both of them still naming a region that only existed while the patch was on.
        private void WriteLastRegion(string region)
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Nexon Games", "Blue Archive");

            var path = Path.Combine(directory, "LocalConfig.json");
            if (File.Exists(path))
            {
                var config = JsonNode.Parse(File.ReadAllText(path));
                var stringTable = config["StringTable"];
                if (stringTable != null && stringTable["LastRegion"]?.GetValue<string>() != region)
                {
                    stringTable["LastRegion"] = region;
                    File.WriteAllText(path, config.ToJsonString());
                }
            }

            var connectPath = Path.Combine(directory, "LastConnectSaveData");
            if (!File.Exists(connectPath))
                return;

            var connect = JsonNode.Parse(File.ReadAllText(connectPath));
            if (connect["Region"]?.GetValue<string>() == region)
                return;

            connect["Region"] = region;
            File.WriteAllText(connectPath, connect.ToJsonString());
        }

        // Anchors on the members around the one it rewrites, since the blob has thousands of loose names and the field table gives no other way back to a type. The rewritten slot itself has to stay out of the match or the patch only ever applies to a stock file.
        private static int FindServerRegionFields(byte[] bytes, int stringOffset, int stringSize, int fieldsOffset, int fieldsSize)
        {
            var names = ServerRegionFields.Select(n => StringOffsets(bytes, stringOffset, stringSize, n)).ToArray();
            var found = -1;

            for (var i = fieldsOffset; i + names.Length * FieldRecordSize <= fieldsOffset + fieldsSize; i += FieldRecordSize)
            {
                var match = true;
                for (var f = 0; f < names.Length && match; f++)
                    match = f == RoutedRegionField || names[f].Contains(BitConverter.ToInt32(bytes, i + f * FieldRecordSize));

                if (!match)
                    continue;

                if (found >= 0)
                    return -1;

                found = i;
            }

            return found;
        }

        // The record gets aimed somewhere else rather than the bytes under it being overwritten, so on a file this has already run against it is the one pointing at the scratch text, and otherwise the only literal spelling the stock name on its own.
        private static int FindRegionLiteral(byte[] bytes, int literalOffset, int literalSize, int literalData, int literalDataSize, int scratchIndex)
        {
            var stock = Encoding.UTF8.GetBytes(StockRegionName);
            var found = -1;

            for (var i = literalOffset; i + LiteralRecordSize <= literalOffset + literalSize; i += LiteralRecordSize)
            {
                var length = BitConverter.ToInt32(bytes, i);
                var index = BitConverter.ToInt32(bytes, i + 4);
                if (index == scratchIndex)
                    return i;

                if (length != stock.Length || index < 0 || index + length > literalDataSize)
                    continue;

                if (!bytes.AsSpan(literalData + index, length).SequenceEqual(stock))
                    continue;

                if (found >= 0)
                    return -1;

                found = i;
            }

            return found;
        }

        private static HashSet<int> StringOffsets(byte[] bytes, int stringOffset, int stringSize, string name)
        {
            var needle = Encoding.UTF8.GetBytes(name + "\0");
            var section = bytes.AsSpan(stringOffset, stringSize);
            var offsets = new HashSet<int>();
            var start = 0;

            while (start <= section.Length - needle.Length)
            {
                var index = section[start..].IndexOf(needle);
                if (index < 0)
                    break;

                var absolute = start + index;
                if (absolute == 0 || section[absolute - 1] == 0)
                    offsets.Add(absolute);

                start = absolute + 1;
            }

            return offsets;
        }

        // String indexes are plain byte offsets that nothing bounds-checks at runtime, so the replacement member name is parked past the last section where it cannot shift anything the loader walks.
        private static int ScratchOffset(byte[] bytes)
        {
            var end = 0;
            for (var i = 8; i < 8 + 31 * 8; i += 8)
                end = Math.Max(end, BitConverter.ToInt32(bytes, i) + BitConverter.ToInt32(bytes, i + 4));

            return end;
        }
    }
}
