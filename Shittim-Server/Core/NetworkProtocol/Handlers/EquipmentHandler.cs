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
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class EquipmentHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly EquipmentManager _equipmentManager;
    private readonly IMapper _mapper;
    private readonly MissionService _missionService;

    public EquipmentHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        EquipmentManager equipmentManager,
        IMapper mapper,
        MissionService missionService) : base(registry)
    {
        _sessionService = sessionService;
        _equipmentManager = equipmentManager;
        _mapper = mapper;
        _missionService = missionService;
    }

    [ProtocolHandler(Protocol.Equipment_Lock)]
    public async Task<EquipmentItemLockResponse> Lock(
        SchaleDataContext db,
        EquipmentItemLockRequest request,
        EquipmentItemLockResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var equipment = db.GetAccountEquipments(account.ServerId).FirstOrDefault(x => x.ServerId == request.TargetServerId)
            ?? throw new WebAPIException(WebAPIErrorCode.EquipmentNotFound, $"Equipment {request.TargetServerId} not found");
        equipment.IsLocked = request.IsLocked;
        await db.SaveChangesAsync();

        response.EquipmentDB = equipment.ToMap(_mapper);

        return response;
    }

    [ProtocolHandler(Protocol.Equipment_Sell)]
    public async Task<EquipmentItemSellResponse> Sell(
        SchaleDataContext db,
        EquipmentItemSellRequest request,
        EquipmentItemSellResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var rows = new List<EquipmentDBServer>();
        foreach (var id in request.TargetServerIds ?? [])
        {
            var equipment = db.GetAccountEquipments(account.ServerId).FirstOrDefault(x => x.ServerId == id)
                ?? throw new WebAPIException(WebAPIErrorCode.EquipmentNotFound, $"Equipment {id} not found");
            if (equipment.IsLocked)
                throw new WebAPIException(WebAPIErrorCode.EquipmentLocked, $"Equipment {id} is locked");
            if (equipment.BoundCharacterServerId != 0)
                throw new WebAPIException(WebAPIErrorCode.EquipmentAlreadyEquiped, $"Equipment {id} is equipped");
            rows.Add(equipment);
        }

        db.Equipments.RemoveRange(rows);
        await db.SaveChangesAsync();

        // No gold credited: a payout needs a per-item or per-rarity gold value and none ships - not in any
        // ExcelDB table, not in the .bytes tables. The response carrying AccountCurrencyDB and nothing else
        // says the live server had one server-side, so credit here if such a value ever surfaces. The
        // item-to-currency GoodsExcel rows are not it: those are event-shop coin exchanges keyed by GoodsId.
        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstMapTo(_mapper);
        return response;
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
        // Official's level-up always returns the updated currency (feeding costs gold) and ticks the equipment-growth missions.
        response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefault()?.ToMap(_mapper);

        var updatedMissions = _missionService.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Achieve_EquipmentLevelUpCount);
        if (updatedMissions.Count > 0)
            response.MissionProgressDBs = updatedMissions;

        return response;
    }

    [ProtocolHandler(Protocol.Equipment_TierUp)]
    public async Task<EquipmentItemTierUpResponse> TierUp(
        SchaleDataContext db,
        EquipmentItemTierUpRequest request,
        EquipmentItemTierUpResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var (targetEquipment, parcelResult, consumeResult) = await _equipmentManager.EquipmentTierUp(db, account, request);

        response.EquipmentDB = targetEquipment.ToMap(_mapper);
        response.ParcelResultDB = parcelResult;
        response.ConsumeResultDB = consumeResult;

        var updatedMissions = _missionService.UpdateMissionProgress(
            db, account, MissionCompleteConditionType.Achieve_EquipmentTierUpCount);
        if (updatedMissions.Count > 0)
            response.MissionProgressDBs = updatedMissions;

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
