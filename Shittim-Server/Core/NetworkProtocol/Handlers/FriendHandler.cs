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

        response.FriendDBs = [];
        response.SentRequestFriendDBs = [];
        response.ReceivedRequestFriendDBs = [];

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

            var friendDbs = new List<FriendDB>();

            foreach (var targetAccount in targetAccounts)
            {
                var attachment = db.GetAccountAttachments(targetAccount.ServerId).FirstOrDefault();

                friendDbs.Add(new FriendDB
                {
                    AccountId = targetAccount.ServerId,
                    Nickname = targetAccount.Nickname ?? "Sensei",
                    Level = targetAccount.Level,
                    RepresentCharacterUniqueId = targetAccount.RepresentCharacterServerId,
                    RepresentCharacterCostumeId = targetAccount.RepresentCharacterServerId,
                    LastConnectTime = account.GameSettings.ServerDateTime(),
                    ComfortValue = 10000,
                    FriendCount = 0,
                    AttachmentDB = attachment != null ? _mapper.Map<AccountAttachmentDB>(attachment) : null
                });
            }

            response.ListResult = friendDbs.ToArray();
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

        // the stored card only holds what SetIdCard writes; the live bits come off the account
        var card = account.ContentInfo.IdCard;
        card.Level = account.Level;
        card.FriendCode = AccountHandler.BuildFriendCode(account.ServerId);
        card.LastConnectTime = account.LastConnectTime;
        if (card.RepresentCharacterUniqueId == 0)
            card.RepresentCharacterUniqueId = account.RepresentCharacterServerId;

        response.FriendIdCardDB = card;

        return response;
    }

    [ProtocolHandler(Protocol.Friend_SetIdCard)]
    public async Task<FriendSetIdCardResponse> SetIdCard(
        SchaleDataContext db,
        FriendSetIdCardRequest request,
        FriendSetIdCardResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var card = account.ContentInfo.IdCard;
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

        db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
        await db.SaveChangesAsync();

        return response;
    }

    [ProtocolHandler(Protocol.Friend_Search)]
    public async Task<FriendSearchResponse> Search(
        SchaleDataContext db,
        FriendSearchRequest request,
        FriendSearchResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        // friend codes are derived from the server id rather than stored, so re-derive per account; there are never enough accounts here for the scan to hurt
        var code = request.FriendCode?.ToUpperInvariant();
        var match = db.Accounts.AsEnumerable().FirstOrDefault(a => a.ServerId != account.ServerId && AccountHandler.BuildFriendCode(a.ServerId) == code);

        if (match == null)
        {
            response.SearchResult = [];
            return response;
        }

        var attachment = db.GetAccountAttachments(match.ServerId).FirstOrDefault();

        response.SearchResult =
        [
            new FriendDB
            {
                AccountId = match.ServerId,
                Nickname = match.Nickname ?? "Sensei",
                Level = match.Level,
                RepresentCharacterUniqueId = match.RepresentCharacterServerId,
                RepresentCharacterCostumeId = match.RepresentCharacterServerId,
                LastConnectTime = account.GameSettings.ServerDateTime(),
                ComfortValue = 10000,
                FriendCount = 0,
                AttachmentDB = attachment != null ? _mapper.Map<AccountAttachmentDB>(attachment) : null
            }
        ];

        return response;
    }

    [ProtocolHandler(Protocol.Friend_GetFriendDetailedInfo)]
    public async Task<FriendGetFriendDetailedInfoResponse> GetFriendDetailedInfo(
        SchaleDataContext db,
        FriendGetFriendDetailedInfoRequest request,
        FriendGetFriendDetailedInfoResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var target = db.GetAccount(request.FriendAccountId);
        var attachment = db.GetAccountAttachments(target.ServerId).FirstOrDefault();

        response.Nickname = target.Nickname;
        response.Level = target.Level;
        response.ClanName = "Schale Network";
        response.Comment = target.Comment;
        response.FriendCount = 0;
        response.FriendCode = AccountHandler.BuildFriendCode(target.ServerId);
        response.RepresentCharacterUniqueId = target.RepresentCharacterServerId;
        response.RepresentCharacterCostumeId = target.RepresentCharacterServerId;
        response.CharacterCount = db.GetAccountCharacters(target.ServerId).Count();
        response.AttachmentDB = attachment != null ? _mapper.Map<AccountAttachmentDB>(attachment) : null;
        response.AssistCharacterDBs = [];

        response.DetailedAccountInfoDB = new DetailedAccountInfoDB
        {
            AccountId = target.ServerId,
            Nickname = target.Nickname,
            Level = target.Level,
            ClanName = "Schale Network",
            Comment = target.Comment,
            FriendCode = response.FriendCode,
            RepresentCharacterUniqueId = target.RepresentCharacterServerId,
            CharacterCount = response.CharacterCount,
            AssistCharacterDBs = []
        };

        return response;
    }

    // no friend rows exist server-side, so every list mutation settles to the same empty lists

    [ProtocolHandler(Protocol.Friend_Remove)]
    public async Task<FriendRemoveResponse> Remove(
        SchaleDataContext db,
        FriendRemoveRequest request,
        FriendRemoveResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.FriendDBs = [];
        response.SentRequestFriendDBs = [];
        response.ReceivedRequestFriendDBs = [];
        response.BlockedUserDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Friend_AcceptFriendRequest)]
    public async Task<FriendAcceptFriendRequestResponse> AcceptFriendRequest(
        SchaleDataContext db,
        FriendAcceptFriendRequestRequest request,
        FriendAcceptFriendRequestResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.FriendDBs = [];
        response.SentRequestFriendDBs = [];
        response.ReceivedRequestFriendDBs = [];
        response.BlockedUserDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Friend_DeclineFriendRequest)]
    public async Task<FriendDeclineFriendRequestResponse> DeclineFriendRequest(
        SchaleDataContext db,
        FriendDeclineFriendRequestRequest request,
        FriendDeclineFriendRequestResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.FriendDBs = [];
        response.SentRequestFriendDBs = [];
        response.ReceivedRequestFriendDBs = [];
        response.BlockedUserDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Friend_CancelFriendRequest)]
    public async Task<FriendCancelFriendRequestResponse> CancelFriendRequest(
        SchaleDataContext db,
        FriendCancelFriendRequestRequest request,
        FriendCancelFriendRequestResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.FriendDBs = [];
        response.SentRequestFriendDBs = [];
        response.ReceivedRequestFriendDBs = [];
        response.BlockedUserDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Friend_Block)]
    public async Task<FriendBlockResponse> Block(
        SchaleDataContext db,
        FriendBlockRequest request,
        FriendBlockResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.FriendDBs = [];
        response.SentRequestFriendDBs = [];
        response.ReceivedRequestFriendDBs = [];
        response.BlockedUserDBs = [];

        return response;
    }

    [ProtocolHandler(Protocol.Friend_Unblock)]
    public async Task<FriendUnblockResponse> Unblock(
        SchaleDataContext db,
        FriendUnblockRequest request,
        FriendUnblockResponse response)
    {
        await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.FriendDBs = [];
        response.SentRequestFriendDBs = [];
        response.ReceivedRequestFriendDBs = [];
        response.BlockedUserDBs = [];

        return response;
    }
}
