using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers
{
    public class CraftHandler : ProtocolHandlerBase
    {
        private readonly ISessionKeyService _sessionService;
        private readonly ExcelTableService _excelService;
        private readonly IMapper _mapper;

        public CraftHandler(
            IProtocolHandlerRegistry registry,
            ISessionKeyService sessionService,
            ExcelTableService excelService,
            IMapper mapper) : base(registry)
        {
            _sessionService = sessionService;
            _excelService = excelService;
            _mapper = mapper;
        }

        [ProtocolHandler(Protocol.Craft_List)]
        public async Task<CraftInfoListResponse> CraftList(
            SchaleDataContext db,
            CraftInfoListRequest request,
            CraftInfoListResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

            var craftInfos = db.CraftInfos
                .Where(x => x.AccountServerId == account.ServerId)
                .ToList();

            // If no craft infos exist (new account), should we initialize?
            // For now, return what we have. If logic requires initialization, we'll add it.
            // Typically CraftUnlock conditions apply, but assuming at least 1 slot might be open?
            // Let's implement initialization if list is empty, just in case.
            if (!craftInfos.Any())
            {
               // TODO: Initialize default craft slots if needed.
               // For now, returning empty list is safe.
            }

            response.CraftInfos = _mapper.Map<List<CraftInfoDB>>(craftInfos);
            response.ShiftingCraftInfos = []; // Not implemented yet or separate

            return response;
        }
    }
}
