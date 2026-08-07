using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;

namespace Shittim_Server.Services
{
    // Collections are the event's CG/emblem album. Every unlock condition reads state that is already persisted somewhere else, so nothing here keeps its own progress counters - a call re-measures the rows of one
    // condition type and grants whatever has come true since the last time. That also means an account that gained the state while the code was missing gets its collection the next time any handler touches the type.
    public class EventContentCollectionService
    {
        private readonly ExcelTableService _excelService;

        public EventContentCollectionService(ExcelTableService excelService)
        {
            _excelService = excelService;
        }

        public List<EventContentCollectionDB> Unlock(
            SchaleDataContext context,
            AccountDBServer account,
            long eventContentId,
            CollectionUnlockType unlockType)
        {
            var owned = context.GetAccountEventContentCollections(account.ServerId)
                .Where(x => x.EventContentId == eventContentId)
                .Select(x => x.UniqueId)
                .ToHashSet();

            var candidates = _excelService.GetTable<EventContentCollectionExcelT>()
                .Where(x => x.EventContentId == eventContentId && x.UnlockConditionType == unlockType && !owned.Contains(x.Id))
                .ToList();

            if (candidates.Count == 0)
                return [];

            var measure = MeasureFor(context, account, eventContentId, unlockType);
            var now = account.GameSettings.ServerDateTime();
            var unlocked = new List<EventContentCollectionDB>();

            foreach (var excel in candidates)
            {
                var parameters = excel.UnlockConditionParameter;
                if (parameters == null || parameters.Count == 0)
                    parameters = [0];

                var threshold = Math.Max(1, excel.UnlockConditionCount);
                var satisfied = excel.MultipleConditionCheckType switch
                {
                    MultipleConditionCheckType.Or => parameters.Any(p => measure(p) >= threshold),
                    MultipleConditionCheckType.Count => parameters.Sum(measure) >= threshold,
                    _ => parameters.All(p => measure(p) >= threshold)
                };

                if (!satisfied)
                    continue;

                context.EventContentCollections.Add(new EventContentCollectionDBServer
                {
                    AccountServerId = account.ServerId,
                    EventContentId = eventContentId,
                    GroupId = excel.GroupId,
                    UniqueId = excel.Id,
                    ReceiveDate = now
                });

                unlocked.Add(new EventContentCollectionDB
                {
                    EventContentId = eventContentId,
                    GroupId = excel.GroupId,
                    UniqueId = excel.Id,
                    ReceiveDate = now
                });
            }

            return unlocked;
        }

        public List<EventContentCollectionDB> UnlockAll(
            SchaleDataContext context,
            AccountDBServer account,
            long eventContentId)
        {
            var types = _excelService.GetTable<EventContentCollectionExcelT>()
                .Where(x => x.EventContentId == eventContentId)
                .Select(x => x.UnlockConditionType)
                .Distinct()
                .ToList();

            return types.SelectMany(t => Unlock(context, account, eventContentId, t)).ToList();
        }

        private Func<long, long> MeasureFor(
            SchaleDataContext context,
            AccountDBServer account,
            long eventContentId,
            CollectionUnlockType unlockType)
        {
            switch (unlockType)
            {
                case CollectionUnlockType.ClearSpecificEventStage:
                {
                    var cleared = context.GetAccountCampaignStageHistories(account.ServerId)
                        .Where(x => x.IsClearedEver)
                        .Select(x => x.StageUniqueId)
                        .ToHashSet();
                    return p => cleared.Contains(p) ? 1 : 0;
                }

                case CollectionUnlockType.ClearSpecificEventScenario:
                case CollectionUnlockType.ClearSpecificScenario:
                {
                    var cleared = context.GetAccountScenarioGroupHistories(account.ServerId)
                        .Select(x => x.ScenarioGroupUqniueId)
                        .ToHashSet();
                    return p => cleared.Contains(p) ? 1 : 0;
                }

                case CollectionUnlockType.ClearSpecificEventMission:
                {
                    var complete = context.MissionProgresses
                        .Where(x => x.AccountServerId == account.ServerId && x.Complete)
                        .Select(x => x.MissionUniqueId)
                        .ToHashSet();
                    return p => complete.Contains(p) ? 1 : 0;
                }

                case CollectionUnlockType.PurchaseSpecificItemCount:
                {
                    var purchases = context.GetAccountShopPurchases(account.ServerId)
                        .ToDictionary(x => x.ShopUniqueId, x => x.PurchaseCount);
                    return p => purchases.TryGetValue(p, out var count) ? count : 0;
                }

                case CollectionUnlockType.SpecificEventLocationRank:
                {
                    var ranks = context.GetAccountEventContentLocations(account.ServerId)
                        .Where(x => x.EventContentId == eventContentId)
                        .ToDictionary(x => x.LocationId, x => x.Rank);
                    return p => ranks.TryGetValue(p, out var rank) ? rank : 0;
                }

                case CollectionUnlockType.DiceRaceConsumeDiceCount:
                {
                    var rolls = context.GetAccountEventContentPlays(account.ServerId)
                        .FirstOrDefault(x => x.EventContentId == eventContentId)?.DiceRaceRollCount ?? 0;
                    return _ => rolls;
                }

                case CollectionUnlockType.MinigameTBGThemaClear:
                {
                    var board = context.GetAccountMiniGameTableBoards(account.ServerId)
                        .FirstOrDefault(x => x.EventContentId == eventContentId);
                    var cleared = board?.BestClearRecord.Keys.ToHashSet() ?? [];
                    // the parameter is the thema index, but a handful of rows just want "any N themas done" and carry no parameter at all
                    return p => p == 0 ? cleared.Count : (cleared.Contains((int)p) ? 1 : 0);
                }

                case CollectionUnlockType.MinigameEnter:
                {
                    var played = context.GetAccountMiniGameHistories(account.ServerId)
                        .Where(x => x.EventContentId == eventContentId)
                        .Select(x => x.UniqueId)
                        .ToHashSet();
                    return p => p == 0 ? played.Count : (played.Contains(p) ? 1 : 0);
                }

                case CollectionUnlockType.MinigameDreamMakerParameter:
                {
                    var dream = context.GetAccountMiniGameDreamMakers(account.ServerId)
                        .FirstOrDefault(x => x.EventContentId == eventContentId);
                    var amounts = dream?.ParameterAmounts ?? [];
                    // the parameter is the MiniGameDreamParameterExcel row id, not the enum - 100/200/300/400 on event 836 and 10100.. on the rerun
                    var byExcelId = _excelService.GetTable<MiniGameDreamParameterExcelT>()
                        .Where(x => x.EventContentId == eventContentId)
                        .ToDictionary(x => x.Id, x => x.ParameterType);
                    return p => byExcelId.TryGetValue(p, out var type) && amounts.TryGetValue(type, out var amount) ? amount : 0;
                }

                case CollectionUnlockType.MinigameCCGBuyPerk:
                {
                    var perks = context.GetAccountMiniGameCCGSaves(account.ServerId)
                        .FirstOrDefault(x => x.EventContentId == eventContentId)?.Perks ?? [];
                    return p => p == 0 ? perks.Count : (perks.Contains(p) ? 1 : 0);
                }

                default:
                    return _ => 0;
            }
        }
    }
}
