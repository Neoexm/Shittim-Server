using BlueArchiveAPI.Services;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.Logic.Battles.Summary;

namespace Shittim_Server.Services;

public class RaidManager
{
    private readonly ExcelTableService excelTableService;

    private readonly List<RaidSeasonManageExcelT> raidSeasonExcels;
    private readonly List<RaidStageExcelT> raidStageExcels;
    private readonly List<CharacterStatExcelT> characterStatExcels;

    public RaidManager(ExcelTableService _excelTableService)
    {
        excelTableService = _excelTableService;

        raidSeasonExcels = excelTableService.GetTable<RaidSeasonManageExcelT>();
        raidStageExcels = excelTableService.GetTable<RaidStageExcelT>();
        characterStatExcels = excelTableService.GetTable<CharacterStatExcelT>();
    }

    public DateTime GetRaidTimeTicks(AccountDBServer account)
    {
        return DateTime.Parse(
            raidSeasonExcels.FirstOrDefault(x => x.SeasonId == account.ContentInfo.RaidDataInfo.SeasonId).SeasonStartData
        );
    }

    public SingleRaidLobbyInfoDBServer GetLobbyData(SchaleDataContext context, AccountDBServer account)
    {
        return context.SingleRaidLobbyInfos.FirstOrDefault(x => x.AccountServerId == account.ServerId);
    }

    public RaidDBServer GetRaidData(SchaleDataContext context, AccountDBServer account)
    {
        return context.Raids.FirstOrDefault(x =>
            x.AccountServerId == account.ServerId &&
            x.RaidState == RaidStatus.Playing &&
            x.ContentType == ContentType.Raid
        );
    }

    public RaidBattleDBServer GetRaidBattleData(SchaleDataContext context, AccountDBServer account)
    {
        return context.RaidBattles.FirstOrDefault(x =>
            x.AccountServerId == account.ServerId &&
            x.ContentType == ContentType.Raid &&
            x.IsClear == false);
    }

    public RaidSummaryDB GetRaidSummaryData(AccountDBServer account, long raidId, long battleRaidId)
    {
        return account.RaidSummaries.FirstOrDefault(x =>
            x.ContentType == ContentTypeSummary.Raid &&
            x.BattleStatus == BattleStatus.Pending &&
            x.RaidDBId == raidId &&
            x.BattleRaidDBId == battleRaidId);
    }

    public RaidStageExcelT GetRaidStage(AccountDBServer account)
    {
        var targetSeason = raidSeasonExcels.FirstOrDefault(x => x.SeasonId == account.ContentInfo.RaidDataInfo.SeasonId);
        var targetStage = raidStageExcels.FirstOrDefault(x => x.Id == account.ContentInfo.RaidDataInfo.CurrentRaidUniqueId);

        return targetStage;
    }

