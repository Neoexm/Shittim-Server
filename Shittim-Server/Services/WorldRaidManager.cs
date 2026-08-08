using AutoMapper;
using BlueArchiveAPI.Services;
using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.Excel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.GameLogic.Parcel;
using Schale.MX.Logic.Battles;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.Logic.Data;
using Schale.MX.NetworkProtocol;

namespace Shittim_Server.Services;

public class WorldRaidManager
{
    private readonly ExcelTableService _excelTableService;
    private readonly ParcelHandler _parcelHandler;
    private readonly IMapper _mapper;

    public WorldRaidManager(
        ExcelTableService excelTableService,
        ParcelHandler parcelHandler,
        IMapper mapper)
    {
        _excelTableService = excelTableService;
        _parcelHandler = parcelHandler;
        _mapper = mapper;
    }

    public async Task<List<WorldRaidLocalBossDBServer>> WorldRaidLobby(
        SchaleDataContext context,
        AccountDBServer account,
        WorldRaidLobbyRequest req)
    {
        var worldRaidStages = _excelTableService.GetTable<WorldRaidStageExcelT>();
        var worldRaidLocalBosses = new List<WorldRaidLocalBossDBServer>();

        foreach (var bossGroupId in SeasonBossGroups(req.SeasonId))
        {
            var worldRaidStageExcelList = worldRaidStages.GetWorldRaidStageExcelsByGroupId(bossGroupId);
            if (worldRaidStageExcelList.Count == 0)
                worldRaidStageExcelList = _excelTableService.GetTable<InteractiveWorldRaidStageExcelT>().Where(x => x.WorldRaidBossGroupId == bossGroupId).Select(AsClassicStage).ToList();

            foreach (var worldRaidStageExcel in worldRaidStageExcelList)
            {
                var worldRaidLocalDB = await context.WorldRaidLocalBosses
                    .FirstOrDefaultAsync(x => 
                        x.AccountServerId == account.ServerId && 
                        x.SeasonId == req.SeasonId &&
                        x.GroupId == bossGroupId &&
                        x.UniqueId == worldRaidStageExcel.Id);

                if (worldRaidLocalDB == null)
                {
                    worldRaidLocalDB = new WorldRaidLocalBossDBServer
                    {
                        AccountServerId = account.ServerId,
                        SeasonId = req.SeasonId,
                        GroupId = bossGroupId,
                        UniqueId = worldRaidStageExcel.Id,
                        IsScenario = worldRaidStageExcel.IsRaidScenarioBattle,
                        IsCleardEver = false,
                        TacticMscSum = 0,
                        IsContinue = false
                    };
                    context.WorldRaidLocalBosses.Add(worldRaidLocalDB);
                    await context.SaveChangesAsync();
                }

                worldRaidLocalBosses.Add(worldRaidLocalDB);
            }
        }

        return worldRaidLocalBosses;
    }

    public async Task<List<WorldRaidBossListInfoDBServer>> GetBossList(
        SchaleDataContext context,
        AccountDBServer account,
        WorldRaidBossListRequest req)
    {
        var worldRaidBossGroups = _excelTableService.GetTable<WorldRaidBossGroupExcelT>();
        var bossList = new List<WorldRaidBossListInfoDBServer>();

        foreach (var bossGroupId in SeasonBossGroups(req.SeasonId))
        {
            var worldRaidBossList = await context.WorldRaidBossListInfos
                .FirstOrDefaultAsync(x => x.GroupId == bossGroupId);

            if (worldRaidBossList == null)
            {
                var seedHP = worldRaidBossGroups.FirstOrDefault(x => x.WorldRaidBossGroupId == bossGroupId)?.WorldBossHP;
                if (seedHP == null)
                {
                    // interactive pools ship per region; the steam client is the global build
                    var interactiveGroup = _excelTableService.GetTable<InteractiveWorldRaidBossGroupExcelT>().First(x => x.WorldRaidBossGroupId == bossGroupId);
                    seedHP = interactiveGroup.WorldBossHPGlobal != 0 ? interactiveGroup.WorldBossHPGlobal : interactiveGroup.WorldBossHP;
                }

                var worldRaidWorldBossDB = new WorldRaidWorldBossDBServer
                {
                    GroupId = bossGroupId,
                    HP = seedHP.Value
                };

                worldRaidBossList = new WorldRaidBossListInfoDBServer
                {
                    GroupId = bossGroupId,
                    WorldBossDB = worldRaidWorldBossDB,
                    LocalBossDBs = new List<WorldRaidLocalBossDBServer>()
                };
                context.WorldRaidBossListInfos.Add(worldRaidBossList);
                await context.SaveChangesAsync();
            }

            // while a manifest is live the shared pool is authoritative; without one the row itself is the pool
            var poolGroupId = PoolGroup(bossGroupId);
            var pooledHP = WorldRaidService.RemainingHP(poolGroupId);
            if (pooledHP != null)
            {
                worldRaidBossList.WorldBossDB.HP = pooledHP.Value;
                worldRaidBossList.WorldBossDB.Participants = WorldRaidService.Participants(poolGroupId);
                await context.SaveChangesAsync();
            }
            else if (poolGroupId != bossGroupId)
            {
                // linked difficulties drain the root row, so show its bar here too
                var rootRow = await context.WorldRaidBossListInfos.FirstOrDefaultAsync(x => x.GroupId == poolGroupId);
                if (rootRow != null)
                {
                    worldRaidBossList.WorldBossDB.HP = rootRow.WorldBossDB.HP;
                    worldRaidBossList.WorldBossDB.Participants = rootRow.WorldBossDB.Participants;
                    await context.SaveChangesAsync();
                }
            }

            worldRaidBossList.LocalBossDBs = context.GetAccountWorldRaidLocalBosses(account.ServerId)
                .GetWorldRaidLocalBossesByGroupId(bossGroupId)
                .ToList();

            bossList.Add(worldRaidBossList);
        }

        return bossList;
    }

