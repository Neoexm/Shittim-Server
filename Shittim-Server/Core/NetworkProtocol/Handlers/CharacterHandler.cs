using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Managers;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class CharacterHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;
    private readonly CharacterManager _characterManager;
    private readonly IMapper _mapper;

    public CharacterHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        CharacterManager characterManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _characterManager = characterManager;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.Character_SetFavorites)]
    public async Task<CharacterSetFavoritesResponse> SetFavorites(
        SchaleDataContext db,
        CharacterSetFavoritesRequest request,
        CharacterSetFavoritesResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var favoriteChars = await _characterManager.CharacterSetFavorite(db, account, request.ActivateByServerIds);
        response.CharacterDBs = favoriteChars.Select(x => x.ToMap(_mapper)).ToList();

        return response;
    }

    [ProtocolHandler(Protocol.Character_UpdateSkillLevel)]
    public async Task<CharacterSkillLevelUpdateResponse> UpdateSkillLevel(
        SchaleDataContext db,
        CharacterSkillLevelUpdateRequest request,
        CharacterSkillLevelUpdateResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (targetCharacter, parcelResult) = await _characterManager.CharacterUpdateSkillLevel(db, account, request);
        
        response.CharacterDB = targetCharacter.ToMap(_mapper);
        response.ParcelResultDB = parcelResult;
        
        return response;
    }

    [ProtocolHandler(Protocol.Character_BatchSkillLevelUpdate)]
    public async Task<CharacterBatchSkillLevelUpdateResponse> BatchUpdateSkillLevel(
        SchaleDataContext db,
        CharacterBatchSkillLevelUpdateRequest request,
        CharacterBatchSkillLevelUpdateResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (targetCharacter, parcelResult) = await _characterManager.CharacterBatchUpdateSkillLevel(db, account, request);
        
        response.CharacterDB = targetCharacter.ToMap(_mapper);
        response.ParcelResultDB = parcelResult;
        
        return response;
    }

    [ProtocolHandler(Protocol.Character_UnlockWeapon)]
    public async Task<CharacterUnlockWeaponResponse> UnlockWeapon(
        SchaleDataContext db,
        CharacterUnlockWeaponRequest request,
        CharacterUnlockWeaponResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var newWeapon = await _characterManager.UnlockWeapon(db, account, request.TargetCharacterServerId);

        response.WeaponDB = newWeapon.ToMap(_mapper);
        return response;
    }

    [ProtocolHandler(Protocol.Character_PotentialGrowth)]
    public async Task<CharacterPotentialGrowthResponse> PotentialGrowth(
        SchaleDataContext db,
        CharacterPotentialGrowthRequest request,
        CharacterPotentialGrowthResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (targetCharacter, consumeResult) = await _characterManager.PotentialGrowth(db, account, request);

        response.CharacterDB = targetCharacter.ToMap(_mapper);
        response.ParcelResultDB = consumeResult;
        
        return response;
    }

    [ProtocolHandler(Protocol.Character_ExpGrowth)]
    public async Task<CharacterExpGrowthResponse> ExpGrowth(
        SchaleDataContext db,
        CharacterExpGrowthRequest request,
        CharacterExpGrowthResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        var (targetCharacter, consumeResult, accountCurrency) = await _characterManager.CharacterGrowth(db, account, request);
        
        response.CharacterDB = targetCharacter.ToMap(_mapper);
        response.ConsumeResultDB = consumeResult;
        response.AccountCurrencyDB = accountCurrency.ToMap(_mapper);
        
        return response;
    }

    [ProtocolHandler(Protocol.Character_Transcendence)]
    public async Task<CharacterTranscendenceResponse> Transcendence(
        SchaleDataContext db,
        CharacterTranscendenceRequest request,
        CharacterTranscendenceResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
        
        var (targetCharacter, parcelResultDb) = await _characterManager.CharacterTranscendence(db, account, request);
        response.CharacterDB = targetCharacter.ToMap(_mapper);
        response.ParcelResultDB = parcelResultDb;
        
        return response;
    }

    [ProtocolHandler(Protocol.Character_WeaponExpGrowth)]
    public async Task<CharacterWeaponExpGrowthResponse> WeaponExpGrowth(
        SchaleDataContext db,
        CharacterWeaponExpGrowthRequest request,
        CharacterWeaponExpGrowthResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var parcelResult = await _characterManager.WeaponGrowth(db, account, request);
        response.ParcelResultDB = parcelResult;
        
        return response;
    }

    [ProtocolHandler(Protocol.Character_WeaponTranscendence)]
    public async Task<CharacterWeaponTranscendenceResponse> WeaponTranscendence(
        SchaleDataContext db,
        CharacterWeaponTranscendenceRequest request,
        CharacterWeaponTranscendenceResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var parcelResultDb = await _characterManager.WeaponTranscendence(db, account, request);
        response.ParcelResultDB = parcelResultDb;
        
        return response;
    }
}