    public async Task<SingleRaidLobbyInfoDBServer> GetUpdatedLobby(SchaleDataContext context, AccountDBServer account)
    {
        var targetSeason = raidSeasonExcels.FirstOrDefault(x => x.SeasonId == account.ContentInfo.RaidDataInfo.SeasonId);

        var raidLobby = GetLobbyData(context, account);

        DateTime serverTime = DateTime.Parse(targetSeason.SeasonStartData);

        if (raidLobby == null)
        {
            raidLobby = new SingleRaidLobbyInfoDBServer()
            {
                AccountServerId = account.ServerId,
                Tier = 4,
                Ranking = 1,
                SeasonId = account.ContentInfo.RaidDataInfo.SeasonId,
                BestRankingPoint = account.ContentInfo.RaidDataInfo.BestRankingPoint,
                TotalRankingPoint = account.ContentInfo.RaidDataInfo.TotalRankingPoint,
                ReceiveRewardIds = targetSeason.SeasonRewardId,
                PlayableHighestDifficulty = new Dictionary<string, Difficulty>()
                {
                    { targetSeason.OpenRaidBossGroup.FirstOrDefault(), Difficulty.Lunatic }
                },
                SweepPointByRaidUniqueId = new Dictionary<long, long>(),
                SeasonStartDate = serverTime.AddHours(-3),
                SeasonEndDate = serverTime.AddDays(4),
                SettlementEndDate = serverTime.AddDays(5),
                NextSeasonId = account.ContentInfo.RaidDataInfo.SeasonId + 1,
                NextSeasonStartDate = serverTime.AddMonths(1),
                NextSeasonEndDate = serverTime.AddMonths(1).AddDays(7),
                NextSettlementEndDate = serverTime.AddMonths(1).AddDays(8),
                RemainFailCompensation = new Dictionary<int, bool>() { { 0, true } },
                ReceivedRankingRewardId = new(),
                ReceiveLimitedRewardIds = new List<long>(),
                CanReceiveRankingReward = false,
                ClearDifficulty = new List<Difficulty>() { Difficulty.Lunatic },
            };
            context.SingleRaidLobbyInfos.Add(raidLobby);
        }
        else
        {
            if (raidLobby.SeasonId != account.ContentInfo.RaidDataInfo.SeasonId)
            {
                raidLobby.SeasonId = account.ContentInfo.RaidDataInfo.SeasonId;
                raidLobby.SeasonStartDate = serverTime.AddHours(-3);
                raidLobby.SeasonEndDate = serverTime.AddDays(4);
                raidLobby.SettlementEndDate = serverTime.AddDays(5);
                raidLobby.NextSeasonId = account.ContentInfo.RaidDataInfo.SeasonId + 1;
                raidLobby.NextSeasonStartDate = serverTime.AddMonths(1);
                raidLobby.NextSeasonEndDate = serverTime.AddMonths(1).AddDays(7);
                raidLobby.NextSettlementEndDate = serverTime.AddMonths(1).AddDays(8);
            }
            raidLobby.BestRankingPoint = account.ContentInfo.RaidDataInfo.BestRankingPoint;
            raidLobby.TotalRankingPoint = account.ContentInfo.RaidDataInfo.TotalRankingPoint;
        }
        await context.SaveChangesAsync();
        return raidLobby;
    }

    public async Task<RaidDBServer> CreateOrUpdateRaid(SchaleDataContext context, AccountDBServer account, bool isPractice, long raidId)
    {
        var targetSeason = raidSeasonExcels.FirstOrDefault(x => x.SeasonId == account.ContentInfo.RaidDataInfo.SeasonId);
        var targetStage = raidStageExcels.FirstOrDefault(x => x.Id == raidId);

        var raidLobby = GetLobbyData(context, account);
        var raid = GetRaidData(context, account);

        DateTime serverTime = DateTime.Parse(targetSeason.SeasonStartData);

        if (raid == null)
        {
            raid = new RaidDBServer()
            {
                AccountServerId = account.ServerId,
                Owner = new()
                {
                    AccountId = account.ServerId,
                    AccountName = account.Nickname,
                    CharacterId = account.RepresentCharacterServerId
                },
                ContentType = ContentType.Raid,
                RaidState = RaidStatus.Playing,
                SeasonId = account.ContentInfo.RaidDataInfo.SeasonId,
                UniqueId = raidId,
                SecretCode = "0",
                Begin = serverTime,
                End = serverTime.AddHours(1),
                PlayerCount = 1,
                IsPractice = isPractice,
                AccountLevelWhenCreateDB = account.Level,
                RaidBossDBs = RaidService.CreateRaidBosses(targetStage.BossCharacterId, characterStatExcels),
            };
            context.Raids.Add(raid);
        }
        else
        {
            raid.BossDifficulty = account.ContentInfo.RaidDataInfo.CurrentDifficulty;
            raid.UniqueId = raidId;
            raid.IsPractice = isPractice;
        }

        raidLobby.PlayingRaidDB = raid;
        await context.SaveChangesAsync();

        raid = GetRaidData(context, account);

        return raid;
    }

    public async Task<RaidBattleDBServer> CreateOrUpdateBattle(SchaleDataContext context, AccountDBServer account, long raidId)
    {
        var targetSeason = raidSeasonExcels.FirstOrDefault(x => x.SeasonId == account.ContentInfo.RaidDataInfo.SeasonId);
        var targetStage = raidStageExcels.FirstOrDefault(x => x.Id == raidId);
        var targetBoss = characterStatExcels.FirstOrDefault(y => y.CharacterId == targetStage.BossCharacterId.FirstOrDefault());

        var raidBattle = GetRaidBattleData(context, account);

        if (raidBattle == null)
        {
            raidBattle = new()
            {
                AccountServerId = account.ServerId,
                ContentType = ContentType.Raid,
                RaidUniqueId = raidId,
                CurrentBossHP = targetBoss.MaxHP100,
                RaidMembers = [
                    new() {
                        AccountId = account.ServerId,
                        AccountName = account.Nickname,
                        CharacterId = account.RepresentCharacterServerId,
                        DamageCollection = new()
                    }
                ]
            };
            context.RaidBattles.Add(raidBattle);
        }
        else
        {
            raidBattle.RaidUniqueId = raidId;
        }

        await context.SaveChangesAsync();
        return raidBattle;
    }

