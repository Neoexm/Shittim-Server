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
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class CafeHandler : ProtocolHandlerBase
{
    private readonly SessionKeyService _sessionService;
    private readonly ExcelTableService _excelService;
    private readonly IMapper _mapper;
    private readonly CafeManager _cafeManager;

    public CafeHandler(
        IProtocolHandlerRegistry registry,
        SessionKeyService sessionService,
        ExcelTableService excelService,
        IMapper mapper,
        CafeManager cafeManager) : base(registry)
    {
        _sessionService = sessionService;
        _excelService = excelService;
        _mapper = mapper;
        _cafeManager = cafeManager;
    }

    [ProtocolHandler(Protocol.Cafe_Get)]
    public async Task<CafeGetInfoResponse> Get(
        SchaleDataContext db,
        CafeGetInfoRequest request,
        CafeGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var cafes = db.GetAccountCafes(account.ServerId).ToList();
        var furnitures = db.GetAccountFurnitures(account.ServerId).ToList();

        response.CafeDBs = cafes.ToMapList(_mapper);
        response.FurnitureDBs = furnitures.ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_Ack)]
    public async Task<CafeAckResponse> Ack(
        SchaleDataContext db,
        CafeAckRequest request,
        CafeAckResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var cafe = await _cafeManager.CafeAck(db, account, request.CafeDBId);
        response.CafeDB = cafe.ToMap(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_Open)]
    public async Task<CafeOpenResponse> Open(
        SchaleDataContext db,
        CafeOpenRequest request,
        CafeOpenResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.OpenedCafeDB = db.Cafes.GetCafeById(account.ServerId, request.CafeId).ToMap(_mapper);
        response.FurnitureDBs = db.GetAccountFurnitures(account.ServerId).ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_RankUp)]
    public async Task<CafeRankUpResponse> RankUp(
        SchaleDataContext db,
        CafeRankUpRequest request,
        CafeRankUpResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_Deploy)]
    public async Task<CafeDeployFurnitureResponse> Deploy(
        SchaleDataContext db,
        CafeDeployFurnitureRequest request,
        CafeDeployFurnitureResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (cafeDb, placedFurniture, inventoryFurniture) = await _cafeManager.CafeDeployFurniture(db, account, request);

        response.CafeDB = cafeDb.ToMap(_mapper);
        response.NewFurnitureServerId = placedFurniture.ServerId;
        response.ChangedFurnitureDBs = [placedFurniture.ToMap(_mapper), inventoryFurniture.ToMap(_mapper)];

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_Relocate)]
    public async Task<CafeRelocateFurnitureResponse> Relocate(
        SchaleDataContext db,
        CafeRelocateFurnitureRequest request,
        CafeRelocateFurnitureResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (cafeDb, furniture) = await _cafeManager.CafeRelocateFurniture(db, account, request);

        response.CafeDB = cafeDb.ToMap(_mapper);
        response.RelocatedFurnitureDB = furniture.ToMap(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_Remove)]
    public async Task<CafeRemoveFurnitureResponse> Remove(
        SchaleDataContext db,
        CafeRemoveFurnitureRequest request,
        CafeRemoveFurnitureResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (cafeDb, removedFurniture) = await _cafeManager.CafeRemoveFurniture(db, account, request);

        response.CafeDB = cafeDb.ToMap(_mapper);
        response.FurnitureDBs = removedFurniture.ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_RemoveAll)]
    public async Task<CafeRemoveAllFurnitureResponse> RemoveAll(
        SchaleDataContext db,
        CafeRemoveAllFurnitureRequest request,
        CafeRemoveAllFurnitureResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (cafeDb, allFurnitures) = await _cafeManager.CafeRemoveAllFurniture(db, account, request);

        response.CafeDB = cafeDb.ToMap(_mapper);
        response.FurnitureDBs = allFurnitures.ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_SummonCharacter)]
    public async Task<CafeSummonCharacterResponse> SummonCharacter(
        SchaleDataContext db,
        CafeSummonCharacterRequest request,
        CafeSummonCharacterResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var cafeDb = await _cafeManager.CafeSummonCharacter(db, account, request);

        response.CafeDB = cafeDb.ToMap(_mapper);
        response.CafeDBs = db.GetAccountCafes(account.ServerId).ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_Interact)]
    public async Task<CafeInteractWithCharacterResponse> Interact(
        SchaleDataContext db,
        CafeInteractWithCharacterRequest request,
        CafeInteractWithCharacterResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (cafeDb, character, parcelResult) = await _cafeManager.CafeInteractWithCharacter(db, account, request);

        response.CafeDB = cafeDb.ToMap(_mapper);
        response.CharacterDB = character;
        response.ParcelResultDB = parcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_GiveGift)]
    public async Task<CafeGiveGiftResponse> GiveGift(
        SchaleDataContext db,
        CafeGiveGiftRequest request,
        CafeGiveGiftResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (consumeResult, parcelResultList) = await _cafeManager.CafeGiveGift(db, account, request);

        response.ConsumeResultDB = consumeResult;
        response.ParcelResultDB = parcelResultList;

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_ReceiveCurrency)]
    public async Task<CafeReceiveCurrencyResponse> ReceiveCurrency(
        SchaleDataContext db,
        CafeReceiveCurrencyRequest request,
        CafeReceiveCurrencyResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_ListPreset)]
    public async Task<CafeListPresetResponse> ListPreset(
        SchaleDataContext db,
        CafeListPresetRequest request,
        CafeListPresetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_ApplyPreset)]
    public async Task<CafeApplyPresetResponse> ApplyPreset(
        SchaleDataContext db,
        CafeApplyPresetRequest request,
        CafeApplyPresetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_ApplyTemplate)]
    public async Task<CafeApplyTemplateResponse> ApplyTemplate(
        SchaleDataContext db,
        CafeApplyTemplateRequest request,
        CafeApplyTemplateResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (cafeDb, allFurnitures) = await _cafeManager.CafeApplyTemplate(db, account, request);

        response.CafeDBs = cafeDb.ToMapList(_mapper);
        response.FurnitureDBs = allFurnitures.ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_RenamePreset)]
    public async Task<CafeRenamePresetResponse> RenamePreset(
        SchaleDataContext db,
        CafeRenamePresetRequest request,
        CafeRenamePresetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_ClearPreset)]
    public async Task<CafeClearPresetResponse> ClearPreset(
        SchaleDataContext db,
        CafeClearPresetRequest request,
        CafeClearPresetResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_TrophyHistory)]
    public async Task<CafeTrophyHistoryResponse> TrophyHistory(
        SchaleDataContext db,
        CafeTrophyHistoryRequest request,
        CafeTrophyHistoryResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }

    [ProtocolHandler(Protocol.Cafe_UpdatePresetFurniture)]
    public async Task<CafeUpdatePresetFurnitureResponse> UpdatePresetFurniture(
        SchaleDataContext db,
        CafeUpdatePresetFurnitureRequest request,
        CafeUpdatePresetFurnitureResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        return response;
    }
}
