using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.NetworkProtocol;
using BlueArchiveAPI.Services;
using Shittim_Server.Services;

namespace Shittim_Server.Managers
{
    // Tiles are conquered (free ones directly, battle tiles through the start/result pair), bases produce
    // and upgrade, a step opens when its playable tiles are all taken, and the calculate gauge pays per threshold.
    // Nothing spawns erosion, unexpected enemies or treasure boxes, so the five object handlers are
    // guard-only. The blocker is not the data - ConquestObjectExcel, ConquestErosionExcel and their
    // condition columns are all populated - it is TypedJsonWrapper<T> (Schale/MX/Core/Services/Services.cs),
    // whose implicit operator drops the object and leaves JsonWithType empty. Every conquest response
    // carries its objects through that wrapper, so spawned rows could not reach the client. Implementing it
    // needs the client's exact $type token, which no capture here pins down.
    public class ConquestManager
    {
        private readonly ExcelTableService _excelService;
        private readonly ParcelHandler _parcelHandler;

        public ConquestManager(ExcelTableService excelService, ParcelHandler parcelHandler)
        {
            _excelService = excelService;
            _parcelHandler = parcelHandler;
        }

        public ConquestInfoDBServer GetOrCreate(SchaleDataContext db, AccountDBServer account, long eventContentId)
        {
            var hasEvent = _excelService.GetTable<ConquestEventExcelT>()
                .Any(x => x.EventContentId == eventContentId || x.MainStoryEventContentId == eventContentId);
            if (!hasEvent && !_excelService.GetTable<ConquestTileExcelT>().Any(x => x.EventId == eventContentId))
                throw new WebAPIException(WebAPIErrorCode.ConquestDataNotFound, $"No conquest data for event {eventContentId}");

            var row = db.GetAccountConquestInfos(account.ServerId).FirstOrDefault(x => x.EventContentId == eventContentId);
            if (row == null)
            {
                row = new ConquestInfoDBServer
                {
                    AccountServerId = account.ServerId,
                    EventContentId = eventContentId,
                    StepByDifficulty = new Dictionary<StageDifficulty, int> { [StageDifficulty.Normal] = 0 }
                };
                db.ConquestInfos.Add(row);
                db.SaveChanges();
            }

            return row;
        }

        public ConquestInfoDBServer Require(SchaleDataContext db, AccountDBServer account, long eventContentId)
        {
            return db.GetAccountConquestInfos(account.ServerId).FirstOrDefault(x => x.EventContentId == eventContentId)
                ?? throw new WebAPIException(WebAPIErrorCode.ConquestDataNotFound, $"No conquest progress for event {eventContentId}");
        }

        public long CalculateConditionAmount(long eventContentId)
        {
            return _excelService.GetTable<ConquestCalculateExcelT>()
                .FirstOrDefault(x => x.EventContentId == eventContentId)?.CalculateConditionParcelAmount ?? 0;
        }

        public ConquestTileExcelT RequireTile(long eventContentId, long tileUniqueId)
        {
            return _excelService.GetTable<ConquestTileExcelT>()
                    .FirstOrDefault(x => x.Id == tileUniqueId && x.EventId == eventContentId)
                ?? throw new WebAPIException(WebAPIErrorCode.ConquestDataNotFound,
                    $"Tile {tileUniqueId} not in event {eventContentId}");
        }

        public ConquestTileDB? StoredTile(ConquestInfoDBServer info, StageDifficulty difficulty, long tileUniqueId)
            => info.Tiles.FirstOrDefault(x => x.Difficulty == difficulty && x.TileUniqueId == tileUniqueId);

        public int CurrentStep(ConquestInfoDBServer info, StageDifficulty difficulty)
            => info.StepByDifficulty.TryGetValue(difficulty, out var step) ? step : 0;

        // Free (non-battle) tiles conquer directly; battle tiles must go through the start/result pair.
        public async Task<(ConquestTileDB Tile, ParcelResultDB ParcelResult, Dictionary<RewardTag, List<ParcelInfo>> ByTag)> Conquer(
            SchaleDataContext db, AccountDBServer account, ConquestInfoDBServer info,
            StageDifficulty difficulty, long tileUniqueId, bool throughBattle)
        {
            var tileExcel = RequireTile(info.EventContentId, tileUniqueId);

            if (tileExcel.Step > CurrentStep(info, difficulty))
                throw new WebAPIException(WebAPIErrorCode.ConquestStepNotOpened,
                    $"Tile {tileUniqueId} is on step {tileExcel.Step}");
            if (StoredTile(info, difficulty, tileUniqueId) != null)
                throw new WebAPIException(WebAPIErrorCode.ConquestAlreadyConquested, $"Tile {tileUniqueId} already taken");

            var isBattleTile = tileExcel.TileType == ConquestTileType.Battle;
            if (isBattleTile != throughBattle)
                throw new WebAPIException(WebAPIErrorCode.ConquestInvalidTileType,
                    $"Tile {tileUniqueId} is {tileExcel.TileType}");

            var parcels = new List<ParcelResult>();
            if (tileExcel.ConquestCostType != ParcelType.None && tileExcel.ConquestCostAmount > 0)
            {
                await _parcelHandler.BuildParcel(db, account,
                    new ParcelResult(tileExcel.ConquestCostType, tileExcel.ConquestCostId, tileExcel.ConquestCostAmount),
                    isConsume: true);
                TrackConditionSpend(info, tileExcel.ConquestCostType, tileExcel.ConquestCostId, tileExcel.ConquestCostAmount);
            }

            var byTag = RollRewardGroup(tileExcel.ConquestRewardId, parcels);
            var resolver = await _parcelHandler.BuildParcel(db, account, parcels);

            var tile = new ConquestTileDB
            {
                EventContentId = info.EventContentId,
                Difficulty = difficulty,
                TileUniqueId = tileUniqueId,
                TileState = TileState.FullyConquested,
                Level = 1,
                CreateTime = account.GameSettings.ServerDateTime()
            };
            info.Tiles.Add(tile);
            AdvanceStepIfCleared(info, difficulty);
            db.ConquestInfos.Update(info);
            await db.SaveChangesAsync();

            return (tile, resolver.ParcelResult, byTag);
        }

