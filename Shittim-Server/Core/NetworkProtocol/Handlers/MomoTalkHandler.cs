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
    private readonly List<AcademyMessangerExcelT> _academyMessengers;

    public MomoTalkHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _academyMessengers = _excelService.GetTable<AcademyMessangerExcelT>();
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

        if (momotalkOutline == null) return response;

        // Logic to determine the NEXT message group
        long nextGroupId = 0;
        
        // If a specific choice was made
        if (request.ChosenMessageId.GetValueOrDefault() > 0)
        {
            var chosenMessage = _academyMessengers.FirstOrDefault(x => x.Id == request.ChosenMessageId.Value);
            if (chosenMessage != null)
            {
                nextGroupId = chosenMessage.NextGroupId;
                
                // Record the choice
                var choiceDB = new MomoTalkChoiceDBServer
                {
                    AccountServerId = account.ServerId,
                    CharacterDBId = request.CharacterDBId,
                    MessageGroupId = request.LastReadMessageGroupId,
                    ChosenMessageId = request.ChosenMessageId.Value,
                    ChosenDate = DateTime.UtcNow
                };
                db.MomoTalkChoices.Add(choiceDB);
            }
        }
        else
        {
            // Just reading through, find the current group info
            // We need to find any message in this group to get the NextGroupId
            // (Assuming all messages in a group point to the same next group, or we take the last one)
            var currentGroupMessages = _academyMessengers.Where(x => x.MessageGroupId == request.LastReadMessageGroupId).ToList();
            if (currentGroupMessages.Count != 0)
            {
                // Take the one that actually has a NextGroupId (transition point)
                var transitionMessage = currentGroupMessages.FirstOrDefault(x => x.NextGroupId > 0 && x.NextGroupId != request.LastReadMessageGroupId);
                
                // Fallback: just take the first one's NextGroupId not equal to itself? 
                // Or simply the first one if the structure is simple.
                // In generic flatbuffers, usually the "Last" message in a chain has the NextGroupId.
                // But since we query by GroupId, they likely share it or only the last one has it.
                if (transitionMessage != null)
                {
                    nextGroupId = transitionMessage.NextGroupId;
                }
                else
                {
                    // Maybe it's a linear chain where NextGroupId is on all of them?
                    nextGroupId = currentGroupMessages.FirstOrDefault()?.NextGroupId ?? 0;
                }
            }
        }

        if (nextGroupId > 0)
        {
            momotalkOutline.LatestMessageGroupId = nextGroupId;
            momotalkOutline.LastUpdateDate = DateTime.Now;
            
            // Also need to update ChosenMessageId in outline if it was a choice?
            if (request.ChosenMessageId.GetValueOrDefault() > 0)
            {
               momotalkOutline.ChosenMessageId = request.ChosenMessageId.Value;
            }
        }

        await db.SaveChangesAsync();

        response.MomoTalkOutLineDB = _mapper.Map<MomoTalkOutLineDB>(momotalkOutline);

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
