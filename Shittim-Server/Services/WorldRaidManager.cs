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
        var worldRaidSeasons = _excelTableService.GetTable<WorldRaidSeasonManageExcelT>();
        var worldSeasonExcel = worldRaidSeasons.GetWorldRaidSeasonById(req.SeasonId);

        var worldRaidStages = _excelTableService.GetTable<WorldRaidStageExcelT>();
        var worldRaidLocalBosses = new List<WorldRaidLocalBossDBServer>();

        foreach (var bossGroupId in worldSeasonExcel.OpenRaidBossGroupId)
        {
            var worldRaidStageExcelList = worldRaidStages.GetWorldRaidStageExcelsByGroupId(bossGroupId);
            
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
        var worldRaidSeasons = _excelTableService.GetTable<WorldRaidSeasonManageExcelT>();
        var worldSeasonExcel = worldRaidSeasons.GetWorldRaidSeasonById(req.SeasonId);

        var worldRaidBossGroups = _excelTableService.GetTable<WorldRaidBossGroupExcelT>();
        var bossList = new List<WorldRaidBossListInfoDBServer>();

        foreach (var bossGroupId in worldSeasonExcel.OpenRaidBossGroupId)
        {
            var worldRaidBossList = await context.WorldRaidBossListInfos
                .FirstOrDefaultAsync(x => x.GroupId == bossGroupId);

            if (worldRaidBossList == null)
            {
                var bossGroupExcel = worldRaidBossGroups.GetWorldRaidBossGroupById(bossGroupId);
                var worldRaidWorldBossDB = new WorldRaidWorldBossDBServer
                {
                    GroupId = bossGroupId,
                    HP = bossGroupExcel.WorldBossHP
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
            var pooledHP = WorldRaidService.RemainingHP(bossGroupId);
            if (pooledHP != null)
            {
                worldRaidBossList.WorldBossDB.HP = pooledHP.Value;
                worldRaidBossList.WorldBossDB.Participants = WorldRaidService.Participants(bossGroupId);
                await context.SaveChangesAsync();
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
        var worldRaidStages = _excelTableService.GetTable<WorldRaidStageExcelT>();
        var targetStage = worldRaidStages.GetWorldRaidStageExcelById(req.UniqueId);

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

        var worldRaidStages = _excelTableService.GetTable<WorldRaidStageExcelT>();
        var targetStage = worldRaidStages.GetWorldRaidStageExcelById(req.UniqueId);

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

            var enterTicket = _excelTableService.GetTable<WorldRaidSeasonManageExcelT>().GetWorldRaidSeasonById(req.SeasonId).EnterTicket;

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
            WorldRaidService.AddDamage(req.GroupId, contribution);
            if (WorldRaidService.RemainingHP(req.GroupId) == null)
            {
                var bossListInfo = await context.WorldRaidBossListInfos.FirstOrDefaultAsync(x => x.GroupId == req.GroupId);
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

        var worldSeasonExcel = _excelTableService.GetTable<WorldRaidSeasonManageExcelT>().GetWorldRaidSeasonById(seasonId);
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

    // world boss clear rewards, claimable once per boss group after the shared pool hits zero. 821/823 ship no clear reward groups so this correctly hands them nothing.
    public async Task<ParcelResultDB?> ReceiveReward(
        SchaleDataContext context,
        AccountDBServer account,
        WorldRaidReceiveRewardRequest req)
    {
        var worldRaidSeasons = _excelTableService.GetTable<WorldRaidSeasonManageExcelT>();
        var worldSeasonExcel = worldRaidSeasons.GetWorldRaidSeasonById(req.SeasonId);
        var worldRaidBossGroups = _excelTableService.GetTable<WorldRaidBossGroupExcelT>();
        var worldRaidRewards = _excelTableService.GetTable<WorldRaidStageRewardExcelT>();

        var claimed = context.GetAccountWorldRaidClearHistories(account.ServerId)
            .GetWorldRaidClearHistoriesBySeasonId(req.SeasonId)
            .Select(x => x.GroupId)
            .ToHashSet();

        var parcelResult = new List<ParcelResult>();
        foreach (var bossGroupId in worldSeasonExcel.OpenRaidBossGroupId)
        {
            if (claimed.Contains(bossGroupId))
                continue;

            var bossGroupExcel = worldRaidBossGroups.GetWorldRaidBossGroupById(bossGroupId);
            if (bossGroupExcel.WorldBossClearRewardGroupId == 0)
                continue;

            var remaining = WorldRaidService.RemainingHP(bossGroupId)
                ?? (await context.WorldRaidBossListInfos.FirstOrDefaultAsync(x => x.GroupId == bossGroupId))?.WorldBossDB.HP;
            if (remaining == null || remaining > 0)
                continue;

            foreach (var reward in worldRaidRewards.GetWorldRaidStageRewardByGroupId(bossGroupExcel.WorldBossClearRewardGroupId))
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
