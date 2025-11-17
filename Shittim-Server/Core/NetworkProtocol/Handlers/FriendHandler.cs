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
    private readonly SessionKeyService _sessionService;
    private readonly IMapper _mapper;

    public FriendHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
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
                    LastConnectTime = DateTime.Now,
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

        return response;
    }
}
