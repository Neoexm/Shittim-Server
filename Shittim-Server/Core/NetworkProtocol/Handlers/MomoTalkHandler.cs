using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.Excel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class MomoTalkHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly ParcelHandler _parcelHandler;
    private readonly List<AcademyMessangerExcelT> _academyMessengers;
    private readonly List<AcademyFavorScheduleExcelT> _academyFavorSchedules;

    public MomoTalkHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _parcelHandler = parcelHandler;
        _academyMessengers = _excelService.GetTable<AcademyMessangerExcelT>();
        _academyFavorSchedules = _excelService.GetTable<AcademyFavorScheduleExcelT>();
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
            .OrderBy(c => c.MessageGroupId)
            .ThenBy(c => c.ChosenDate)
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

        if (momotalkOutline == null)
        {
            // Fallback: Check if user owns the character and create outline
            var character = db.Characters.FirstOrDefault(c => c.ServerId == request.CharacterDBId && c.AccountServerId == account.ServerId);
            if (character == null) return response;

            momotalkOutline = new MomoTalkOutLineDBServer
            {
                AccountServerId = account.ServerId,
                CharacterDBId = request.CharacterDBId,
                CharacterId = character.UniqueId,
                LatestMessageGroupId = request.LastReadMessageGroupId,
                LastUpdateDate = DateTime.Now
            };
            db.MomoTalkOutLines.Add(momotalkOutline);
            // Save immediately or let the bottom SaveChanges call handle it?
            // Better to let bottom handle it so we bundle.
        }

        // Logic to determine the NEXT message group
        long nextGroupId = 0;
        
        // If a specific choice was made
        if (request.ChosenMessageId.GetValueOrDefault() > 0)
        {
            var chosenMessage = _academyMessengers.FirstOrDefault(x => x.Id == request.ChosenMessageId.Value);
            if (chosenMessage != null)
            {
                nextGroupId = chosenMessage.NextGroupId;
                
                // Record the choice if not exists
                var existingChoice = db.MomoTalkChoices.FirstOrDefault(x => 
                    x.AccountServerId == account.ServerId && 
                    x.CharacterDBId == request.CharacterDBId && 
                    x.MessageGroupId == request.LastReadMessageGroupId);

                if (existingChoice == null)
                {
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
                else if (existingChoice.ChosenMessageId != request.ChosenMessageId.Value)
                {
                    existingChoice.ChosenMessageId = request.ChosenMessageId.Value;
                    existingChoice.ChosenDate = DateTime.UtcNow;
                }
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
            // Keep current outline choice null to prevent duplicate message bubbles.
            // Choice history is already represented by MomoTalkChoiceDBs.
            momotalkOutline.ChosenMessageId = null;
        }

        await db.SaveChangesAsync();

        response.MomoTalkOutLineDB = _mapper.Map<MomoTalkOutLineDB>(momotalkOutline);

        var choices = db.GetAccountMomoTalkChoices(account.ServerId)
            .Where(c => c.CharacterDBId == request.CharacterDBId)
            .OrderBy(c => c.MessageGroupId)
            .ThenBy(c => c.ChosenDate)
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
        response.FavorScheduleRecords = MomoTalkService.GetAllFavorSchedules(outlines);

        return response;
    }

    [ProtocolHandler(Protocol.MomoTalk_FavorSchedule)]
    public async Task<MomoTalkFavorScheduleResponse> FavorSchedule(
        SchaleDataContext db,
        MomoTalkFavorScheduleRequest request,
        MomoTalkFavorScheduleResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var outlines = db.GetAccountMomoTalkOutLines(account.ServerId).ToList();
        response.FavorScheduleRecords = MomoTalkService.GetAllFavorSchedules(outlines);
        response.ParcelResultDB = new();

        var schedule = _academyFavorSchedules.GetScheduleById(request.ScheduleId);
        if (schedule == null)
            return response;

        var targetOutline = outlines.FirstOrDefault(x => x.CharacterId == schedule.CharacterId);
        if (targetOutline == null)
            return response;

        if (targetOutline.ScheduleIds.Contains(request.ScheduleId))
            return response;

        var accountCurrency = db.Currencies.FirstOrDefault(x => x.AccountServerId == account.ServerId);
        if (accountCurrency == null)
            return response;

        if (!accountCurrency.CurrencyDict.TryGetValue(CurrencyTypes.AcademyTicket, out var currentTicket) || currentTicket <= 0)
            return response;

        var parcelResults = new List<ParcelResult>
        {
            new(ParcelType.Currency, (long)CurrencyTypes.AcademyTicket, -1)
        };

        var rewardCount = new[]
        {
            schedule.RewardParcelType?.Count ?? 0,
            schedule.RewardParcelId?.Count ?? 0,
            schedule.RewardAmount?.Count ?? 0
        }.Min();

        for (int i = 0; i < rewardCount; i++)
        {
            var amount = schedule.RewardAmount![i];
            if (amount == 0)
                continue;

            parcelResults.Add(new ParcelResult(
                schedule.RewardParcelType![i],
                schedule.RewardParcelId![i],
                amount));
        }

        targetOutline.ScheduleIds.Add(request.ScheduleId);
        targetOutline.LastUpdateDate = DateTime.Now;

        if (parcelResults.Count > 0)
        {
            var parcelResolver = await _parcelHandler.BuildParcel(db, account, parcelResults);
            response.ParcelResultDB = parcelResolver.ParcelResult;
        }
        else
        {
            await db.SaveChangesAsync();
        }

        response.FavorScheduleRecords = MomoTalkService.GetAllFavorSchedules(db.GetAccountMomoTalkOutLines(account.ServerId).ToList());

        return response;
    }
}
