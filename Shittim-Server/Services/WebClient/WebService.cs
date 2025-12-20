using System.Collections.Concurrent;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using BlueArchiveAPI.Services;

namespace Shittim.Services.WebClient
{
    public class WebService
    {
        private ConcurrentDictionary<long, WebClientConnection> clients = new ConcurrentDictionary<long, WebClientConnection>();

        private readonly IDbContextFactory<SchaleDataContext> contextFactory;
        private readonly IMapper mapper;
        private readonly ExcelTableService excelTableService;

        public WebService(
            IDbContextFactory<SchaleDataContext> _contextFactory,
            IMapper _mapper,
            ExcelTableService _excelTableService
        )
        {
            contextFactory = _contextFactory;
            mapper = _mapper;
            excelTableService = _excelTableService;
        }

        public WebClientConnection GetClient(long uid, StreamWriter writer)
        {
            if (clients.TryGetValue(uid, out WebClientConnection existedClient))
            {
                existedClient.StreamWriter = writer;
                return existedClient;
            }

            var client = new WebClientConnection(
                contextFactory,
                mapper,
                excelTableService,
                writer,
                uid
            );

            clients.TryAdd(uid, client);
            return client;
        }

        public WebClientConnection GetClient(long uid)
        {
            if (clients.TryGetValue(uid, out WebClientConnection existedClient))
                return existedClient;

            var client = new WebClientConnection(
                contextFactory,
                mapper,
                excelTableService,
                new StreamWriter(new MemoryStream()),
                uid
            );

            clients.TryAdd(uid, client);
            return client;
        }
    }

    public static class WebServiceExtensions
    {
        public static void AddWebService(this IServiceCollection services)
        {
            services.AddSingleton<WebService>();
        }
    }
}
