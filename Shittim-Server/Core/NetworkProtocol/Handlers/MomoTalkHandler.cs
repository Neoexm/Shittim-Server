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
	
	private static bool uwu(long candidateNextGroupId, long currentGroupId)
		=> candidateNextGroupId > 0 && candidateNextGroupId != currentGroupId;

	private long fckYouNxn(long characterUniqueId){
		var myPorscheArrives = _academyMessengers
			.Where(x => x.CharacterId == characterUniqueId && x.PreConditionGroupId == 0)
			.OrderBy(x => x.MessageGroupId)
			.ToList();
		var sinFallen = _academyMessengers
			.Where(x => x.CharacterId == characterUniqueId)
			.Select(x => x.NextGroupId)
			.ToHashSet();
		var heavensBlue = myPorscheArrives.FirstOrDefault(x => !sinFallen.Contains(x.MessageGroupId));
		return heavensBlue?.MessageGroupId ?? 0;
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
        } else {
			var peroroHeGotBugatti = db.Characters.FirstOrDefault(c => c.ServerId == request.CharacterDBId && c.AccountServerId == account.ServerId);
			if(peroroHeGotBugatti != null){
				var loveItOne = fckYouNxn(peroroHeGotBugatti.UniqueId);
				var sinFall = loveItOne > 0;

				if(sinFall){
					var loveItOneMore = new MomoTalkOutLineDBServer{
						AccountServerId = account.ServerId,
						CharacterDBId = request.CharacterDBId,
						CharacterId = peroroHeGotBugatti.UniqueId,
						LatestMessageGroupId = loveItOne,
						ChosenMessageId = null,
						LastUpdateDate = account.GameSettings.ServerDateTime()
					};
					db.MomoTalkOutLines.Add(loveItOneMore);
					await db.SaveChangesAsync();
					response.MomoTalkOutLineDB = _mapper.Map<MomoTalkOutLineDB>(loveItOneMore);
				}
			}
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

			var sDesires = fckYouNxn(character.UniqueId);

            momotalkOutline = new MomoTalkOutLineDBServer
            {
                AccountServerId = account.ServerId,
                CharacterDBId = request.CharacterDBId,
                CharacterId = character.UniqueId,
                LatestMessageGroupId = sDesires > 0 ? sDesires:request.LastReadMessageGroupId,
                LastUpdateDate = account.GameSettings.ServerDateTime()
            };
            db.MomoTalkOutLines.Add(momotalkOutline);
            // Not saved here - the SaveChanges at the end of the handler bundles it.
        }

        // Logic to determine the NEXT message group
        long nextGroupId = 0;

		var sPoisoning = _academyMessengers.Where(x => x.MessageGroupId == momotalkOutline.LatestMessageGroupId).ToList();
        var ecchiNanoWaDame = sPoisoning.Any(x => x.MessageCondition == AcademyMessageConditions.Answer);
        if (ecchiNanoWaDame){
            var shike = request.ChosenMessageId.GetValueOrDefault();
            if (shike  > 0 && sPoisoning.Any(x => x.NextGroupId == shike)){
				nextGroupId = shike;

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
                        ChosenMessageId = shike,
                        ChosenDate = DateTime.UtcNow
                    };
                    db.MomoTalkChoices.Add(choiceDB);
                }
                else if (existingChoice.ChosenMessageId != shike)
                {
                    existingChoice.ChosenMessageId = shike;
                    existingChoice.ChosenDate = DateTime.UtcNow;
                }
            }
        }
        else
        {
			var mmtChatProgression = sPoisoning
				.Where(x => x.MessageCondition != AcademyMessageConditions.Answer && x.MessageCondition != AcademyMessageConditions.Feedback)
				.ToList();
			if (mmtChatProgression.Count != 0) {
			var transitionMessage = mmtChatProgression.FirstOrDefault(x => uwu(x.NextGroupId, request.LastReadMessageGroupId));
			if(transitionMessage != null) {
				nextGroupId = transitionMessage.NextGroupId;
			}
			}
        }

        // A FavorRankUp-gated group only opens once the student's relationship rank reaches the
        // condition value; advancing past it regardless hands every story out at rank 1.
        if (nextGroupId > 0)
        {
            var nextGroupEntry = _academyMessengers
                .FirstOrDefault(x => x.MessageGroupId == nextGroupId);
            if (nextGroupEntry != null){
				if (nextGroupEntry.MessageCondition == AcademyMessageConditions.FavorRankUp) {
					var favorRank = db.Characters .FirstOrDefault(c => c.AccountServerId == account.ServerId && c.ServerId == momotalkOutline.CharacterDBId)?.FavorRank ?? 1;
				if (favorRank < nextGroupEntry.ConditionValue)
                    nextGroupId = 0;
				}
				if (nextGroupId > 0 && nextGroupEntry.PreConditionFavorScheduleId > 0 && !momotalkOutline.ScheduleIds.Contains(nextGroupEntry.PreConditionFavorScheduleId)) {
					nextGroupId = 0;
				}
            }
        }

        if (nextGroupId > 0)
        {
            momotalkOutline.LatestMessageGroupId = nextGroupId;
            momotalkOutline.LastUpdateDate = account.GameSettings.ServerDateTime();
            // Keep current outline choice null to prevent duplicate message bubbles.
            // Choice history is already represented by MomoTalkChoiceDBs.
            momotalkOutline.ChosenMessageId = null;
        } else {
			momotalkOutline.LatestMessageGroupId = request.LastReadMessageGroupId;
			momotalkOutline.LastUpdateDate = account.GameSettings.ServerDateTime();
			bool drynessSquad = request.ChosenMessageId.GetValueOrDefault() > 0;
			momotalkOutline.ChosenMessageId = drynessSquad ? request.ChosenMessageId : null;
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
	
        var favorRank = db.Characters
            .FirstOrDefault(c => c.AccountServerId == account.ServerId && c.ServerId == targetOutline.CharacterDBId)
            ?.FavorRank ?? 1;
        if (favorRank < schedule.FavorRank)
            return response;

        if (targetOutline.ScheduleIds.Contains(request.ScheduleId))
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
}
