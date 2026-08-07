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
            var character = db.Characters.FirstOrDefault(c => c.ServerId == request.CharacterDBId && c.AccountServerId == account.ServerId);
            if (character == null) return response;

            // LastReadMessageGroupId is 0 the first time a student is opened, and storing that leaves them with a
            // conversation that can never start, so fall back to their first unlocked group.
            var openingGroup = request.LastReadMessageGroupId > 0
                ? request.LastReadMessageGroupId
                : MomoTalkService.OpeningGroup(_academyMessengers, character.UniqueId, character.FavorRank);

            // A 0 here means the student has no messenger rows or their first group is still rank-locked;
            // persisting it would recreate the stuck-conversation state this outline exists to avoid.
            if (openingGroup == 0)
                return response;

            momotalkOutline = new MomoTalkOutLineDBServer
            {
                AccountServerId = account.ServerId,
                CharacterDBId = request.CharacterDBId,
                CharacterId = character.UniqueId,
                LatestMessageGroupId = openingGroup,
                LastUpdateDate = account.GameSettings.ServerDateTime()
            };
            db.MomoTalkOutLines.Add(momotalkOutline);
            // Not saved here - the SaveChanges at the end of the handler bundles it.
        }

        var favorRank = db.Characters
            .FirstOrDefault(c => c.AccountServerId == account.ServerId
                && c.ServerId == momotalkOutline.CharacterDBId)?.FavorRank ?? 1;

        long nextGroupId = 0;
        
        if (request.ChosenMessageId.GetValueOrDefault() > 0)
        {
            var chosenMessage = _academyMessengers.FirstOrDefault(x => x.Id == request.ChosenMessageId.Value);
            if (chosenMessage != null)
            {
                nextGroupId = chosenMessage.NextGroupId;
                
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
            // No choice made, so the next group comes from the group being read.
            var currentGroupMessages = _academyMessengers.Where(x => x.MessageGroupId == request.LastReadMessageGroupId).ToList();
            if (currentGroupMessages.Count != 0)
            {
                // The transition is carried by whichever message points somewhere other than its own group. If none does, fall back to the first message's NextGroupId.
                var transitionMessage = currentGroupMessages.FirstOrDefault(x => x.NextGroupId > 0 && x.NextGroupId != request.LastReadMessageGroupId);
                nextGroupId = transitionMessage != null
                    ? transitionMessage.NextGroupId
                    : currentGroupMessages[0].NextGroupId;
            }
        }

        // A FavorRankUp-gated group only opens once the student's relationship rank reaches the condition value; advancing past it regardless hands every story out at rank 1.
        if (nextGroupId > 0)
        {
            // Ordered by Id, not table order: the condition sits on the group's opening row and only the dump
            // happens to store them in that order.
            var nextGroupEntry = _academyMessengers
                .Where(x => x.MessageGroupId == nextGroupId)
                .OrderBy(x => x.Id)
                .FirstOrDefault();
            if (nextGroupEntry != null
                && nextGroupEntry.MessageCondition == AcademyMessageConditions.FavorRankUp
                && favorRank < nextGroupEntry.ConditionValue)
            {
                // Remember where the conversation stopped so the login sync can resume it once the rank
                // is reached - without this marker it cannot tell a gate-stop from an unread group.
                momotalkOutline.PendingGateGroupId = nextGroupId;
                nextGroupId = 0;
            }
        }

        if (nextGroupId > 0)
        {
            momotalkOutline.LatestMessageGroupId = nextGroupId;
            momotalkOutline.PendingGateGroupId = null;
            momotalkOutline.LastUpdateDate = account.GameSettings.ServerDateTime();
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

        MomoTalkService.SyncOutlines(db, account, _academyMessengers);
        await db.SaveChangesAsync();

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

        // Every one of these used to return an empty success, which the client reads as "reward accepted" while
        // nothing was granted and nothing was spent. They are real refusals and have to reach the client as errors.
        var schedule = _academyFavorSchedules.GetScheduleById(request.ScheduleId)
            ?? throw new WebAPIException(WebAPIErrorCode.AcademyScheduleTableNotFound,
                $"Favor schedule {request.ScheduleId} not found");

        var targetOutline = outlines.FirstOrDefault(x => x.CharacterId == schedule.CharacterId)
            ?? throw new WebAPIException(WebAPIErrorCode.AcademyRewardCharacterNotFound,
                $"No MomoTalk outline for character {schedule.CharacterId}");

        if (targetOutline.ScheduleIds.Contains(request.ScheduleId))
            throw new WebAPIException(WebAPIErrorCode.AcademyAlreadyAttendedFavorSchedule,
                $"Favor schedule {request.ScheduleId} already attended");

        var accountCurrency = db.Currencies.FirstOrDefault(x => x.AccountServerId == account.ServerId);
        if (accountCurrency == null
            || !accountCurrency.CurrencyDict.TryGetValue(CurrencyTypes.AcademyTicket, out var currentTicket)
            || currentTicket <= 0)
        {
            throw new WebAPIException(WebAPIErrorCode.AcademyTicketZero,
                "No AcademyTicket available for a favor schedule");
        }

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
        targetOutline.LastUpdateDate = account.GameSettings.ServerDateTime();

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

    // answers travel inside MomoTalk_Read's AnswerScenarioId; Reply is a leftover the client never sends.
    [ProtocolHandler(Protocol.MomoTalk_Reply)]
    public async Task<MomoTalkReplyResponse> Reply(
        SchaleDataContext db,
        MomoTalkReplyRequest request,
        MomoTalkReplyResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        return response;
    }
}
