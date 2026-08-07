using System.Text;
using BlueArchiveAPI.Configuration;

namespace Shittim_Server.Services
{
    // ToySteam builds the store call from a string literal, so with no name server there is nothing for store.steampowered.com to resolve to and the currency lookup that runs before Steam Inventory never answers.
    // A hosts entry is not an alternative here: pointing that host at loopback hands Steam's own CEF ui our 404s, its watchdog never sees steamwebhelper come up, and it restarts the helper in a loop across most of the machine's cores. The literal is the only place to move it that Steam does not share.
    public class ClientStoreUrlPatchService : IHostedService
    {
        private const string StockUrl = "https://store.steampowered.com/api/appdetails?appids={0}";
        private const string LocalUrl = "http://127.0.0.1:5000/api/appdetails?appids={0}";

        private readonly ILogger<ClientStoreUrlPatchService> logger;

        public ClientStoreUrlPatchService(ILogger<ClientStoreUrlPatchService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!Config.Instance.ServerConfiguration.AutoPatchClientStoreUrl)
                return Task.CompletedTask;

            try
            {
                Rewrite(LocalUrl);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to point the client store url at the local server");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (!Config.Instance.ServerConfiguration.AutoPatchClientStoreUrl)
                return Task.CompletedTask;

            try
            {
                Rewrite(StockUrl);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restore the client store url");
            }

            return Task.CompletedTask;
        }

        // The replacement is shorter than the stock url, so it goes down over the same bytes and only the record's length moves. That keeps the tail of the stock text sitting untouched under it and restoring is just writing all 56 bytes back.
        private void Rewrite(string url)
        {
            var path = ClientMetadataPatchService.GetMetadataPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            var bytes = File.ReadAllBytes(path);
            var literals = BitConverter.ToInt32(bytes, 8);
            var literalsSize = BitConverter.ToInt32(bytes, 12);
            var data = BitConverter.ToInt32(bytes, 16);
            var dataSize = BitConverter.ToInt32(bytes, 20);

            var stock = Encoding.UTF8.GetBytes(StockUrl);
            var local = Encoding.UTF8.GetBytes(LocalUrl);
            var wanted = Encoding.UTF8.GetBytes(url);

            var record = -1;
            var index = 0;

            for (var i = literals; i + 8 <= literals + literalsSize; i += 8)
            {
                var length = BitConverter.ToInt32(bytes, i);
                var at = BitConverter.ToInt32(bytes, i + 4);
                if (at < 0 || length < 0 || at + length > dataSize)
                    continue;

                if (length != stock.Length && length != local.Length)
                    continue;

                var text = bytes.AsSpan(data + at, length);
                if (!text.SequenceEqual(stock) && !text.SequenceEqual(local))
                    continue;

                if (record >= 0)
                {
                    logger.LogWarning("More than one steam store url in {MetadataPath}; leaving it alone", path);
                    return;
                }

                record = i;
                index = at;
            }

            if (record < 0)
            {
                logger.LogWarning("Could not find the steam store url in {MetadataPath}; leaving it alone", path);
                return;
            }

            using (var stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                stream.Position = data + index;
                stream.Write(wanted, 0, wanted.Length);

                stream.Position = record;
                stream.Write(BitConverter.GetBytes(wanted.Length), 0, 4);
                stream.Flush(true);
            }

            logger.LogInformation("Client store url set to {StoreUrl}: {MetadataPath}", url, path);
        }
    }
}
