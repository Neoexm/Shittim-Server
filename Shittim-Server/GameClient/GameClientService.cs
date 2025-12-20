using Shittim_Server.Services;

namespace Shittim_Server.GameClient
{

    public class GameClientService : IHostedService
    {
        private readonly SchaleAI _schaleAI;

        public GameClientService(SchaleAI schaleAI, SharedDataCacheService _)
        {
            _schaleAI = schaleAI;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _schaleAI.InitializeSchale();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
