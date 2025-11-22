using System.Net;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Configuration;
using Schale.Data;
using BlueArchiveAPI.Services;

namespace Shittim.Services.IrcClient
{
    public class IrcService : BackgroundService
    {
        private IrcServer server;

        public IrcService(IDbContextFactory<SchaleDataContext> context, IMapper mapper, ExcelTableService excelTableService)
        {
            server = new IrcServer(
                IPAddress.Any,
                Config.Instance.IrcConfiguration.IrcPort,
                context, mapper, excelTableService
            );
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await server.StartAsync(stoppingToken);
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            server.Stop();
            await base.StopAsync(stoppingToken);
        }
    }

    public static class IrcServiceExtensions
    {
        public static void AddIrcService(this IServiceCollection services)
        {
            services.AddHostedService<IrcService>();
        }
    }
}
