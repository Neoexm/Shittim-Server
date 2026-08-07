using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.NetworkProtocol;
using Schale.MX.GameLogic.Parcel;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Managers;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class CharacterHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly CharacterManager _characterManager;
    private readonly IMapper _mapper;
    private readonly ConsumeHandler _consumeHandler;
    private readonly ParcelHandler _parcelHandler;

    public CharacterHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        CharacterManager characterManager,
        IMapper mapper,
        ConsumeHandler consumeHandler,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _characterManager = characterManager;
        _mapper = mapper;
        _consumeHandler = consumeHandler;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.Character_FavorGrowth)]
    public async Task<CharacterFavorGrowthResponse> FavorGrowth(
        SchaleDataContext db,
        CharacterFavorGrowthRequest request,
        CharacterFavorGrowthResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var character = db.Characters.FirstOrDefault(x =>
                x.AccountServerId == account.ServerId && x.ServerId == request.TargetCharacterDBId)
            ?? throw new WebAPIException(WebAPIErrorCode.CharacterNotFound,
                $"Character {request.TargetCharacterDBId} not found");

        // Same pipeline as the cafe gift: the consume side accumulates the favor exp the items carry,
        // and the FavorExp parcel applies it (including any rank-ups) to the character.
        var consumeData = await _consumeHandler.BuildConsumeResult(db, account, new ConsumeRequestDB
        {
            ConsumeItemServerIdAndCounts = (request.ConsumeItemDBIdsAndCounts ?? [])
                .ToDictionary(kv => kv.Key, kv => (long)kv.Value),
            IsItemsValid = true,
            IsValid = true
        });

        var favorResolver = await _parcelHandler.BuildParcel(db, account,
            new ParcelResult(ParcelType.FavorExp, character.UniqueId, consumeData.AccumulatedExp));

        response.CharacterDB = favorResolver.ParcelResult.CharacterDBs?.FirstOrDefault(x => x.ServerId == character.ServerId)
            ?? character.ToMap(_mapper);
        response.ConsumeStackableItemDBResult = consumeData.ParcelResult.ItemDBs?.Values.ToList() ?? [];
        response.ParcelResultDB = favorResolver.ParcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.Character_SetCostume)]
    public async Task<CharacterSetCostumeResponse> SetCostume(
        SchaleDataContext db,
        CharacterSetCostumeRequest request,
        CharacterSetCostumeResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var character = db.Characters.FirstOrDefault(x =>
                x.AccountServerId == account.ServerId && x.UniqueId == request.CharacterUniqueId)
            ?? throw new WebAPIException(WebAPIErrorCode.CharacterNotFound,
                $"Character {request.CharacterUniqueId} not found");

        var costumes = db.GetAccountCostumes(account.ServerId).ToList();

        var current = costumes.FirstOrDefault(x => x.BoundCharacterServerId == character.ServerId);
        if (current != null)
        {
            current.BoundCharacterServerId = 0;
            response.UnsetCostumeDB = current.ToMap(_mapper);
        }

        if (request.CostumeIdToSet is long costumeId)
        {
            var target = costumes.FirstOrDefault(x => x.UniqueId == costumeId)
                ?? throw new WebAPIException(WebAPIErrorCode.CharacterCostumeNotFound,
                    $"Costume {costumeId} not owned");

            target.BoundCharacterServerId = character.ServerId;
            response.SetCostumeDB = target.ToMap(_mapper);
        }

        await db.SaveChangesAsync();
        return response;
    }

    [ProtocolHandler(Protocol.Character_List)]
    public async Task<CharacterListResponse> List(
        SchaleDataContext db,
        CharacterListRequest request,
        CharacterListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.CharacterDBs = db.GetAccountCharacters(account.ServerId).ToMapList(_mapper);
        response.TSSCharacterDBs = [];
        response.WeaponDBs = db.GetAccountWeapons(account.ServerId).ToMapList(_mapper);
        response.CostumeDBs = db.GetAccountCostumes(account.ServerId).ToMapList(_mapper);

        return response;
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
