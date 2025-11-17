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
        if (req.Summary.EndType != BattleEndType.Clear)
            return null;

        var worldRaidStages = _excelTableService.GetTable<WorldRaidStageExcelT>();
        var targetStage = worldRaidStages.GetWorldRaidStageExcelById(req.UniqueId);

        var worldRaidRewards = _excelTableService.GetTable<WorldRaidStageRewardExcelT>();
        var rewardStage = worldRaidRewards.GetWorldRaidStageRewardByGroupId(targetStage.RaidRewardGroupId);

        ParcelResultDB? parcelResultDB = null;

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

            var parcelResolver = await _parcelHandler.BuildParcel(context, account, parcelResult);
            parcelResultDB = parcelResolver.ParcelResult;
        }

        var raidBattle = await context.RaidBattles
            .FirstOrDefaultAsync(x =>
                x.AccountServerId == account.ServerId &&
                x.ContentType == ContentType.WorldRaid &&
                x.RaidUniqueId == req.UniqueId &&
                !x.IsClear);

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