    public async Task<RaidBattleDBServer> EnterBattle(
        SchaleDataContext context,
        AccountDBServer account,
        WorldRaidEnterBattleRequest req)
    {
        var targetStage = GetStage(req.UniqueId);

        var characterStats = _excelTableService.GetTable<CharacterStatExcelT>();
        var targetBoss = characterStats.FirstOrDefault(y => y.CharacterId == targetStage.BossCharacterId.FirstOrDefault());

        var raidBattle = await context.RaidBattles
            .FirstOrDefaultAsync(x => 
                x.AccountServerId == account.ServerId &&
                x.ContentType == ContentType.WorldRaid &&
                x.RaidUniqueId == req.UniqueId &&
                !x.IsClear);

        if (raidBattle == null)
        {
            raidBattle = new RaidBattleDBServer
            {
                AccountServerId = account.ServerId,
                ContentType = ContentType.WorldRaid,
                RaidUniqueId = req.UniqueId,
                CurrentBossHP = targetBoss?.MaxHP100 ?? 10000000,
                CurrentBossGroggy = 0,
                IsClear = false,
                RaidMembers = new RaidMemberCollection()
            };
            context.RaidBattles.Add(raidBattle);
        }
        else
        {
            raidBattle.RaidUniqueId = req.UniqueId;
        }

        await context.SaveChangesAsync();

        return raidBattle;
    }

