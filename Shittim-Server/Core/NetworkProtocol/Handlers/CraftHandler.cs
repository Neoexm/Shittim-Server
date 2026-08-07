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
    // Crafting chamber, modelled on the complete flow in the detailed official capture: UpdateNodeLevel (consume materials + gold; the node gains a level, a random seed and five leaf choices for the next tier) -> SelectNode (commit one leaf) -> repeat per tier -> BeginProcess (resolve per-tier results and a StartTime/EndTime window)
    // -> CompleteProcess (time-skip tickets, item 2, one per started 2.5h) -> Reward (parcels, slot clears). Wire shapes follow the capture throughout;
    // official's internal rules for leaf-choice weighting, the meaning of ResultId and the exact craft duration are not observable in it.
    public class CraftHandler : ProtocolHandlerBase
    {
        // The captured 3-tier craft ran 7h30m and its CompleteProcess burned 3 tickets, so 2.5h per selected tier with 1 ticket per started 2.5h reproduces both observations.
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

            // Slots written before SelectNode was bounded can hold duplicate tiers or more than the five legal nodes. Resending one replays a broken node animation and kicks the client to the lobby on every menu open, so drop them here - the slot is lost but crafting works again.
            var corrupted = craftInfos.Where(x => x.Nodes != null &&
                (x.Nodes.Count > 5 || x.Nodes.GroupBy(n => n.NodeTier).Any(g => g.Count() > 1) || x.Nodes.Any(n => n.NodeTier < CraftNodeTier.Base || n.NodeTier > CraftNodeTier.Max))).ToList();
            if (corrupted.Count > 0)
            {
                db.CraftInfos.RemoveRange(corrupted);
                await db.SaveChangesAsync();
                craftInfos = craftInfos.Except(corrupted).ToList();
            }

            // Official omits both keys when there is nothing to list (the first captured sample is bare Protocol + ServerTimeTicks) and never sends
            // ShiftingCraftInfos at all.
            response.CraftInfos = craftInfos.Count > 0 ? _mapper.Map<List<CraftInfoDB>>(craftInfos) : null;
            response.ShiftingCraftInfos = null;
            response.PresetSlotDBs = account.ContentInfo.CraftPresets.Count > 0 ? account.ContentInfo.CraftPresets : null;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_SavePreset)]
        public async Task<CraftSavePresetResponse> SavePreset(
            SchaleDataContext db,
            CraftSavePresetRequest request,
            CraftSavePresetResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

            if (request.PresetSlotDB != null)
            {
                var presets = account.ContentInfo.CraftPresets;
                var existing = presets.FirstOrDefault(x => x.PresetIndex == request.PresetSlotDB.PresetIndex);
                if (existing != null)
                {
                    existing.PresetNodeDBs = request.PresetSlotDB.PresetNodeDBs;
                    // Overwriting a slot's nodes keeps the name it already has; the client renames through SavePresetName.
                    request.PresetSlotDB.PresetName = existing.PresetName;
                }
                else
                    presets.Add(request.PresetSlotDB);

                db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
                await db.SaveChangesAsync();
            }

            response.PresetSlotDB = request.PresetSlotDB;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_SavePresetName)]
        public async Task<CraftSavePresetNameResponse> SavePresetName(
            SchaleDataContext db,
            CraftSavePresetNameRequest request,
            CraftSavePresetNameResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

            var presets = account.ContentInfo.CraftPresets;
            var slot = presets.FirstOrDefault(x => x.PresetIndex == request.PresetIndex);
            if (slot == null)
            {
                slot = new CraftPresetSlotDB { PresetIndex = request.PresetIndex };
                presets.Add(slot);
            }
            slot.PresetName = request.PresetName;

            db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await db.SaveChangesAsync();

            return response;
        }

        [ProtocolHandler(Protocol.Craft_HistoryList)]
        public async Task<CraftHistoryListResponse> HistoryList(
            SchaleDataContext db,
            CraftHistoryListRequest request,
            CraftHistoryListResponse response)
        {
            await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            return response;
        }

        // the instant recipe path (RecipeType Basic/etc), separate from the gacha-craft chamber below
        [ProtocolHandler(Protocol.Recipe_Craft)]
        public async Task<RecipeCraftResponse> RecipeCraft(
            SchaleDataContext db,
            RecipeCraftRequest request,
            RecipeCraftResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);

            var recipe = _excelService.GetTable<RecipeCraftExcelT>().FirstOrDefault(x => x.Id == request.RecipeCraftUniqueId)
                ?? throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Unknown recipe {request.RecipeCraftUniqueId}");
            var ingredient = _excelService.GetTable<RecipeIngredientExcelT>().FirstOrDefault(x => x.Id == request.RecipeIngredientUniqueId);

            if (ingredient != null)
            {
                var costs = new List<ParcelResult>();
                for (var i = 0; i < (ingredient.CostId?.Count ?? 0); i++)
                    costs.Add(new ParcelResult(ingredient.CostParcelType[i], ingredient.CostId[i], ingredient.CostAmount[i]));
                for (var i = 0; i < (ingredient.IngredientId?.Count ?? 0); i++)
                    costs.Add(new ParcelResult(ingredient.IngredientParcelType[i], ingredient.IngredientId[i], ingredient.IngredientAmount[i]));
                await _parcelHandler.BuildParcel(db, account, costs, isConsume: true);
            }

            var results = new List<ParcelResult>();
            for (var i = 0; i < (recipe.ParcelId?.Count ?? 0); i++)
                results.Add(new ParcelResult(recipe.ParcelType[i], recipe.ParcelId[i],
                    Random.Shared.NextInt64(recipe.ResultAmountMin[i], recipe.ResultAmountMax[i] + 1)));

            var resolver = await _parcelHandler.BuildParcel(db, account, results);
            await db.SaveChangesAsync();

            response.ParcelResultDB = resolver.ParcelResult;

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
                    // Not-yet-processing crafts carry the MaxValue sentinels officially.
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

            // The node being leveled: the freshly selected (still seedless) node if there is one, otherwise the base node, created on the very first call.
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

            // only the last node may be selected from. Falling back to an earlier tier that still has its leaves would append a duplicate of the tier after it - that is how a maxed craft (whose final node rolls no leaves, there is no tier past Max) grows an endless tail of tier-4 nodes.
            var current = slot.Nodes is { Count: > 0 } ? slot.Nodes[^1] : null;
            if (current == null || current.LeafNodeIds == null || current.LeafNodeIds.Count == 0)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "No node with leaf choices to select from");

            if (request.LeafNodeIndex < 0 || request.LeafNodeIndex >= current.LeafNodeIds!.Count)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Leaf index {request.LeafNodeIndex} out of range");

            // Committing a leaf appends the next tier's node, bare until it gets leveled. Official's SelectedNodeDB is exactly {NodeTier, NodeId, LeafNodeIds: []}.
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

            ResolveNodeResults(selectedNodes);

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

            // Finishing early costs time-skip tickets (item 2), one per started 2.5h.
            // Whatever the account can pay is taken; a short ticket stack still completes the craft rather than stranding it (official behaviour for that case is unobserved).
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

            // The claimed slot clears: official's post-reward Craft_List omits it.
            db.CraftInfos.Remove(slot);
            await db.SaveChangesAsync();

            var updatedMissions = _missionService.UpdateMissionProgress(
                db, account, MissionCompleteConditionType.Reset_CraftCount);
            if (updatedMissions.Count > 0)
                response.MissionProgressDBs = updatedMissions;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_CompleteProcessAll)]
        public async Task<CraftCompleteProcessAllResponse> CompleteProcessAll(
            SchaleDataContext db,
            CraftCompleteProcessAllRequest request,
            CraftCompleteProcessAllResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            // MaxValue start = the slot never began processing, nothing to finish there
            var processing = db.CraftInfos
                .Where(x => x.AccountServerId == account.ServerId)
                .ToList()
                .Where(x => x.StartTime != DateTime.MaxValue && x.EndTime > now)
                .ToList();

            long ticketsNeeded = 0;
            foreach (var slot in processing)
            {
                ticketsNeeded += (long)System.Math.Ceiling((slot.EndTime - now) / TierDuration);
                slot.EndTime = now;
                db.CraftInfos.Update(slot);
            }

            if (ticketsNeeded > 0)
            {
                var ticket = db.GetAccountItems(account.ServerId).FirstOrDefault(x => x.UniqueId == TimeSkipTicketItemId);
                if (ticket != null)
                {
                    ticket.StackCount -= System.Math.Min(ticketsNeeded, ticket.StackCount);
                    response.TicketItemDB = ticket.ToMap(_mapper);
                }
            }

            await db.SaveChangesAsync();

            response.CraftInfoDBs = processing.Count > 0 ? _mapper.Map<List<CraftInfoDB>>(processing) : null;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_RewardAll)]
        public async Task<CraftRewardAllResponse> RewardAll(
            SchaleDataContext db,
            CraftRewardAllRequest request,
            CraftRewardAllResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var finished = db.CraftInfos
                .Where(x => x.AccountServerId == account.ServerId)
                .ToList()
                .Where(x => x.EndTime <= now)
                .ToList();

            var parcels = finished
                .SelectMany(slot => (slot.Nodes ?? [])
                    .Where(x => x.CraftNodeResult?.ParcelInfo != null)
                    .Select(x => new ParcelResult(
                        x.CraftNodeResult!.ParcelInfo!.Key.Type,
                        x.CraftNodeResult.ParcelInfo.Key.Id,
                        x.CraftNodeResult.ParcelInfo.Amount)))
                .ToList();

            var parcelResolver = await _parcelHandler.BuildParcel(db, account, parcels);
            response.ParcelResultDB = parcelResolver.ParcelResult;

            db.CraftInfos.RemoveRange(finished);
            await db.SaveChangesAsync();

            var remaining = db.CraftInfos.Where(x => x.AccountServerId == account.ServerId).ToList();
            response.CraftInfos = remaining.Count > 0 ? _mapper.Map<List<CraftInfoDB>>(remaining) : null;

            var updatedMissions = _missionService.UpdateMissionProgress(
                db, account, MissionCompleteConditionType.Reset_CraftCount);
            if (updatedMissions.Count > 0)
                response.MissionProgressDBs = updatedMissions;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_AutoBeginProcess)]
        public async Task<CraftAutoBeginProcessResponse> AutoBeginProcess(
            SchaleDataContext db,
            CraftAutoBeginProcessRequest request,
            CraftAutoBeginProcessResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var preset = account.ContentInfo.CraftPresets.FirstOrDefault(x => x.PresetIndex == request.PresetIndex)
                ?? throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"No craft preset at index {request.PresetIndex}");

            var activated = (preset.PresetNodeDBs ?? []).Where(x => x.IsActivated).OrderBy(x => x.NodeTier).ToList();
            if (activated.Count == 0)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "Preset has no activated nodes");

            // the preset carries the per-craft cost the client showed; take it Count times in one consume
            var costs = activated
                .SelectMany(x => x.CostParcels ?? [])
                .Where(c => c.Key != null)
                .Select(c => new ParcelResult(c.Key!.Type, c.Key.Id, c.Amount * request.Count))
                .ToList();
            var resolver = await _parcelHandler.BuildParcel(db, account, costs, isConsume: true);

            var nodeExcels = _excelService.GetTable<GachaCraftNodeExcelT>();
            var used = db.CraftInfos.Where(x => x.AccountServerId == account.ServerId).Select(x => x.SlotSequence).ToList();

            var started = new List<CraftInfoDBServer>();
            long nextSlot = 0;
            for (var i = 0; i < request.Count; i++)
            {
                while (used.Contains(nextSlot)) nextSlot++;
                used.Add(nextSlot);

                // base node first like the manual flow, then one node per activated preset tier; the preset's priority list wins, a random tier node fills in when none of its picks exist
                var nodes = new List<CraftNodeDB> { new() };
                foreach (var presetNode in activated)
                {
                    var pick = (presetNode.PriorityNodeIds ?? []).FirstOrDefault(id => nodeExcels.Any(x => x.ID == id));
                    if (pick == 0)
                    {
                        var pool = nodeExcels.Where(x => x.Tier == (long)presetNode.NodeTier).Select(x => x.ID).Distinct().ToList();
                        if (pool.Count == 0)
                            continue;
                        pick = pool[Random.Shared.Next(pool.Count)];
                    }
                    nodes.Add(new CraftNodeDB { NodeTier = presetNode.NodeTier, NodeId = pick, LeafNodeIds = [] });
                }

                var selectedNodes = nodes.Where(x => x.NodeId != 0).ToList();
                ResolveNodeResults(selectedNodes);

                var slot = new CraftInfoDBServer
                {
                    AccountServerId = account.ServerId,
                    SlotSequence = nextSlot,
                    StartTime = now,
                    EndTime = now + TierDuration * selectedNodes.Count,
                    CraftSlotOpenDate = now,
                    Nodes = nodes
                };
                db.CraftInfos.Add(slot);
                started.Add(slot);
            }

            await db.SaveChangesAsync();

            response.CraftInfoDBs = _mapper.Map<List<CraftInfoDB>>(started);
            response.ParcelResultDB = resolver.ParcelResult;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_ShiftingBeginProcess)]
        public async Task<CraftShiftingBeginProcessResponse> ShiftingBeginProcess(
            SchaleDataContext db,
            CraftShiftingBeginProcessRequest request,
            CraftShiftingBeginProcessResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var recipe = _excelService.GetTable<ShiftingCraftRecipeExcelT>().FirstOrDefault(x => x.Id == request.RecipeId)
                ?? throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Unknown shifting recipe {request.RecipeId}");

            // craft count falls out of the fed items' shifting quality against the recipe's per-craft exp, measured before the consume eats the stacks
            var itemExcels = _excelService.GetTable<ItemExcelT>();
            long fedExp = 0;
            foreach (var (serverId, count) in request.ConsumeRequestDB?.ConsumeItemServerIdAndCounts ?? new Dictionary<long, long>())
            {
                var item = db.GetAccountItems(account.ServerId).FirstOrDefault(x => x.ServerId == serverId);
                if (item != null)
                    fedExp += (itemExcels.FirstOrDefault(x => x.Id == item.UniqueId)?.ShiftingCraftQuality ?? 0) * count;
            }
            var craftAmount = recipe.IngredientExp > 0 ? System.Math.Max(1, fedExp / recipe.IngredientExp) : 1;

            var consumeResult = await _consumeHandler.BuildConsumeResult(
                db, account, request.ConsumeRequestDB ?? new ConsumeRequestDB());

            var currency = db.GetAccountCurrencies(account.ServerId).FirstOrDefault();
            if (currency != null && recipe.RequireGold > 0)
            {
                var gold = System.Math.Min(recipe.RequireGold * craftAmount, currency.CurrencyDict[CurrencyTypes.Gold]);
                currency.CurrencyDict[CurrencyTypes.Gold] -= gold;
                currency.UpdateTimeDict[CurrencyTypes.Gold] = now;
                consumeResult.ParcelResult.AccountCurrencyDB = currency.ToMap(_mapper);
            }

            var craft = new ShiftingCraftInfoDB
            {
                SlotSequence = request.SlotId,
                CraftRecipeId = request.RecipeId,
                CraftAmount = craftAmount,
                StartTime = now,
                EndTime = now + TierDuration * craftAmount
            };
            var crafts = account.ContentInfo.ShiftingCrafts;
            crafts.RemoveAll(x => x.SlotSequence == request.SlotId);
            crafts.Add(craft);

            db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await db.SaveChangesAsync();

            response.CraftInfoDB = craft;
            response.ParcelResultDB = consumeResult.ParcelResult;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_ShiftingCompleteProcess)]
        public async Task<CraftShiftingCompleteProcessResponse> ShiftingCompleteProcess(
            SchaleDataContext db,
            CraftShiftingCompleteProcessRequest request,
            CraftShiftingCompleteProcessResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var craft = account.ContentInfo.ShiftingCrafts.FirstOrDefault(x => x.SlotSequence == request.SlotId)
                ?? throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Shifting slot {request.SlotId} has no craft");

            if (craft.EndTime > now)
            {
                var ticketsNeeded = (long)System.Math.Ceiling((craft.EndTime - now) / TierDuration);
                var ticket = db.GetAccountItems(account.ServerId).FirstOrDefault(x => x.UniqueId == TimeSkipTicketItemId);
                if (ticket != null)
                {
                    ticket.StackCount -= System.Math.Min(ticketsNeeded, ticket.StackCount);
                    response.ParcelResultDB = new ParcelResultDB { ItemDBs = new() { [ticket.ServerId] = ticket.ToMap(_mapper) } };
                }
                craft.EndTime = now;
            }

            db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await db.SaveChangesAsync();

            response.CraftInfoDB = craft;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_ShiftingReward)]
        public async Task<CraftShiftingRewardResponse> ShiftingReward(
            SchaleDataContext db,
            CraftShiftingRewardRequest request,
            CraftShiftingRewardResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var crafts = account.ContentInfo.ShiftingCrafts;
            var craft = crafts.FirstOrDefault(x => x.SlotSequence == request.SlotId)
                ?? throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, $"Shifting slot {request.SlotId} has no craft");

            if (craft.EndTime > now)
                throw new WebAPIException(WebAPIErrorCode.ServerFailedToHandleRequest, "Craft is still processing");

            var recipe = _excelService.GetTable<ShiftingCraftRecipeExcelT>().FirstOrDefault(x => x.Id == craft.CraftRecipeId);
            var parcelResolver = await _parcelHandler.BuildParcel(db, account,
                new ParcelResult(recipe.ResultParcel, recipe.ResultId, recipe.ResultAmount * craft.CraftAmount));

            crafts.Remove(craft);
            db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await db.SaveChangesAsync();

            response.ParcelResultDB = parcelResolver.ParcelResult;
            response.TargetCraftInfos = crafts.Count > 0 ? crafts : null;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_ShiftingCompleteProcessAll)]
        public async Task<CraftShiftingCompleteProcessAllResponse> ShiftingCompleteProcessAll(
            SchaleDataContext db,
            CraftShiftingCompleteProcessAllRequest request,
            CraftShiftingCompleteProcessAllResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var processing = account.ContentInfo.ShiftingCrafts.Where(x => x.EndTime > now).ToList();

            long ticketsNeeded = 0;
            foreach (var craft in processing)
            {
                ticketsNeeded += (long)System.Math.Ceiling((craft.EndTime - now) / TierDuration);
                craft.EndTime = now;
            }

            if (ticketsNeeded > 0)
            {
                var ticket = db.GetAccountItems(account.ServerId).FirstOrDefault(x => x.UniqueId == TimeSkipTicketItemId);
                if (ticket != null)
                {
                    ticket.StackCount -= System.Math.Min(ticketsNeeded, ticket.StackCount);
                    response.ParcelResultDB = new ParcelResultDB { ItemDBs = new() { [ticket.ServerId] = ticket.ToMap(_mapper) } };
                }
            }

            db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await db.SaveChangesAsync();

            response.CraftInfoDBs = processing.Count > 0 ? processing : null;

            return response;
        }

        [ProtocolHandler(Protocol.Craft_ShiftingRewardAll)]
        public async Task<CraftShiftingRewardAllResponse> ShiftingRewardAll(
            SchaleDataContext db,
            CraftShiftingRewardAllRequest request,
            CraftShiftingRewardAllResponse response)
        {
            var account = await _sessionService.GetAuthenticatedUser(db, request.SessionKey);
            var now = account.GameSettings.ServerDateTime();

            var crafts = account.ContentInfo.ShiftingCrafts;
            var finished = crafts.Where(x => x.EndTime <= now).ToList();

            var recipes = _excelService.GetTable<ShiftingCraftRecipeExcelT>();
            var parcels = new List<ParcelResult>();
            foreach (var craft in finished)
            {
                var recipe = recipes.FirstOrDefault(x => x.Id == craft.CraftRecipeId);
                if (recipe == null)
                    continue;
                parcels.Add(new ParcelResult(recipe.ResultParcel, recipe.ResultId, recipe.ResultAmount * craft.CraftAmount));
            }

            var parcelResolver = await _parcelHandler.BuildParcel(db, account, parcels);

            crafts.RemoveAll(x => finished.Contains(x));
            db.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await db.SaveChangesAsync();

            response.ParcelResultDB = parcelResolver.ParcelResult;
            response.CraftInfoDBs = crafts.Count > 0 ? crafts : null;

            return response;
        }

        private void ResolveNodeResults(List<CraftNodeDB> selectedNodes)
        {
            var nodeGroups = _excelService.GetTable<GachaCraftNodeGroupExcelT>();
            var nodeExcels = _excelService.GetTable<GachaCraftNodeExcelT>();
            var gachaElements = _excelService.GetTable<GachaElementExcelT>();

            foreach (var node in selectedNodes)
            {
                node.NodeQuality = nodeExcels.FirstOrDefault(x => x.ID == node.NodeId)?.NodeQuality ?? 1;
                // Official's final-tier node carries no LeafNodeIds key; the intermediate tiers keep the five choices they showed.
                if (node.LeafNodeIds is { Count: 0 })
                    node.LeafNodeIds = null;

                // Node -> reward: the node's gacha groups weighted by ProbWeight, then one roll inside the chosen group. ResultId is set to the chosen group id - its exact official semantics are not derivable from the capture, only its presence.
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
        }

        private static CraftInfoDBServer? GetSlot(SchaleDataContext db, long accountServerId, long slotId) =>
            db.CraftInfos.FirstOrDefault(x => x.AccountServerId == accountServerId && x.SlotSequence == slotId);

        // Five distinct choices from the next tier's node pool, like every captured node offers. Official's choice weighting is unknown; uniform across the tier's nodes.
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
