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
using Shittim_Server.Services;
using Microsoft.Extensions.Configuration;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ClanHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;
    private readonly IMapper _mapper;
    private readonly AronaService _aronaService;
    private readonly IConfiguration _configuration;

    public ClanHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        IMapper mapper,
        AronaService aronaService,
        IConfiguration configuration) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
        _aronaService = aronaService;
        _configuration = configuration;
    }

    [ProtocolHandler(Protocol.Clan_Lobby)]
    public async Task<ClanLobbyResponse> Lobby(
        SchaleDataContext db,
        ClanLobbyRequest request,
        ClanLobbyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var aronaAccount = db.Accounts.FirstOrDefault(x => x.DevId == AronaAI.AccountDevId);

        response.IrcConfig = new IrcServerConfig
        {
            HostAddress = _configuration["Irc:Address"] ?? "localhost",
            Port = int.Parse(_configuration["Irc:Port"] ?? "6667"),
            Password = _configuration["Irc:Password"] ?? ""
        };

        response.AccountClanDB = new ClanDB
        {
            ClanDBId = 777,
            ClanName = "Schale Network",
            ClanChannelName = "channel_1",
            ClanPresidentNickName = aronaAccount?.Nickname ?? "Arona",
            ClanPresidentRepresentCharacterUniqueId = aronaAccount?.RepresentCharacterServerId ?? 19900006,
            ClanPresidentRepresentCharacterCostumeId = aronaAccount?.RepresentCharacterServerId ?? 19900006,
            ClanNotice = "Welcome to Schale Network\n\nEnjoy your stay, Sensei!",
            ClanJoinOption = ClanJoinOption.Free,
            ClanMemberCount = 2
        };

        var accountCharacter = db.Characters.FirstOrDefault(c => c.ServerId == account.RepresentCharacterServerId);
        var accountAttachment = db.GetAccountAttachments(account.ServerId).FirstOrDefault();

        response.AccountClanMemberDB = new ClanMemberDB
        {
            AccountId = account.ServerId,
            AccountLevel = account.Level,
            ClanDBId = 777,
            RepresentCharacterUniqueId = accountCharacter?.UniqueId ?? account.RepresentCharacterServerId,
            ClanSocialGrade = ClanSocialGrade.Member,
            AccountNickName = account.Nickname ?? "Sensei",
            AttendanceCount = 33,
            GameLoginDate = account.LastConnectTime,
            LastLoginDate = account.LastConnectTime,
            JoinDate = account.CreateDate,
            AttachmentDB = accountAttachment?.ToMap(_mapper)
        };

        var aronaAttachment = aronaAccount != null ? db.GetAccountAttachments(aronaAccount.ServerId).FirstOrDefault() : null;

        response.ClanMemberDBs = new List<ClanMemberDB>
        {
            new ClanMemberDB
            {
                AccountId = aronaAccount?.ServerId ?? 100000,
                AccountLevel = aronaAccount?.Level ?? 90,
                ClanDBId = 777,
                RepresentCharacterUniqueId = aronaAccount?.RepresentCharacterServerId ?? 19900006,
                ClanSocialGrade = ClanSocialGrade.President,
                AccountNickName = aronaAccount?.Nickname ?? "Arona",
                GameLoginDate = aronaAccount?.LastConnectTime ?? DateTime.UtcNow,
                LastLoginDate = aronaAccount?.LastConnectTime ?? DateTime.UtcNow,
                JoinDate = aronaAccount?.CreateDate ?? DateTime.UtcNow,
                AttachmentDB = aronaAttachment?.ToMap(_mapper) ?? new AccountAttachmentDB
                {
                    AccountId = aronaAccount?.ServerId ?? 100000,
                    EmblemUniqueId = aronaAccount?.RepresentCharacterServerId ?? 19900006
                }
            },
            response.AccountClanMemberDB
        };

        return response;
    }

    [ProtocolHandler(Protocol.Clan_Check)]
    public async Task<ClanCheckResponse> Check(
        SchaleDataContext db,
        ClanCheckRequest request,
        ClanCheckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Clan_AllAssistList)]
    public async Task<ClanAllAssistListResponse> AllAssistList(
        SchaleDataContext db,
        ClanAllAssistListRequest request,
        ClanAllAssistListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.ClanDBId = 777;
        response.AssistCharacterDBs = _aronaService.GetAssistCharacter(request.EchelonType);
        response.AssistCharacterRentHistoryDBs = [];

        return response;
    }
}
