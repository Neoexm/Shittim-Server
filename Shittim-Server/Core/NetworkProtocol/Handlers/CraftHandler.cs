using AutoMapper;
using Microsoft.EntityFrameworkCore;
using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.Core.Math;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.NetworkProtocol;
using Shittim_Server.Core;
using Shittim_Server.Services;

namespace Shittim_Server.Core.NetworkProtocol.Handlers
{
    /// <summary>
    /// Crafting chamber, modelled on the complete flow in the detailed official capture: UpdateNodeLevel
    /// (consume materials + gold; the node gains a level, a random seed and five leaf choices for the
    /// next tier) -> SelectNode (commit one leaf) -> repeat per tier -> BeginProcess (resolve per-tier
    /// results and a StartTime/EndTime window) -> CompleteProcess (time-skip tickets, item 2, one per
    /// started 2.5h) -> Reward (parcels, slot clears). Wire shapes follow the capture throughout; where
    /// official's internal rules are not observable - leaf-choice weighting, the meaning of ResultId,
    /// exact craft duration - the approximation is noted inline.
    /// </summary>
    public class CraftHandler : ProtocolHandlerBase
    {
        // The captured 3-tier craft ran 7h30m and its CompleteProcess burned 3 tickets, so
        // 2.5h per selected tier with 1 ticket per started 2.5h reproduces both observations.
        private static readonly TimeSpan TierDuration = TimeSpan.FromHours(2.5);
        private const long TimeSkipTicketItemId = 2;

        private readonly ISessionKeyService _sessionService;
        private readonly ExcelTableService _excelService;
        private readonly IMapper _mapper;
        private readonly ConsumeHandler _consumeHandler;
        private readonly ParcelHandler _parcelHandler;
        private readonly MissionService _missionService;

        public CraftHandler(
            IProtocolHandlerRegistry registry,
            ISessionKeyService sessionService,
            ExcelTableService excelService,
            IMapper mapper,
            ConsumeHandler consumeHandler,
            ParcelHandler parcelHandler,
            MissionService missionService) : base(registry)
        {
            _sessionService = sessionService;
            _excelService = excelService;
            _mapper = mapper;
            _consumeHandler = consumeHandler;
            _parcelHandler = parcelHandler;
            _missionService = missionService;
        }

