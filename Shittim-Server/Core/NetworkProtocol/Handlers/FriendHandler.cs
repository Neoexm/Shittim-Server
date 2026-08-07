using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class FriendHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;

    public FriendHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.Friend_Check)]
    public async Task<FriendCheckResponse> Check(
        SchaleDataContext db,
        FriendCheckRequest request,
        FriendCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Friend_List)]
    public async Task<FriendListResponse> List(
        SchaleDataContext db,
        FriendListRequest request,
        FriendListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.FriendIdCardDB = account.GameSettings.FriendIdCard ?? BuildDefaultIdCard(db, account);
        response.IdCardBackgroundDBs = db.IdCardBackgrounds
            .Where(x => x.AccountServerId == account.ServerId).ToMapList(_mapper).ToArray();
        response.FriendDBs = [];
        response.SentRequestFriendDBs = [];
        response.ReceivedRequestFriendDBs = [];
        response.BlockedUserDBs = BuildBlockedList(db, account);

        return response;
    }

    [ProtocolHandler(Protocol.Friend_ListByIds)]
    public async Task<FriendListByIdsResponse> ListByIds(
        SchaleDataContext db,
        FriendListByIdsRequest request,
        FriendListByIdsResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ListResult = [];

        if (request.TargetAccountIds != null && request.TargetAccountIds.Length > 0)
        {
            var targetAccounts = await db.Accounts
                .Where(a => request.TargetAccountIds.Contains(a.ServerId))
                .ToListAsync();

            response.ListResult = targetAccounts.Select(x => BuildFriendDB(db, x, account)).ToArray();
        }

        return response;
    }

    [ProtocolHandler(Protocol.Friend_SendFriendRequest)]
    public async Task<FriendSendFriendRequestResponse> SendFriendRequest(
        SchaleDataContext db,
        FriendSendFriendRequestRequest request,
        FriendSendFriendRequestResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        
        throw new WebAPIException(WebAPIErrorCode.FriendRequestNotFound);
    }

    [ProtocolHandler(Protocol.Friend_GetIdCard)]
    public async Task<FriendGetIdCardResponse> GetIdCard(
        SchaleDataContext db,
        FriendGetIdCardRequest request,
        FriendGetIdCardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.FriendIdCardDB = account.GameSettings.FriendIdCard ?? BuildDefaultIdCard(db, account);
        return response;
    }

    [ProtocolHandler(Protocol.Friend_SetIdCard)]
    public async Task<FriendSetIdCardResponse> SetIdCard(
        SchaleDataContext db,
        FriendSetIdCardRequest request,
        FriendSetIdCardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        if (request.Comment?.Length > 100)
            throw new WebAPIException(WebAPIErrorCode.FriendIdCardCommentLengthOverLimit, "Comment too long");

        if (request.BackgroundId != 0
            && !db.IdCardBackgrounds.Any(x => x.AccountServerId == account.ServerId && x.UniqueId == request.BackgroundId))
        {
            throw new WebAPIException(WebAPIErrorCode.FriendBackgroundNotOwned,
                $"Id card background {request.BackgroundId} not owned");
        }

        account.Comment = request.Comment;
        var represent = db.GetAccountCharacters(account.ServerId)
            .FirstOrDefault(c => c.UniqueId == request.RepresentCharacterUniqueId);
        if (represent != null)
            account.RepresentCharacterServerId = represent.ServerId;

        var card = account.GameSettings.FriendIdCard ?? BuildDefaultIdCard(db, account);
        card.Comment = request.Comment;
        card.RepresentCharacterUniqueId = request.RepresentCharacterUniqueId;
        card.EmblemId = request.EmblemId;
        card.SearchPermission = request.SearchPermission;
        card.AutoAcceptFriendRequest = request.AutoAcceptFriendRequest;
        card.ShowAccountLevel = request.ShowAccountLevel;
        card.ShowFriendCode = request.ShowFriendCode;
        card.ShowRaidRanking = request.ShowRaidRanking;
        card.ShowArenaRanking = request.ShowArenaRanking;
        card.ShowEliminateRaidRanking = request.ShowEliminateRaidRanking;
        card.CardBackgroundId = request.BackgroundId;
        card.Level = account.Level;
        account.GameSettings.FriendIdCard = card;

        db.Accounts.Update(account);
        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.Friend_GetFriendDetailedInfo)]
    public async Task<FriendGetFriendDetailedInfoResponse> GetFriendDetailedInfo(
        SchaleDataContext db,
        FriendGetFriendDetailedInfoRequest request,
        FriendGetFriendDetailedInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var target = db.Accounts.FirstOrDefault(a => a.ServerId == request.FriendAccountId)
            ?? throw new WebAPIException(WebAPIErrorCode.FriendUserIsNotFriend,
                $"Account {request.FriendAccountId} does not exist");

        if (account.GameSettings.BlockedAccountIds.Contains(target.ServerId))
            throw new WebAPIException(WebAPIErrorCode.FriendBlockUserCannotOpenProfile,
                "Target is blocked");

        var attachment = db.GetAccountAttachments(target.ServerId).FirstOrDefault();
        var representCharacter = db.Characters.FirstOrDefault(c => c.ServerId == target.RepresentCharacterServerId);

        response.Nickname = target.Nickname ?? "Sensei";
        response.Level = target.Level;
        response.ClanName = "Schale Network";
        response.Comment = target.Comment;
        response.FriendCount = 0;
        response.FriendCode = AccountHandler.BuildFriendCode(target.ServerId);
        response.RepresentCharacterUniqueId = representCharacter?.UniqueId ?? target.RepresentCharacterServerId;
        response.RepresentCharacterCostumeId = representCharacter?.UniqueId ?? target.RepresentCharacterServerId;
        response.CharacterCount = db.GetAccountCharacters(target.ServerId).Count();
        response.AttachmentDB = attachment != null ? _mapper.Map<AccountAttachmentDB>(attachment) : null;
        response.AssistCharacterDBs = [];

        return response;
    }

    private FriendDB[] BuildBlockedList(SchaleDataContext db, AccountDBServer account)
    {
        return account.GameSettings.BlockedAccountIds
            .Select(id => db.Accounts.FirstOrDefault(a => a.ServerId == id))
            .Where(a => a != null)
            .Select(a => BuildFriendDB(db, a!, account))
            .ToArray();
    }

    private FriendDB BuildFriendDB(SchaleDataContext db, AccountDBServer target, AccountDBServer viewer)
    {
        var attachment = db.GetAccountAttachments(target.ServerId).FirstOrDefault();
        // RepresentCharacterServerId is a Characters row id; the wire field wants the character's UniqueId.
        var representUniqueId = RepresentCharacterUniqueId(db, target);
        return new FriendDB
        {
            AccountId = target.ServerId,
            Nickname = target.Nickname ?? "Sensei",
            Level = target.Level,
            RepresentCharacterUniqueId = representUniqueId,
            RepresentCharacterCostumeId = representUniqueId,
            LastConnectTime = viewer.GameSettings.ServerDateTime(),
            ComfortValue = 10000,
            FriendCount = 0,
            AttachmentDB = attachment != null ? _mapper.Map<AccountAttachmentDB>(attachment) : null
        };
    }

    internal static long RepresentCharacterUniqueId(SchaleDataContext db, AccountDBServer account)
        => db.Characters.FirstOrDefault(c => c.ServerId == account.RepresentCharacterServerId)?.UniqueId
            ?? account.RepresentCharacterServerId;

    private static FriendIdCardDB BuildDefaultIdCard(SchaleDataContext db, AccountDBServer account) => new()
    {
        Level = account.Level,
        FriendCode = AccountHandler.BuildFriendCode(account.ServerId),
        Comment = account.Comment,
        LastConnectTime = account.GameSettings.ServerDateTime(),
        RepresentCharacterUniqueId = RepresentCharacterUniqueId(db, account),
        RepresentCharacterCostumeId = RepresentCharacterUniqueId(db, account),
        SearchPermission = true,
        ShowAccountLevel = true,
        ShowFriendCode = true
    };

    private static bool InLevelBand(int level, FriendSearchLevelOption option) => option switch
    {
        FriendSearchLevelOption.Recommend or FriendSearchLevelOption.All => true,
        FriendSearchLevelOption.Level1To30 => level is >= 1 and <= 30,
        FriendSearchLevelOption.Level31To40 => level is >= 31 and <= 40,
        FriendSearchLevelOption.Level41To50 => level is >= 41 and <= 50,
        FriendSearchLevelOption.Level51To60 => level is >= 51 and <= 60,
        FriendSearchLevelOption.Level61To70 => level is >= 61 and <= 70,
        FriendSearchLevelOption.Level71To80 => level is >= 71 and <= 80,
        FriendSearchLevelOption.Level81To90 => level is >= 81 and <= 90,
        FriendSearchLevelOption.Level91To100 => level is >= 91 and <= 100,
        _ => true
    };
}
