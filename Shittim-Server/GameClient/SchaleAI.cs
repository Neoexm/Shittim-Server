using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;

namespace Shittim_Server.GameClient
{
    public class SchaleAI : IGameClient
    {
        private readonly IDbContextFactory<SchaleDataContext> contextFactory;
        private readonly ExcelTableService excelTableService;
        private readonly IMapper mapper;

        public static string AccountDevId = "schaleLoveSENSEI";
        public static string AccountName = "Schale";

        public static long AccountId;

        public SchaleAI(IDbContextFactory<SchaleDataContext> contextFactory, ExcelTableService excelTableService, IMapper mapper)
        {
            this.contextFactory = contextFactory;
            this.excelTableService = excelTableService;
            this.mapper = mapper;
        }

        public async Task InitializeSchale()
        {
            var context = await contextFactory.CreateDbContextAsync();

            var account = await GameClientHelper.CreateAIClient(context, AccountName, AccountDevId);
            AccountId = account.ServerId;

            await SchaleService.CreateArenaEchelonDB(context, account);

            SchaleService.CreateBatchAssistCharacterDB(excelTableService, mapper);
        }
    }
}
