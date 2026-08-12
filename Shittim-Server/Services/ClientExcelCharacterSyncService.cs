namespace Shittim_Server.Services
{
    // Cloned students only ever existed in the ExcelDB copies, so the client losing its copy to an update leaves an account owning an id the client cannot look up. Nothing on the wire says which side is behind, and the failure lands in the middle of the login sync, so the copies are reconciled at startup instead.
    public class ClientExcelCharacterSyncService : IHostedService
    {
        private readonly CustomCharacterService characters;
        private readonly ILogger<ClientExcelCharacterSyncService> logger;

        public ClientExcelCharacterSyncService(CustomCharacterService characters, ILogger<ClientExcelCharacterSyncService> logger)
        {
            this.characters = characters;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                characters.SyncMissing();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to reconcile custom students across the ExcelDB copies");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
