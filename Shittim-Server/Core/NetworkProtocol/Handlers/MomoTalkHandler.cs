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

public class MomoTalkHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;

    public MomoTalkHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.MomoTalk_MessageList)]
    public async Task<MomoTalkMessageListResponse> MessageList(
        SchaleDataContext db,
        MomoTalkMessageListRequest request,
        MomoTalkMessageListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var momotalkOutline = db.GetAccountMomoTalkOutLines(account.ServerId)
            .FirstOrDefault(o => o.CharacterDBId == request.CharacterDBId);

        if (momotalkOutline != null)
        {
            response.MomoTalkOutLineDB = _mapper.Map<MomoTalkOutLineDB>(momotalkOutline);
        }

        var choices = db.GetAccountMomoTalkChoices(account.ServerId)
            .Where(c => c.CharacterDBId == request.CharacterDBId)
            .ToList();

        response.MomoTalkChoiceDBs = _mapper.Map<List<MomoTalkChoiceDB>>(choices);

        return response;
    }

    [ProtocolHandler(Protocol.MomoTalk_Read)]
    public async Task<MomoTalkReadResponse> Read(
        SchaleDataContext db,
        MomoTalkReadRequest request,
        MomoTalkReadResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var momotalkOutline = db.GetAccountMomoTalkOutLines(account.ServerId)
            .FirstOrDefault(o => o.CharacterDBId == request.CharacterDBId);

        if (momotalkOutline != null)
        {
            response.MomoTalkOutLineDB = _mapper.Map<MomoTalkOutLineDB>(momotalkOutline);
        }

        var choices = db.GetAccountMomoTalkChoices(account.ServerId)
            .Where(c => c.CharacterDBId == request.CharacterDBId)
            .ToList();

        response.MomoTalkChoiceDBs = _mapper.Map<List<MomoTalkChoiceDB>>(choices);

        return response;
    }

    [ProtocolHandler(Protocol.MomoTalk_OutLine)]
    public async Task<MomoTalkOutLineResponse> OutLine(
        SchaleDataContext db,
        MomoTalkOutLineRequest request,
        MomoTalkOutLineResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var outlines = db.GetAccountMomoTalkOutLines(account.ServerId).ToList();

        response.MomoTalkOutLineDBs = _mapper.Map<List<MomoTalkOutLineDB>>(outlines);
        response.FavorScheduleRecords = [];

        return response;
    }

    [ProtocolHandler(Protocol.MomoTalk_FavorSchedule)]
    public async Task<MomoTalkFavorScheduleResponse> FavorSchedule(
        SchaleDataContext db,
        MomoTalkFavorScheduleRequest request,
        MomoTalkFavorScheduleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.FavorScheduleRecords = [];
        response.ParcelResultDB = new();

        return response;
    }
}
