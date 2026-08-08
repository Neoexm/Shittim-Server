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

            momotalkOutline = new MomoTalkOutLineDBServer
            {
                AccountServerId = account.ServerId,
                CharacterDBId = request.CharacterDBId,
                CharacterId = character.UniqueId,
                LatestMessageGroupId = request.LastReadMessageGroupId,
                LastUpdateDate = account.GameSettings.ServerDateTime()
            };
            db.MomoTalkOutLines.Add(momotalkOutline);
            // Not saved here - the SaveChanges at the end of the handler bundles it.
        }

if (request.ChosenMessageId.GetValueOrDefault() > 0 &&
    _excelService.GetTable<AcademyMessangerExcelT>().Any(x =>
        x.Id == request.ChosenMessageId.Value &&
        x.MessageGroupId == request.LastReadMessageGroupId &&
        x.CharacterId == momotalkOutline.CharacterId &&
        x.MessageCondition == AcademyMessageConditions.Answer))
{
            // LastReadMessageGroupId is the Answer group and ChosenMessageId is the tapped row's Id. The transcript rebuild looks the pair up verbatim (MomoTalkDBService.RestoreMessageGroupHistory), so store it as sent.
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

        // The outline only mirrors the last group the client actually displayed; the client walks the chain and applies every FavorRankUp/schedule gate itself (CalcNewArrivalMessageGroupIds). Advancing to a computed successor parks Latest on groups the player never saw - and once it sits on an unanswered Answer group, the walk resolves the branch from the first row and the prompt never shows again.
        if (request.LastReadMessageGroupId > momotalkOutline.LatestMessageGroupId)
        {
            momotalkOutline.LatestMessageGroupId = request.LastReadMessageGroupId;
            momotalkOutline.LastUpdateDate = account.GameSettings.ServerDateTime();
        }
        // Keep current outline choice null to prevent duplicate message bubbles.
        // Choice history is already represented by MomoTalkChoiceDBs.
        momotalkOutline.ChosenMessageId = null;

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

        // Relationship events are free - the schedule ticket belongs to the lesson system. The only gate is the schedule's own favor rank.
        var favorRank = db.Characters.FirstOrDefault(c => c.AccountServerId == account.ServerId && c.UniqueId == schedule.CharacterId)?.FavorRank ?? 1;
        if (favorRank < schedule.FavorRank)
            return response;

        var parcelResults = new List<ParcelResult>();

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