    public async Task<ParcelResultDB?> BattleResult(
        SchaleDataContext context,
        AccountDBServer account,
        WorldRaidBattleResultRequest req)
    {
        // practice fights touch nothing: no ticket, no pool damage, no clear
        if (req.IsPractice)
            return null;

        var targetStage = GetStage(req.UniqueId);

        var raidBattle = await context.RaidBattles
            .FirstOrDefaultAsync(x =>
                x.AccountServerId == account.ServerId &&
                x.ContentType == ContentType.WorldRaid &&
                x.RaidUniqueId == req.UniqueId &&
                !x.IsClear);

        ParcelResultDB? parcelResultDB = null;

        if (req.IsTicket)
        {
            // the client computed the cost itself: ReEnterAmount when it resumed a saved fight, the full RaidEnterAmount otherwise. Mirror that instead of trusting a flag it never sends.
            var characterStats = _excelTableService.GetTable<CharacterStatExcelT>();
            var freshBossHP = characterStats.FirstOrDefault(y => y.CharacterId == targetStage.BossCharacterId.FirstOrDefault())?.MaxHP100 ?? 10000000;
            var isContinue = targetStage.SaveCurrentLocalBossHP && raidBattle != null && raidBattle.CurrentBossHP < freshBossHP;
            var cost = isContinue ? targetStage.ReEnterAmount : targetStage.RaidEnterAmount;

            var classicSeason = _excelTableService.GetTable<WorldRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == req.SeasonId);
            var enterTicket = classicSeason?.EnterTicket ?? default;
            if (classicSeason == null)
            {
                // interactive seasons charge per phase and the replay phase reopens every boss on its own currency, so the latest phase that has started is the one the client is paying with. The manifest is what the client's phase dates were patched from; without one the shipped dates decide.
                var phases = _excelTableService.GetTable<InteractiveWorldRaidSeasonManageExcelT>()
                    .Where(x => x.SeasonId == req.SeasonId && x.OpenRaidBossGroupId.Contains(req.GroupId))
                    .OrderBy(x => x.PhaseId)
                    .ToList();
                if (phases.Count > 0)
                {
                    enterTicket = phases[0].EnterTicket;
                    var manifest = WorldRaidService.Manifest;
                    var now = DateTime.Now;
                    foreach (var phase in phases)
                    {
                        DateTime start;
                        if (manifest != null && manifest.seasonId == req.SeasonId)
                        {
                            if (phase.IsReplaySeason)
                            {
                                if (!WorldRaidService.TryLocal(manifest.close, out start))
                                    continue;
                            }
                            else if (phase.PhaseStartCondition == 0)
                            {
                                if (!WorldRaidService.TryLocal(manifest.open, out start))
                                    continue;
                            }
                            else
                            {
                                var spawns = phase.OpenRaidBossGroupId.Select(WorldRaidService.BossWindow).Where(w => w.HasValue).Select(w => w!.Value.Spawn).ToList();
                                if (spawns.Count == 0)
                                    continue;
                                start = spawns.Min();
                            }
                        }
                        else if (!DateTime.TryParse(phase.PhaseStartTime, out start))
                            continue;

                        if (start <= now)
                            enterTicket = phase.EnterTicket;
                    }
                }
            }

            var currency = await context.Currencies.FirstOrDefaultAsync(x => x.AccountServerId == account.ServerId);
            if (cost > 0 && currency != null && currency.CurrencyDict.TryGetValue(enterTicket, out var tickets) && tickets >= cost)
            {
                var resolver = await _parcelHandler.BuildParcel(context, account,
                    new ParcelResult(ParcelType.Currency, (long)enterTicket, cost),
                    isConsume: true);
                parcelResultDB = resolver.ParcelResult;
            }
        }

        var summary = req.Summary.RaidSummary;

        // fixed-quota stages (821's scripted fights) credit DamageToWorldBoss on a clear, everything else credits what was actually dealt
        var contribution = targetStage.DamageToWorldBoss > 0
            ? (req.Summary.EndType == BattleEndType.Clear ? targetStage.DamageToWorldBoss : 0)
            : summary.GivenDamage;
        if (contribution > 0)
        {
            var poolGroupId = PoolGroup(req.GroupId);
            WorldRaidService.AddDamage(poolGroupId, contribution);
            if (WorldRaidService.RemainingHP(poolGroupId) == null)
            {
                var bossListInfo = await context.WorldRaidBossListInfos.FirstOrDefaultAsync(x => x.GroupId == poolGroupId);
                if (bossListInfo != null)
                {
                    bossListInfo.WorldBossDB.HP = Math.Max(0, bossListInfo.WorldBossDB.HP - contribution);
                    if (bossListInfo.WorldBossDB.Participants == 0)
                        bossListInfo.WorldBossDB.Participants = 1;
                }
            }
        }

        if (req.Summary.EndType != BattleEndType.Clear)
        {
            // a retreat keeps the saved fight; the next entry resumes the boss from whatever was left
            if (raidBattle != null && targetStage.SaveCurrentLocalBossHP)
            {
                raidBattle.CurrentBossHP = Math.Max(1, raidBattle.CurrentBossHP - summary.GivenDamage);
                raidBattle.CurrentBossGroggy = summary.TotalGroggyCount;
            }
            await context.SaveChangesAsync();
            return parcelResultDB;
        }

        var worldRaidRewards = _excelTableService.GetTable<WorldRaidStageRewardExcelT>();
        var rewardStage = worldRaidRewards.GetWorldRaidStageRewardByGroupId(targetStage.RaidRewardGroupId);

        var clearedRaidBattle = await context.RaidBattles
            .FirstOrDefaultAsync(x =>
                x.AccountServerId == account.ServerId &&
                x.ContentType == ContentType.WorldRaid &&
                x.RaidUniqueId == req.UniqueId &&
                x.IsClear);

