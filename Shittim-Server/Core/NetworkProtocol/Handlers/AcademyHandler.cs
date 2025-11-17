using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.FlatData;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class AcademyHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;

    public AcademyHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.Academy_GetInfo)]
    public async Task<AcademyGetInfoResponse> GetInfo(
        SchaleDataContext db,
        AcademyGetInfoRequest request,
        AcademyGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var academy = await db.Academies
            .FirstOrDefaultAsync(a => a.AccountServerId == account.ServerId);

        if (academy != null)
        {
            response.AcademyDB = _mapper.Map<AcademyDB>(academy);
        }

        var locations = db.GetAccountAcademyLocations(account.ServerId).ToList();

        response.AcademyLocationDBs = _mapper.Map<List<AcademyLocationDB>>(locations);

        return response;
    }

    [ProtocolHandler(Protocol.Academy_AttendSchedule)]
    public async Task<AcademyAttendScheduleResponse> AttendSchedule(
        SchaleDataContext db,
        AcademyAttendScheduleRequest request,
        AcademyAttendScheduleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var academy = await db.Academies
            .FirstOrDefaultAsync(a => a.AccountServerId == account.ServerId);

        if (academy != null)
        {
            response.AcademyDB = _mapper.Map<AcademyDB>(academy);
        }

        response.ExtraRewards = [];
        response.ParcelResultDB = new();

        return response;
    }
}