    public async Task<bool> SaveBattle(SchaleDataContext context, AccountDBServer account, BattleSummary summary, bool isPractice)
    {
        var targetSeason = raidSeasonExcels.FirstOrDefault(x => x.SeasonId == account.ContentInfo.RaidDataInfo.SeasonId);
        var targetStage = raidStageExcels.FirstOrDefault(x => x.Id == account.ContentInfo.RaidDataInfo.CurrentRaidUniqueId);

        var raidLobby = GetLobbyData(context, account);
        var raid = GetRaidData(context, account);
        var raidBattle = GetRaidBattleData(context, account);
        var raidSummary = GetRaidSummaryData(account, raid.ServerId, raidBattle.ServerId);

        RaidService.CalculateRaidCollection(raidBattle, summary.RaidSummary);
        RaidService.UpdateRaidBossStats(summary, characterStatExcels,
            targetStage.GroundDevName, targetStage.Difficulty, targetStage.BossCharacterId,
            raid, raidBattle, raidLobby);
        RaidService.UpdateCharacterParticipation(account, raidLobby, summary);

        var battleId = Guid.NewGuid().ToString();
        if (raidSummary == null)
        {
            raidSummary = new RaidSummaryDB()
            {
                AccountServerId = account.ServerId,
                SeasonId = account.ContentInfo.RaidDataInfo.SeasonId,
                RaidStageId = account.ContentInfo.RaidDataInfo.CurrentRaidUniqueId,
                ContentType = ContentTypeSummary.Raid,
                RaidDBId = raid.ServerId,
                BattleRaidDBId = raidBattle.ServerId,
                Difficulty = account.ContentInfo.RaidDataInfo.CurrentDifficulty,
                IsMock = isPractice,
                CurrentTeam = 2,
                BattleSummaryIds = [battleId]
            };
            await context.RaidSummaries.AddAsync(raidSummary);
        }
        else
        {
            raidSummary.BattleSummaryIds.Add(battleId);
            raidSummary.CurrentTeam++;
        }

        var newBattleSummary = RaidService.CreateBattleSummary(account, raidSummary, targetStage.BossCharacterId, raid, raidBattle, battleId, summary);
        await context.BattleSummaries.AddAsync(newBattleSummary);

        context.Raids.Update(raid);
        context.RaidBattles.Update(raidBattle);
        context.SingleRaidLobbyInfos.Update(raidLobby);
        await context.SaveChangesAsync();

        bool raidCleared = raid != null && raid.RaidBossDBs.All(b => b.BossCurrentHP == 0);
        return raidCleared;
    }

    public async Task EndBossBattle(SchaleDataContext context, AccountDBServer account, RaidStatus raidStatus, long totalScore = 0)
    {
        var raidLobby = GetLobbyData(context, account);
        var raids = context.Raids.FirstOrDefault(r => r.AccountServerId == account.ServerId && r.RaidState == RaidStatus.Playing && r.ContentType == ContentType.Raid);
        var battles = context.RaidBattles.FirstOrDefault(b => b.AccountServerId == account.ServerId && b.IsClear == false && b.ContentType == ContentType.Raid);
        var raidSummary = account.RaidSummaries.FirstOrDefault(b => b.ContentType == ContentTypeSummary.Raid);

        raidLobby.PlayingRaidDB = null;
        raidLobby.ParticipateCharacterServerIds = [];
        raids.RaidState = RaidStatus.Clear;
        battles.IsClear = true;

        if (raidSummary != null)
        {
            raidSummary.BattleStatus =
                raids != null && raids.RaidBossDBs.All(b => b.BossCurrentHP == 0) ?
                BattleStatus.Win : BattleStatus.Lose;
            raidSummary.Score = totalScore;
            context.RaidSummaries.Update(raidSummary);
        }

        context.SingleRaidLobbyInfos.Update(raidLobby);
        context.Raids.Update(raids);
        context.RaidBattles.Update(battles);

        account.ContentInfo.RaidDataInfo.TimeBonus = 0;
        context.Entry(account).Property(x => x.ContentInfo).IsModified = true;

        await context.SaveChangesAsync();
    }
}