        public void AdvanceStepIfCleared(ConquestInfoDBServer info, StageDifficulty difficulty)
        {
            var step = CurrentStep(info, difficulty);
            var stepTiles = _excelService.GetTable<ConquestTileExcelT>()
                .Where(x => x.EventId == info.EventContentId && x.Step == step && x.Playable)
                .Select(x => x.Id)
                .ToHashSet();

            if (stepTiles.Count == 0)
                return;

            var conquered = info.Tiles
                .Where(x => x.Difficulty == difficulty)
                .Select(x => x.TileUniqueId)
                .ToHashSet();

            if (!stepTiles.IsSubsetOf(conquered))
                return;

            var maxStep = _excelService.GetTable<ConquestMapExcelT>()
                .Where(x => x.EventContentId == info.EventContentId
                    && (x.MapDifficulty == difficulty || x.MapDifficulty == StageDifficulty.None))
                .Select(x => x.StepIndex)
                .DefaultIfEmpty(step)
                .Max();

            if (step < maxStep)
                info.StepByDifficulty[difficulty] = step + 1;
        }

        // A 0-probability row always drops, matching the repo-wide 10000-basis convention.
        public Dictionary<RewardTag, List<ParcelInfo>> RollRewardGroup(long rewardGroupId, List<ParcelResult> into)
        {
            var byTag = new Dictionary<RewardTag, List<ParcelInfo>>();
            if (rewardGroupId == 0)
                return byTag;

            foreach (var row in _excelService.GetTable<ConquestRewardExcelT>().Where(x => x.GroupId == rewardGroupId))
            {
                if (row.RewardProb != 0 && Random.Shared.Next(10000) >= row.RewardProb)
                    continue;

                into.Add(new ParcelResult(row.RewardParcelType, row.RewardId, row.RewardAmount));

                if (!byTag.TryGetValue(row.RewardTag, out var list))
                    byTag[row.RewardTag] = list = [];
                list.Add(new ParcelInfo
                {
                    Key = new ParcelKeyPair { Type = row.RewardParcelType, Id = row.RewardId },
                    Amount = row.RewardAmount
                });
            }

            return byTag;
        }

        public ConquestStageSaveDB OpenTileBattle(
            SchaleDataContext db, AccountDBServer account, ConquestInfoDBServer info,
            StageDifficulty difficulty, long tileUniqueId, ConquestTileType tileType)
        {
            var save = new ConquestStageSaveDB
            {
                AccountServerId = account.ServerId,
                CreateTime = account.GameSettings.ServerDateTime(),
                EventContentId = info.EventContentId,
                Difficulty = difficulty,
                TileUniqueId = tileUniqueId,
                ConquestTileType = tileType
            };

            info.OpenBattle = save;
            db.ConquestInfos.Update(info);
            db.SaveChanges();

            return save;
        }

        public void RequireAndClearOpenBattle(
            SchaleDataContext db, ConquestInfoDBServer info, StageDifficulty difficulty, long tileUniqueId)
        {
            var open = info.OpenBattle;
            info.OpenBattle = null;
            db.ConquestInfos.Update(info);

            if (open == null
                || open.EventContentId != info.EventContentId
                || open.Difficulty != difficulty
                || open.TileUniqueId != tileUniqueId)
            {
                db.SaveChanges();
                throw new WebAPIException(WebAPIErrorCode.ConquestInvalidSaveData,
                    $"No open battle on tile {tileUniqueId}");
            }
        }

        public static bool IsWin(BattleSummary? summary)
            => summary != null && !summary.IsAbort && summary.EndType == BattleEndType.Clear;

        // The calculate gauge counts what the event's condition parcel has been spent on.
        public void TrackConditionSpend(ConquestInfoDBServer info, ParcelType type, long id, long amount)
        {
            var calc = _excelService.GetTable<ConquestCalculateExcelT>()
                .FirstOrDefault(x => x.EventContentId == info.EventContentId);
            if (calc == null)
                return;
            if (calc.CalculateConditionParcelType == type && calc.CalculateConditionParcelUniqueId == id)
                info.CumulatedConditionValue += amount;
        }
    }
}