        if (clearedRaidBattle == null)
        {
            var parcelResult = new List<ParcelResult>();
            foreach (var reward in rewardStage)
            {
                parcelResult.Add(new ParcelResult(
                    reward.ClearStageRewardParcelType,
                    reward.ClearStageRewardParcelUniqueID,
                    reward.ClearStageRewardAmount));
            }

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelResult, parcelResultDB);
            parcelResultDB = parcelResolver.ParcelResult;
        }

        if (raidBattle != null)
        {
            CalculateRaidCollection(raidBattle, req.Summary.RaidSummary);
            raidBattle.IsClear = true;
            raidBattle.CurrentBossHP -= req.Summary.RaidSummary.GivenDamage;
            raidBattle.CurrentBossGroggy = req.Summary.RaidSummary.TotalGroggyCount;
            await context.SaveChangesAsync();
        }

        var worldRaidLocalDB = await context.WorldRaidLocalBosses
            .FirstOrDefaultAsync(x =>
                x.AccountServerId == account.ServerId &&
                x.SeasonId == req.SeasonId &&
                x.GroupId == req.GroupId &&
                x.UniqueId == req.UniqueId);

        if (worldRaidLocalDB == null)
        {
            worldRaidLocalDB = new WorldRaidLocalBossDBServer
            {
                AccountServerId = account.ServerId,
                SeasonId = req.SeasonId,
                GroupId = req.GroupId,
                UniqueId = req.UniqueId,
                IsScenario = targetStage.IsRaidScenarioBattle,
                RaidBattleDB = raidBattle,
                TacticMscSum = (long)(req.Summary.EndFrame / 30f * 1000),
                IsCleardEver = true,
                IsContinue = true
            };
            context.WorldRaidLocalBosses.Add(worldRaidLocalDB);
        }
        else
        {
            worldRaidLocalDB.IsCleardEver = true;
            worldRaidLocalDB.RaidBattleDB = raidBattle;
            worldRaidLocalDB.TacticMscSum += (long)(req.Summary.EndFrame / 30f * 1000);
            worldRaidLocalDB.IsContinue = true;
        }

        await context.SaveChangesAsync();

        return parcelResultDB;
    }

    // the client indexes BossGroups[phaseId] without a guard, so this is either null (excel dates rule) or a dict that definitely holds the season key. Entries overwrite the excel spawn/eliminate windows by group id, which is how the manifest's boss schedule reaches the player without touching ExcelDB.
    public Dictionary<long, List<WorldRaidBossGroup>>? BuildBossGroups(long seasonId)
    {
        var manifest = WorldRaidService.Manifest;
        if (manifest == null || manifest.seasonId != seasonId)
            return null;

        // interactive seasons get their schedule written straight into the client's ExcelDB phase rows, so there is nothing to overwrite from here
        var worldSeasonExcel = _excelTableService.GetTable<WorldRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == seasonId);
        if (worldSeasonExcel == null)
            return null;

        var groups = new List<WorldRaidBossGroup>();
        foreach (var bossGroupId in worldSeasonExcel.OpenRaidBossGroupId)
        {
            var window = WorldRaidService.BossWindow(bossGroupId);
            if (window == null)
                continue;
            groups.Add(new WorldRaidBossGroup
            {
                ContentsChangeType = ContentsChangeType.WorldRaidBossGroupDate,
                ContentType = ContentType.WorldRaid,
                GroupId = bossGroupId,
                BossSpawnTime = window.Value.Spawn,
                EliminateTime = window.Value.Eliminate
            });
        }

        return new Dictionary<long, List<WorldRaidBossGroup>> { [seasonId] = groups };
    }

    // world boss clear rewards, claimable once per boss group after the shared pool hits zero. 821/823 ship no clear reward groups so this correctly hands them nothing, and every 854 group ships zero too.
    public async Task<ParcelResultDB?> ReceiveReward(
        SchaleDataContext context,
        AccountDBServer account,
        WorldRaidReceiveRewardRequest req)
    {
        var worldRaidBossGroups = _excelTableService.GetTable<WorldRaidBossGroupExcelT>();
        var worldRaidRewards = _excelTableService.GetTable<WorldRaidStageRewardExcelT>();

        var claimed = context.GetAccountWorldRaidClearHistories(account.ServerId)
            .GetWorldRaidClearHistoriesBySeasonId(req.SeasonId)
            .Select(x => x.GroupId)
            .ToHashSet();

        var parcelResult = new List<ParcelResult>();
        foreach (var bossGroupId in SeasonBossGroups(req.SeasonId))
        {
            if (claimed.Contains(bossGroupId))
                continue;

            var clearRewardGroupId = worldRaidBossGroups.FirstOrDefault(x => x.WorldRaidBossGroupId == bossGroupId)?.WorldBossClearRewardGroupId
                ?? _excelTableService.GetTable<InteractiveWorldRaidBossGroupExcelT>().First(x => x.WorldRaidBossGroupId == bossGroupId).WorldBossClearRewardGroupId;
            if (clearRewardGroupId == 0)
                continue;

            var poolGroupId = PoolGroup(bossGroupId);
            var remaining = WorldRaidService.RemainingHP(poolGroupId)
                ?? (await context.WorldRaidBossListInfos.FirstOrDefaultAsync(x => x.GroupId == poolGroupId))?.WorldBossDB.HP;
            if (remaining == null || remaining > 0)
                continue;

            foreach (var reward in worldRaidRewards.GetWorldRaidStageRewardByGroupId(clearRewardGroupId))
            {
                parcelResult.Add(new ParcelResult(
                    reward.ClearStageRewardParcelType,
                    reward.ClearStageRewardParcelUniqueID,
                    reward.ClearStageRewardAmount));
            }

            context.WorldRaidClearHistories.Add(new WorldRaidClearHistoryDBServer
            {
                AccountServerId = account.ServerId,
                SeasonId = req.SeasonId,
                GroupId = bossGroupId,
                RewardReceiveDate = DateTime.Now
            });
        }

        ParcelResultDB? parcelResultDB = null;
        if (parcelResult.Count > 0)
            parcelResultDB = (await _parcelHandler.BuildParcel(context, account, parcelResult)).ParcelResult;

        await context.SaveChangesAsync();

        return parcelResultDB;
    }

    // interactive seasons (854) are one row per phase rather than one per season; the union covers every group the client can ask about, and the client filters what it shows by its own phase dates
    private List<long> SeasonBossGroups(long seasonId)
    {
        var season = _excelTableService.GetTable<WorldRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == seasonId);
        if (season != null)
            return season.OpenRaidBossGroupId;

        return _excelTableService.GetTable<InteractiveWorldRaidSeasonManageExcelT>()
            .Where(x => x.SeasonId == seasonId)
            .SelectMany(x => x.OpenRaidBossGroupId)
            .Distinct()
            .ToList();
    }

    // 854's stages live in the interactive table but carry the same battle fields, so an interactive row gets dressed up as a classic one and the rest of the flow does not care
    private WorldRaidStageExcelT GetStage(long uniqueId)
    {
        var stage = _excelTableService.GetTable<WorldRaidStageExcelT>().FirstOrDefault(x => x.Id == uniqueId);
        if (stage != null)
            return stage;

        return AsClassicStage(_excelTableService.GetTable<InteractiveWorldRaidStageExcelT>().First(x => x.Id == uniqueId));
    }

    private static WorldRaidStageExcelT AsClassicStage(InteractiveWorldRaidStageExcelT stage) => new()
    {
        Id = stage.Id,
        WorldRaidBossGroupId = stage.WorldRaidBossGroupId,
        BossCharacterId = stage.BossCharacterId,
        RaidEnterAmount = stage.RaidEnterAmount,
        ReEnterAmount = stage.ReEnterAmount,
        RaidRewardGroupId = stage.RaidRewardGroupId,
        RaidBattleEndRewardGroupId = stage.RaidBattleEndRewardGroupId,
        DamageToWorldBoss = stage.DamageToWorldBoss,
        SaveCurrentLocalBossHP = stage.SaveCurrentLocalBossHP,
        IsRaidScenarioBattle = stage.IsRaidScenarioBattle
    };

    // Malkuth's two difficulties share one health bar: WorldBossHPLinkGroup names the pool a group drains, zero means the group is its own pool
    private long PoolGroup(long groupId)
    {
        var link = _excelTableService.GetTable<InteractiveWorldRaidBossGroupExcelT>().FirstOrDefault(x => x.WorldRaidBossGroupId == groupId)?.WorldBossHPLinkGroup ?? 0;
        return link != 0 ? link : groupId;
    }

    private static void CalculateRaidCollection(RaidBattleDBServer raidBattle, RaidSummary summary)
    {
        var raidMember = raidBattle.RaidMembers.FirstOrDefault();
        if (raidMember == null) return;

        foreach (var raidDamageResult in summary.RaidBossResults)
        {
            var existingDamageCol = raidMember.DamageCollection
                .FirstOrDefault(x => x.Index == raidDamageResult.RaidDamage.Index);

            if (existingDamageCol != null)
            {
                existingDamageCol.GivenDamage += raidDamageResult.RaidDamage.GivenDamage;
                existingDamageCol.GivenGroggyPoint += raidDamageResult.RaidDamage.GivenGroggyPoint;
            }
            else
            {
                raidMember.DamageCollection.Add(new RaidDamage
                {
                    Index = raidDamageResult.RaidDamage.Index,
                    GivenDamage = raidDamageResult.RaidDamage.GivenDamage,
                    GivenGroggyPoint = raidDamageResult.RaidDamage.GivenGroggyPoint
                });
            }
        }
    }
}
