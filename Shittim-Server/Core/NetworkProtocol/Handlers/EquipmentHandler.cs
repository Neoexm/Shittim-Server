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

public class EquipmentHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly EquipmentManager _equipmentManager;
    private readonly IMapper _mapper;

    public EquipmentHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        EquipmentManager equipmentManager,
        IMapper mapper) : base(registry)
    {
        _sessionService = sessionService;
        _equipmentManager = equipmentManager;
        _mapper = mapper;
    }

    [ProtocolHandler(Protocol.Equipment_List)]
    public async Task<EquipmentItemListResponse> List(
        SchaleDataContext db,
        EquipmentItemListRequest request,
        EquipmentItemListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.EquipmentDBs = db.GetAccountEquipments(account.ServerId).ToMapList(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Equipment_Equip)]
    public async Task<EquipmentItemEquipResponse> Equip(
        SchaleDataContext db,
        EquipmentItemEquipRequest request,
        EquipmentItemEquipResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (equippedCharacter, originalStack, newEquipment) = await _equipmentManager.EquipmentEquip(db, account, request);

        response.CharacterDB = equippedCharacter.ToMap(_mapper);
        response.EquipmentDBs = [newEquipment.ToMap(_mapper), originalStack.ToMap(_mapper)];

        return response;
    }

    [ProtocolHandler(Protocol.Equipment_LevelUp)]
    public async Task<EquipmentItemLevelUpResponse> LevelUp(
        SchaleDataContext db,
        EquipmentItemLevelUpRequest request,
        EquipmentItemLevelUpResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (targetEquipment, consumeResult) = await _equipmentManager.EquipmentLevelUp(db, account, request);

        response.EquipmentDB = targetEquipment.ToMap(_mapper);
        response.ConsumeResultDB = consumeResult;

        return response;
    }

    [ProtocolHandler(Protocol.Equipment_TierUp)]
    public async Task<EquipmentItemTierUpResponse> TierUp(
        SchaleDataContext db,
        EquipmentItemTierUpRequest request,
        EquipmentItemTierUpResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (targetEquipment, parcelResult) = await _equipmentManager.EquipmentTierUp(db, account, request);

        response.EquipmentDB = targetEquipment.ToMap(_mapper);
        response.ParcelResultDB = parcelResult;

        return response;
    }

    [ProtocolHandler(Protocol.Equipment_BatchGrowth)]
    public async Task<EquipmentBatchGrowthResponse> BatchGrowth(
        SchaleDataContext db,
        EquipmentBatchGrowthRequest request,
        EquipmentBatchGrowthResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (equipmentDBs, gearDB, consumeResultDB, parcelResultDB) = await _equipmentManager.EquipmentBatchGrowth(db, account, request);

        response.EquipmentDBs = equipmentDBs.ToMapList(_mapper);
        response.GearDB = gearDB?.ToMap(_mapper);
        response.ConsumeResultDB = consumeResultDB;
        response.ParcelResultDB = parcelResultDB;

        return response;
    }
}
