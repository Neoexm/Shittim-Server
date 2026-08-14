namespace Shittim_Server.Services
{
    // steam updates and verifies put the retail catalog back, so the splice is reapplied at every boot
    public class ClientCatalogPatchService : IHostedService
    {
        private readonly ModCatalogService catalog;
        private readonly ILogger<ClientCatalogPatchService> logger;

        public ClientCatalogPatchService(ModCatalogService catalog, ILogger<ClientCatalogPatchService> logger)
        {
            this.catalog = catalog;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                catalog.SyncClient();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to rewrite the client catalog with the modded bundles");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