        [ProtocolHandler(Protocol.Craft_List)]
        public async Task<CraftInfoListResponse> CraftList(
            SchaleDataContext db,
            CraftInfoListRequest request,
            CraftInfoListResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

            var craftInfos = db.CraftInfos
                .Where(x => x.AccountServerId == account.ServerId)
                .ToList();

            // Official omits both keys when there is nothing to list (the first captured sample
            // is bare Protocol + ServerTimeTicks) and never sends ShiftingCraftInfos at all.
            response.CraftInfos = craftInfos.Count > 0 ? _mapper.Map<List<CraftInfoDB>>(craftInfos) : null;
            response.ShiftingCraftInfos = null;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_UpdateNodeLevel)]
        public async Task<CraftUpdateNodeLevelResponse> UpdateNodeLevel(
            SchaleDataContext db,
            CraftUpdateNodeLevelRequest request,
            CraftUpdateNodeLevelResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var slot = GetSlot(db, account.ServerId, request.SlotId);
            if (slot == null)
            {
                slot = new CraftInfoDBServer
                {
                    AccountServerId = account.ServerId,
                    SlotSequence = request.SlotId,
                    // Crafts that have not begun processing carry the MaxValue sentinels officially.
                    StartTime = DateTime.MaxValue,
                    EndTime = DateTime.MaxValue,
                    CraftSlotOpenDate = now,
                    Nodes = []
                };
                db.CraftInfos.Add(slot);
            }
            slot.Nodes ??= [];

            var consumeResult = await _consumeHandler.BuildConsumeResult(
                db, account, request.ConsumeRequestDB ?? new ConsumeRequestDB());

            var currency = db.GetAccountCurrencies(account.ServerId).FirstOrDefault();
            if (currency != null && request.ConsumeGoldAmount > 0)
            {
                var gold = System.Math.Min(request.ConsumeGoldAmount, currency.CurrencyDict[CurrencyTypes.Gold]);
                currency.CurrencyDict[CurrencyTypes.Gold] -= gold;
                currency.UpdateTimeDict[CurrencyTypes.Gold] = now;
            }

            // The node being leveled: the freshly selected (still seedless) node if there is one,
            // otherwise the base node, created on the very first call.
            var node = slot.Nodes.LastOrDefault(x => x.NodeRandomSeed == 0);
            if (node == null && slot.Nodes.Count == 0)
            {
                node = new CraftNodeDB();
                slot.Nodes.Add(node);
            }
            node ??= slot.Nodes[^1];

            node.NodeLevel += 1;
            node.NodeRandomSeed = Random.Shared.Next(1, int.MaxValue);
            node.LeafNodeIds = RollLeafNodes(node.NodeTier);

            db.CraftInfos.Update(slot);
            await db.SaveChangesAsync();

            response.CraftInfoDB = _mapper.Map<CraftInfoDB>(slot);
            response.CraftNodeDB = node;
            response.AccountCurrencyDB = currency?.ToMap(_mapper);
            response.ConsumeResultDB = consumeResult.ConsumeResult;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_SelectNode)]
        public async Task<CraftSelectNodeResponse> SelectNode(
            SchaleDataContext db,
            CraftSelectNodeRequest request,
            CraftSelectNodeResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

            var slot = GetSlot(db, account.ServerId, request.SlotId)
                ?? throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Craft slot {request.SlotId} has no craft");

            var current = slot.Nodes?.LastOrDefault(x => x.LeafNodeIds is { Count: > 0 })
                ?? throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "No node with leaf choices to select from");

            if (request.LeafNodeIndex < 0 || request.LeafNodeIndex >= current.LeafNodeIds!.Count)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Leaf index {request.LeafNodeIndex} out of range");

            // Committing a leaf appends the next tier's node, bare until it gets leveled.
            // Official's SelectedNodeDB is exactly {NodeTier, NodeId, LeafNodeIds: []}.
            var selected = new CraftNodeDB
            {
                NodeTier = current.NodeTier + 1,
                NodeId = current.LeafNodeIds[(int)request.LeafNodeIndex],
                LeafNodeIds = []
            };
            slot.Nodes!.Add(selected);

            db.CraftInfos.Update(slot);
            await db.SaveChangesAsync();

            response.SelectedNodeDB = selected;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_BeginProcess)]
        public async Task<CraftBeginProcessResponse> BeginProcess(
            SchaleDataContext db,
            CraftBeginProcessRequest request,
            CraftBeginProcessResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var slot = GetSlot(db, account.ServerId, request.SlotId)
                ?? throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Craft slot {request.SlotId} has no craft");

            var selectedNodes = (slot.Nodes ?? []).Where(x => x.NodeId != 0).ToList();
            if (selectedNodes.Count == 0)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "No nodes selected to process");

            var nodeGroups = _excelService.GetTable<GachaCraftNodeGroupExcelT>();
            var nodeExcels = _excelService.GetTable<GachaCraftNodeExcelT>();
            var gachaElements = _excelService.GetTable<GachaElementExcelT>();

            foreach (var node in selectedNodes)
            {
                node.NodeQuality = nodeExcels.FirstOrDefault(x => x.ID == node.NodeId)?.NodeQuality ?? 1;
                // Official's final-tier node carries no LeafNodeIds key; the intermediate tiers
                // keep the five choices they showed.
                if (node.LeafNodeIds is { Count: 0 })
                    node.LeafNodeIds = null;

                // Node -> reward: the node's gacha groups weighted by ProbWeight, then one roll
                // inside the chosen group. ResultId is set to the chosen group id - its exact
                // official semantics are not derivable from the capture, only its presence.
                var groups = nodeGroups.Where(x => x.NodeId == node.NodeId).ToList();
                if (groups.Count == 0)
                    continue;

                var totalWeight = groups.Sum(x => System.Math.Max(1, x.ProbWeight));
                var roll = Random.Shared.NextInt64(1, totalWeight + 1);
                long acc = 0;
                var chosen = groups[^1];
                foreach (var g in groups)
                {
                    acc += System.Math.Max(1, g.ProbWeight);
                    if (roll <= acc) { chosen = g; break; }
                }

                node.ResultId = chosen.GachaGroupId;
                var rolled = new GachaGroupHandler(gachaElements)
                    .CreateGachaGroupParcel([new ParcelResult(ParcelType.GachaGroup, chosen.GachaGroupId, 1)])
                    .FirstOrDefault();
                if (rolled != null)
                {
                    node.CraftNodeResult = new CraftNodeResult
                    {
                        NodeTier = node.NodeTier,
                        ParcelInfo = new ParcelInfo
                        {
                            Key = new ParcelKeyPair { Type = rolled.Type, Id = rolled.Id },
                            Amount = rolled.Amount,
                            Multiplier = BasisPoint.One,
                            Probability = BasisPoint.One
                        }
                    };
                }
            }

            slot.StartTime = now;
            slot.EndTime = now + TierDuration * selectedNodes.Count;

            db.CraftInfos.Update(slot);
            await db.SaveChangesAsync();

            response.CraftInfoDB = _mapper.Map<CraftInfoDB>(slot);

            return response;
        }

        [ProtocolHandler(Protocol.Craft_CompleteProcess)]
        public async Task<CraftCompleteProcessResponse> CompleteProcess(
            SchaleDataContext db,
            CraftCompleteProcessRequest request,
            CraftCompleteProcessResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var slot = GetSlot(db, account.ServerId, request.SlotId)
                ?? throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Craft slot {request.SlotId} has no craft");

            // Finishing early costs time-skip tickets (item 2), one per started 2.5h. Whatever
            // the account can pay is taken; a short ticket stack still completes the craft rather
            // than stranding it (official behaviour for that case is unobserved).
            if (slot.EndTime > now)
            {
                var remaining = slot.EndTime - now;
                var ticketsNeeded = (long)System.Math.Ceiling(remaining / TierDuration);
                var ticket = db.GetAccountItems(account.ServerId).FirstOrDefault(x => x.UniqueId == TimeSkipTicketItemId);
                if (ticket != null)
                {
                    ticket.StackCount -= System.Math.Min(ticketsNeeded, ticket.StackCount);
                    response.TicketItemDB = ticket.ToMap(_mapper);
                }
                slot.EndTime = now;
            }

            db.CraftInfos.Update(slot);
            await db.SaveChangesAsync();

            response.AccountCurrencyDB = db.GetAccountCurrencies(account.ServerId).FirstOrDefault()?.ToMap(_mapper);
            response.CraftInfoDB = _mapper.Map<CraftInfoDB>(slot);

            return response;
        }

        [ProtocolHandler(Protocol.Craft_Reward)]
        public async Task<CraftRewardResponse> Reward(
            SchaleDataContext db,
            CraftRewardRequest request,
            CraftRewardResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var slot = GetSlot(db, account.ServerId, request.SlotId)
                ?? throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Craft slot {request.SlotId} has no craft");

            if (slot.EndTime > now)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "Craft is still processing");

            var parcels = (slot.Nodes ?? [])
                .Where(x => x.CraftNodeResult?.ParcelInfo != null)
                .Select(x => new ParcelResult(
                    x.CraftNodeResult!.ParcelInfo!.Key.Type,
                    x.CraftNodeResult.ParcelInfo.Key.Id,
                    x.CraftNodeResult.ParcelInfo.Amount))
                .ToList();

            var parcelResolver = await _parcelHandler.BuildParcel(db, account, parcels);
            response.ParcelResultDB = parcelResolver.ParcelResult;

            // The claimed slot clears: official's post-reward Craft_List no longer carries it.
            db.CraftInfos.Remove(slot);
            await db.SaveChangesAsync();

            var updatedMissions = _missionService.UpdateMissionProgress(
                db, account, MissionCompleteConditionType.Reset_CraftCount);
            if (updatedMissions.Count > 0)
                response.MissionProgressDBs = updatedMissions;

            return response;
        }

        private static CraftInfoDBServer? GetSlot(SchaleDataContext db, long accountServerId, long slotId) =>
            db.CraftInfos.FirstOrDefault(x => x.AccountServerId == accountServerId && x.SlotSequence == slotId);

        // Five distinct choices from the next tier's node pool, like every captured node offers.
        // Official's choice weighting is unknown; uniform across the tier's nodes.
        private List<long> RollLeafNodes(CraftNodeTier currentTier)
        {
            var nextTier = (long)currentTier + 1;
            var candidates = _excelService.GetTable<GachaCraftNodeExcelT>()
                .Where(x => x.Tier == nextTier)
                .Select(x => x.ID)
                .Distinct()
                .ToList();

            var picks = new List<long>();
            while (picks.Count < 5 && candidates.Count > 0)
            {
                var i = Random.Shared.Next(candidates.Count);
                picks.Add(candidates[i]);
                candidates.RemoveAt(i);
            }
            return picks;
        }
    }
}
