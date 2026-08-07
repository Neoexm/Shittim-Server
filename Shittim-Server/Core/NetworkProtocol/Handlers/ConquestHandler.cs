using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Managers;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers;

public class ConquestHandler : ProtocolHandlerBase
{
    private const int MaxManagePerRequest = 100;

    private readonly ISessionKeyService _sessionService;
    private readonly ConquestManager _conquestManager;
    private readonly ExcelTableService _excelService;
    private readonly ParcelHandler _parcelHandler;

    public ConquestHandler(
        IProtocolHandlerRegistry registry,
        ISessionKeyService sessionService,
        ConquestManager conquestManager,
        ExcelTableService excelService,
        ParcelHandler parcelHandler) : base(registry)
    {
        _sessionService = sessionService;
        _conquestManager = conquestManager;
        _excelService = excelService;
        _parcelHandler = parcelHandler;
    }

    [ProtocolHandler(Protocol.Conquest_GetInfo)]
    public async Task<ConquestGetInfoResponse> GetInfo(
        SchaleDataContext db,
        ConquestGetInfoRequest request,
        ConquestGetInfoResponse response)
    {
        var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

        var info = _conquestManager.GetOrCreate(db, account, request.EventContentId);

        response.IsFirstEnter = !info.FirstEnterDone;
        if (!info.FirstEnterDone)
        {
            info.FirstEnterDone = true;
            db.ConquestInfos.Update(info);
            await db.SaveChangesAsync();
        }

        response.ConquestInfoDB = info.ToInfoDB(_conquestManager.CalculateConditionAmount(info.EventContentId));
        response.ConquestedTileDBs = info.Tiles;
        response.ConquestEchelonDBs = info.Echelons;
        response.DifficultyToStepDict = info.StepByDifficulty;

        return response;
    }

    private ConquestStageSaveDB StartTileBattle(
        SchaleDataContext db, AccountDBServer account, ConquestInfoDBServer info,
        StageDifficulty difficulty, long tileUniqueId)
    {
        var tileExcel = _conquestManager.RequireTile(info.EventContentId, tileUniqueId);

        if (tileExcel.Step > _conquestManager.CurrentStep(info, difficulty))
            throw new WebAPIException(WebAPIErrorCode.ConquestStepNotOpened, $"Tile {tileUniqueId} is on step {tileExcel.Step}");
        if (_conquestManager.StoredTile(info, difficulty, tileUniqueId) != null)
            throw new WebAPIException(WebAPIErrorCode.ConquestAlreadyConquested, $"Tile {tileUniqueId} already taken");
        if (tileExcel.TileType != ConquestTileType.Battle)
            throw new WebAPIException(WebAPIErrorCode.ConquestInvalidTileType, $"Tile {tileUniqueId} has no battle");

        return _conquestManager.OpenTileBattle(db, account, info, difficulty, tileUniqueId, tileExcel.TileType);
    }

    private ConquestSummary BuildSummary(ConquestInfoDBServer info)
    {
        var tileExcels = _excelService.GetTable<ConquestTileExcelT>()
            .Where(x => x.EventId == info.EventContentId && x.Playable)
            .ToList();
        var currentStep = _conquestManager.CurrentStep(info, StageDifficulty.Normal);
        var conquered = info.Tiles
            .Where(x => x.Difficulty == StageDifficulty.Normal)
            .Select(x => x.TileUniqueId)
            .ToHashSet();

        return new ConquestSummary
        {
            EventContentId = info.EventContentId,
            Difficulty = StageDifficulty.Normal,
            ConquestStepSummaryDict = tileExcels
                .GroupBy(x => x.Step)
                .ToDictionary(g => g.Key, g => new ConquestStepSummary
                {
                    ConqueredTileCount = g.Count(x => conquered.Contains(x.Id)),
                    AllTileCount = g.Count(),
                    IsStepOpen = g.Key <= currentStep
                })
        };
    }
}
