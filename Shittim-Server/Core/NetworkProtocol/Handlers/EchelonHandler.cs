using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.MX.NetworkProtocol;
using Schale.FlatData;
using Shittim_Server.Core;
using Shittim_Server.Managers;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class EchelonHandler : ProtocolHandlerBase
{
    private readonly ISessionKeyService _sessionService;
    private readonly IMapper _mapper;
    private readonly EchelonManager _echelonManager;

    public EchelonHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        IMapper mapper,
        EchelonManager echelonManager) : base(registry)
    {
        _sessionService = sessionService;
        _mapper = mapper;
        _echelonManager = echelonManager;
    }

    [ProtocolHandler(Protocol.Echelon_List)]
    public async Task<EchelonListResponse> List(
        SchaleDataContext db,
        EchelonListRequest request,
        EchelonListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.EchelonDBs = db.GetAccountEchelons(account.ServerId).ToMapList(_mapper);
        response.ArenaEchelonDB = new();
        if (account.GameSettings.EnableMultiFloorRaid)
            response.ServerTimeTicks = MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.Echelon_Save)]
    public async Task<EchelonSaveResponse> Save(
        SchaleDataContext db,
        EchelonSaveRequest request,
        EchelonSaveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        await _echelonManager.SaveEchelon(db, account, request.EchelonDB);

        response.EchelonDB = request.EchelonDB;
        if (account.GameSettings.EnableMultiFloorRaid)
            response.ServerTimeTicks = MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.Echelon_PresetList)]
    public async Task<EchelonPresetListResponse> PresetList(
        SchaleDataContext db,
        EchelonPresetListRequest request,
        EchelonPresetListResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        response.PresetGroupDBs = db.GetAccountEchelonPresetGroups(account.ServerId)
            .Where(g => g.ExtensionType == request.EchelonExtensionType)
            .ToMapList(_mapper).ToArray();
        if (account.GameSettings.EnableMultiFloorRaid)
            response.ServerTimeTicks = MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.Echelon_PresetSave)]
    public async Task<EchelonPresetSaveResponse> PresetSave(
        SchaleDataContext db,
        EchelonPresetSaveRequest request,
        EchelonPresetSaveResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        await _echelonManager.SaveEchelonPreset(db, account, request.PresetDB);

        response.PresetDB = request.PresetDB;
        if (account.GameSettings.EnableMultiFloorRaid)
            response.ServerTimeTicks = MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks;

        return response;
    }

    [ProtocolHandler(Protocol.Echelon_PresetGroupRename)]
    public async Task<EchelonPresetGroupRenameResponse> PresetGroupRename(
        SchaleDataContext db,
        EchelonPresetGroupRenameRequest request,
        EchelonPresetGroupRenameResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var presetGroup = await db.EchelonPresetGroups
            .FirstOrDefaultAsync(g => g.AccountServerId == account.ServerId 
                && g.GroupIndex == request.PresetGroupIndex 
                && g.ExtensionType == request.ExtensionType);

        if (presetGroup != null)
        {
            presetGroup.GroupLabel = request.PresetGroupLabel;
            await db.SaveChangesAsync();

            response.PresetGroupDB = presetGroup.ToMap(_mapper);
        }
        if (account.GameSettings.EnableMultiFloorRaid)
            response.ServerTimeTicks = MultiFloorRaidHandler.MultiFloorRaidDateTime.Ticks;

        return response;
    }
}
