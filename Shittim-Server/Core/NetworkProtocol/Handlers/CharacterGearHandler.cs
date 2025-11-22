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

public class CharacterGearHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly GearManager _gearManager;
    private readonly IMapper _mapper;

    public CharacterGearHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        GearManager gearManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _gearManager = gearManager;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.CharacterGear_List)]
    public async Task<CharacterGearListResponse> List(
        SchaleDataContext db,
        CharacterGearListRequest request,
        CharacterGearListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var gears = db.Gears.Where(x => x.AccountServerId == account.ServerId);

        response.GearDBs = gears.ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.CharacterGear_Unlock)]
    public async Task<CharacterGearUnlockResponse> Unlock(
        SchaleDataContext db,
        CharacterGearUnlockRequest request,
        CharacterGearUnlockResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (gear, targetCharacter) = await _gearManager.GearUnlock(db, account, request);

        response.GearDB = gear.ToMap(_mapper);
        response.CharacterDB = targetCharacter.ToMap(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.CharacterGear_TierUp)]
    public async Task<CharacterGearTierUpResponse> TierUp(
        SchaleDataContext db,
        CharacterGearTierUpRequest request,
        CharacterGearTierUpResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (targetGear, parcelResult) = await _gearManager.GearTierUp(db, account, request);

        response.GearDB = targetGear.ToMap(_mapper);
        response.ParcelResultDB = parcelResult;

        return response;
    }
}
