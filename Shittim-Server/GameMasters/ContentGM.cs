using Shittim.Models.GM;
using Shittim.Services;
using Shittim.Services.Client;
using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Serilog;
using BlueArchiveAPI.Services;

namespace Shittim.GameMasters
{
    public class ContentGM
    {
        public static List<GrandAssaultDataWeb> GenGrandAssaultData(ExcelTableService excelTableService)
        {
            var characterList = excelTableService.GetTable<CharacterExcelT>();
            var raidSeasonData = excelTableService.GetTable<EliminateRaidSeasonManageExcelT>();
            var raidStageList = excelTableService.GetTable<EliminateRaidStageExcelT>();
            var groundList = excelTableService.GetTable<GroundExcelT>();

            var normalStages = raidStageList.Where(rs => rs.Difficulty == Difficulty.Normal).ToList();

            var grandAssault = raidSeasonData.Select(raid =>
            {
                var bg1 = raid.OpenRaidBossGroup01;
                var bg2 = raid.OpenRaidBossGroup02;
                var bg3 = raid.OpenRaidBossGroup03;

                var raidStageNormal = normalStages.Find(rs => rs.RaidBossGroup == bg1);
                var raidCharacterForArmor = characterList.Find(c => c.Id == raidStageNormal.RaidCharacterId);
                var ground = groundList.Find(g => g.Id == raidStageNormal.GroundId);

                var raidStageInsane = raidStageList.Find(rs => rs.RaidBossGroup == bg1 && rs.Difficulty == Difficulty.Insane);
                var raidCharacterForAttack = characterList.Find(c => c.Id == raidStageInsane.RaidCharacterId);

                var bossName = bg1.Contains('_') ? bg1.Substring(0, bg1.IndexOf('_')) : bg1;

                var a1 = bg1.Substring(bg1.LastIndexOf('_') + 1);
                var a2 = bg2.Substring(bg2.LastIndexOf('_') + 1);
                var a3 = bg3.Substring(bg3.LastIndexOf('_') + 1);

                return new GrandAssaultDataWeb
                {
                    SeasonId = raid.SeasonId,
                    BossName = bossName,
                    Date = raid.SeasonStartData.Split(' ')[0],
                    PortraitPath = raidStageNormal.PortraitPath.Split('/').Last(),
                    BGPath = raidStageNormal.BGPath.Split('/').Last(),
                    GroundType = ground.StageTopography.ToString(),
                    AttackType = raidCharacterForAttack.BulletType.ToString(),
                    ArmorTypes = new List<string> { a1, a2, a3 },
                };
            }).ToList();

            return grandAssault;
        }
        public static List<TADDataWeb> GenTADData(ExcelTableService excelTableService)
        {
            var timeAttackDungeonData = excelTableService.GetTable<TimeAttackDungeonSeasonManageExcelT>();
            var geasExcel = excelTableService.GetTable<TimeAttackDungeonGeasExcelT>();

            var result = timeAttackDungeonData.Select(dungeon =>
            {
                var lastGeasId = dungeon.DifficultyGeas.Last();
                var geasEntry = geasExcel.Find(g => g.Id == lastGeasId);

                return new TADDataWeb
                {
                    Id = dungeon.Id,
                    Date = dungeon.StartDate.Split(' ')[0],
                    DungeonType = geasEntry.TimeAttackDungeonType.ToString(),
                    GeasIconPaths = geasEntry.GeasIconPath
                                    .Select(path => path.Split('/').Last().ToLower())
                                    .ToList()
                };
            }).ToList();

            return result;
        }
        public static List<MultiFloorDataWeb> GenMultiFloorData(ExcelTableService excelTableService)
        {
            var characterList = excelTableService.GetTable<CharacterExcelT>();
            var multiFloorRaidData = excelTableService.GetTable<MultiFloorRaidSeasonManageExcelT>();
            var multiFloorStageData = excelTableService.GetTable<MultiFloorRaidStageExcelT>();
            var groundList = excelTableService.GetTable<GroundExcelT>();

            var result = multiFloorRaidData.Select(raid =>
            {
                var bossGroupId = raid.OpenRaidBossGroupId;
                var bossName = bossGroupId.Contains('_')
                                ? bossGroupId.Substring(0, bossGroupId.IndexOf('_'))
                                : bossGroupId;

                var raidStage = multiFloorStageData.Find(rs => rs.BossGroupId == bossGroupId && rs.Difficulty == 120);
                var raidCharacter = characterList.Find(c => c.Id == raidStage.RaidCharacterId);
                var ground = groundList.Find(g => g.Id == raidStage.GroundId);

                return new MultiFloorDataWeb
                {
                    SeasonId = raid.SeasonId,
                    BossName = bossName,
                    Date = raid.SeasonStartDate.Split(' ')[0],
                    GroundType = ground.StageTopography.ToString(),
                    AttackType = raidCharacter.BulletType.ToString(),
                    ArmorType = raidCharacter.ArmorType.ToString(),
                };
            }).ToList();

            return result;
        }
        
        public static List<TotalAssaultDataWeb> GenTotalAssaultData(ExcelTableService excelTableService)
        {
            var characterList = excelTableService.GetTable<CharacterExcelT>();
            var raidSeasonData = excelTableService.GetTable<RaidSeasonManageExcelT>();
            var raidStageList = excelTableService.GetTable<RaidStageExcelT>();
            var groundList = excelTableService.GetTable<GroundExcelT>();
            var stage = raidStageList.Where(rs => rs.Difficulty == Difficulty.Normal).ToList();

            var totalAssault = raidSeasonData.Select(raid =>
            {
                var bossGroup = raid.OpenRaidBossGroup.First();
                var raidStage = stage.Find(rs => rs.RaidBossGroup == bossGroup);
                var raidCharacter = characterList.Find(c => c.Id == raidStage.RaidCharacterId);
                var ground = groundList.Find(g => g.Id == raidStage.GroundId);

                var raidStageInsane = raidStageList.Find(rs => rs.RaidBossGroup == bossGroup && rs.Difficulty == Difficulty.Insane);
                var raidCharacterForAttack = characterList.Find(c => c.Id == raidStageInsane.RaidCharacterId);

                return new TotalAssaultDataWeb
                {
                    SeasonId = raid.SeasonId,
                    BossName = raid.OpenRaidBossGroup.First() is var rawName && rawName.Contains('_')
                                ? rawName[..rawName.LastIndexOf('_')]
                                : rawName,
                    Date = raid.SeasonStartData.Split(' ')[0],
                    PortraitPath = raidStage.PortraitPath.Split('/').Last(),
                    BGPath = raidStage.BGPath.Split('/').Last(),
                    GroundType = ground.StageTopography.ToString(),
                    AttackType = raidCharacterForAttack.BulletType.ToString(),
                    ArmorType = raidCharacter.ArmorType.ToString(),
                };
            }).ToList();

            return totalAssault;
        }

        public static async Task SetContentSeason(
            IClientConnection connection, AccountDBServer account, SchaleDataContext context,
            string type, long seasonId)
        {
            switch (type)
            {
                case "total":
                    var raidSeason = connection.ExcelTableService.GetTable<RaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == seasonId);
                    if (raidSeason == null)
                    {
                        await connection.SendChatMessage("Season ID does not exist");
                        return;
                    }

                    await SetRaidSeason(context, account, connection.ExcelTableService, seasonId);

                    await connection.SendChatMessage($"Total Assault Boss: {string.Join(", ", raidSeason.OpenRaidBossGroup)}");
                    await connection.SendChatMessage($"Total Assault ID: {raidSeason.SeasonId}");
                    await connection.SendChatMessage($"Total Assault StartTime: {raidSeason.SeasonStartData}");
                    await connection.SendChatMessage($"Total Assault Raid is set to {seasonId}");
                    break;

                case "drill":
                    var TADSeasonData = connection.ExcelTableService.GetTable<TimeAttackDungeonSeasonManageExcelT>().FirstOrDefault(x => x.Id == seasonId);
                    if (TADSeasonData == null)
                    {
                        await connection.SendChatMessage("Season ID does not exist");
                        return;
                    }
                    var TADExcel = connection.ExcelTableService.GetTable<TimeAttackDungeonExcelT>().FirstOrDefault(x => x.Id == TADSeasonData.DungeonId);

                    await SetTimeAttackDungeonSeason(context, account, seasonId);

                    await connection.SendChatMessage($"Joint Firing Drill Type: {string.Join(", ", TADExcel.TimeAttackDungeonType)}");
                    await connection.SendChatMessage($"Joint Firing Drill ID: {TADSeasonData.Id}");
                    await connection.SendChatMessage($"Joint Firing Drill StartTime: {TADSeasonData.StartDate}");
                    await connection.SendChatMessage($"Joint Firing Drill is set to {seasonId}");
                    break;

                case "grand":
                    var eliminateRaidSeason = connection.ExcelTableService.GetTable<EliminateRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == seasonId);
                    if (eliminateRaidSeason == null)
                    {
                        await connection.SendChatMessage("Season ID does not exist");
                        return;
                    }

                    await SetEliminateRaidSeason(context, account, connection.ExcelTableService, seasonId);

                    List<string> raidBoss = [
                        eliminateRaidSeason.OpenRaidBossGroup01,
                        eliminateRaidSeason.OpenRaidBossGroup02,
                        eliminateRaidSeason.OpenRaidBossGroup03
                    ];
                    await connection.SendChatMessage($"Grand Assault Boss: {string.Join(", ", raidBoss)}");
                    await connection.SendChatMessage($"Grand Assault ID: {eliminateRaidSeason.SeasonId}");
                    await connection.SendChatMessage($"Grand Assault StartTime: {eliminateRaidSeason.SeasonStartData}");
                    await connection.SendChatMessage($"Grand Assault Raid is set to {seasonId}");
                    break;
                case "final":
                    var multiFloorRaidSeasonList = connection.ExcelTableService.GetTable<MultiFloorRaidSeasonManageExcelT>();
                    if (!multiFloorRaidSeasonList.Any(x => x.SeasonId == seasonId))
                    {
                        await connection.SendChatMessage("Season ID does not exist");
                        return;
                    }
                    var multiFloorRaidSeason = multiFloorRaidSeasonList.FirstOrDefault(x => x.SeasonId == seasonId);

                    await SetMultiFloorRaidSeason(context, account, seasonId);

                    await connection.SendChatMessage($"Final Restriction Boss: {multiFloorRaidSeason.OpenRaidBossGroupId}");
                    await connection.SendChatMessage($"Final Restriction ID: {multiFloorRaidSeason.SeasonId}");
                    await connection.SendChatMessage($"Final Restriction StartTime: {multiFloorRaidSeason.SeasonStartDate}");
                    await connection.SendChatMessage($"Final Restriction is set to {seasonId}");
                    await connection.SendChatMessage($"Final Restriction has been enabled");
                    break;
                default:
                    await connection.SendChatMessage("Invalid type!");
                    return;
            }

            context.Entry(account).Property(x => x.ContentInfo).IsModified = true;
            await context.SaveChangesAsync();
        }

        public static async Task<IResult> SetContentSeasonWeb(
            ExcelTableService excelTableService, AccountDBServer account, SchaleDataContext context,
            string type, long seasonId)
        {
            switch (type.ToLower())
            {
                case "raids":
                    var raidSeason = excelTableService.GetTable<RaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == seasonId);
                    if (raidSeason == null)
                        return BaseAPIResponse.Create(ResponseStatus.Failure, "Season ID does not exist for Total Assault.");

                    await SetRaidSeason(context, account, excelTableService, seasonId);

                    Log.Information("Total Assault with Account Id {UserId} set to season {SeasonId}. Boss: {BossGroup}, StartTime: {StartTime}",
                        account.ServerId, seasonId, string.Join(", ", raidSeason.OpenRaidBossGroup), raidSeason.SeasonStartData);
                    break;

                case "timeattackdungeons":
                    var tadSeasonData = excelTableService.GetTable<TimeAttackDungeonSeasonManageExcelT>().FirstOrDefault(x => x.Id == seasonId);
                    if (tadSeasonData == null)
                        return BaseAPIResponse.Create(ResponseStatus.Failure, "Season ID does not exist for Joint Firing Drill.");

                    var tadExcel = excelTableService.GetTable<TimeAttackDungeonExcelT>().FirstOrDefault(x => x.Id == tadSeasonData.DungeonId);
                    if (tadExcel == null)
                        return BaseAPIResponse.Create(ResponseStatus.Failure, "Drill details not found for the given season.");

                    await SetTimeAttackDungeonSeason(context, account, seasonId);

                    Log.Information("Joint Firing Drill with Account Id {UserId} set to season {SeasonId}. Type: {DrillType}, StartTime: {StartTime}",
                        account.ServerId, seasonId, tadExcel.TimeAttackDungeonType, tadSeasonData.StartDate);
                    break;

                case "eliminateraids":
                    var eliminateRaidSeason = excelTableService.GetTable<EliminateRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == seasonId);
                    if (eliminateRaidSeason == null)
                        return BaseAPIResponse.Create(ResponseStatus.Failure, "Season ID does not exist for Grand Assault.");

                    await SetEliminateRaidSeason(context, account, excelTableService, seasonId);

                    List<string> raidBoss = new List<string> {
                        eliminateRaidSeason.OpenRaidBossGroup01,
                        eliminateRaidSeason.OpenRaidBossGroup02,
                        eliminateRaidSeason.OpenRaidBossGroup03
                    }.Where(b => !string.IsNullOrEmpty(b)).ToList();

                    Log.Information("Grand Assault with Account Id {UserId} set to season {SeasonId}. Boss: {BossGroup}, StartTime: {StartTime}",
                        account.ServerId, seasonId, string.Join(", ", raidBoss), eliminateRaidSeason.SeasonStartData);
                    break;

                case "multifloreraids":
                    var multiFloorRaidSeason = excelTableService.GetTable<MultiFloorRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == seasonId);
                    if (multiFloorRaidSeason == null)
                        return BaseAPIResponse.Create(ResponseStatus.Failure, "Season ID does not exist for Final Restriction.");

                    await SetMultiFloorRaidSeason(context, account, seasonId);

                    Log.Information("Final Restriction with Account Id {UserId} set to season {SeasonId} for user {UserId} and enabled. Boss: {BossGroup}, StartTime: {StartTime}",
                        account.ServerId, seasonId, multiFloorRaidSeason.OpenRaidBossGroupId, multiFloorRaidSeason.SeasonStartDate);
                    break;

                default:
                    Log.Warning("Invalid raid type received in SetRaid request: {RaidType}", type);
                    return BaseAPIResponse.Create(ResponseStatus.Failure, "Invalid raid type provided.");
            }

            try
            {
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save changes to the database after setting raid.");
                return BaseAPIResponse.Create(ResponseStatus.Error, "An error occurred while saving changes.");
            }

            return BaseAPIResponse.Create(ResponseStatus.Success, $"Successfully set {type} to season {seasonId}");
        }

        public static async Task SetRaidSeason(
            SchaleDataContext context, AccountDBServer account,
            ExcelTableService excelTableService, long seasonId)
        {
            var targetSeason = excelTableService.GetTable<RaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == seasonId);

            account.ContentInfo.RaidDataInfo.SeasonId = seasonId;
            account.ContentInfo.RaidDataInfo.TimeBonus = 0;
            account.ContentInfo.RaidDataInfo.BestRankingPoint = 0;
            account.ContentInfo.RaidDataInfo.TotalRankingPoint = 0;

            var raidLobby = context.GetAccountSingleRaidLobbyInfos(account.ServerId).FirstOrDefault();
            var raid = context.GetAccountRaids(account.ServerId).Where(x => x.RaidState == RaidStatus.Playing && x.ContentType == ContentType.Raid).ToList();
            var raidBattle = context.GetAccountRaidBattles(account.ServerId).Where(x => x.ContentType == ContentType.Raid && x.IsClear == false).ToList();
            var raidSummary = account.RaidSummaries.Where(x => x.ContentType == ContentTypeSummary.Raid && x.BattleStatus == BattleStatus.Pending).ToList();

            raid.ForEach(x => x.RaidState = RaidStatus.Clear);
            raidBattle.ForEach(x => x.IsClear = true);
            raidSummary.ForEach(x => x.BattleStatus = BattleStatus.Lose);
            if (raidLobby != null)
            {
                DateTime serverTime = DateTime.Parse(targetSeason.SeasonStartData);
                raidLobby.SeasonId = seasonId;
                raidLobby.ReceiveRewardIds = targetSeason.SeasonRewardId;
                raidLobby.PlayableHighestDifficulty = new Dictionary<string, Difficulty>()
                {
                    { targetSeason.OpenRaidBossGroup.FirstOrDefault(), Difficulty.Lunatic }
                };
                raidLobby.SeasonStartDate = serverTime.AddHours(-3);
                raidLobby.SeasonEndDate = serverTime.AddDays(4);
                raidLobby.SettlementEndDate = serverTime.AddDays(5);
                raidLobby.NextSeasonId = account.ContentInfo.RaidDataInfo.SeasonId + 1;
                raidLobby.NextSeasonStartDate = serverTime.AddMonths(1);
                raidLobby.NextSeasonEndDate = serverTime.AddMonths(1).AddDays(7);
                raidLobby.NextSettlementEndDate = serverTime.AddMonths(1).AddDays(8);
                raidLobby.BestRankingPoint = 0;
                raidLobby.TotalRankingPoint = 0;
                raidLobby.PlayingRaidDB = null;
                raidLobby.ParticipateCharacterServerIds = [];
                context.SingleRaidLobbyInfos.Update(raidLobby);
            }
            context.Raids.UpdateRange(raid);
            context.RaidBattles.UpdateRange(raidBattle);
            context.RaidSummaries.UpdateRange(raidSummary);

            await context.SaveChangesAsync();
        }

        public static async Task SetTimeAttackDungeonSeason(SchaleDataContext context, AccountDBServer account, long seasonId)
        {
            account.ContentInfo.TimeAttackDungeonDataInfo.SeasonId = seasonId;
            account.ContentInfo.TimeAttackDungeonDataInfo.SeasonBestRecord = 0;
            context.TimeAttackDungeonRooms.RemoveRange(context.GetAccountTimeAttackDungeonRooms(account.ServerId));
            context.TimeAttackDungeonBattleHistories.RemoveRange(context.GetAccountTimeAttackDungeonBattleHistoryDBs(account.ServerId));

            await context.SaveChangesAsync();
        }

        public static async Task SetEliminateRaidSeason(
            SchaleDataContext context, AccountDBServer account,
            ExcelTableService excelTableService, long seasonId)
        {
            var targetSeason = excelTableService.GetTable<EliminateRaidSeasonManageExcelT>().FirstOrDefault(x => x.SeasonId == seasonId);

            account.ContentInfo.EliminateRaidDataInfo.SeasonId = seasonId;
            account.ContentInfo.EliminateRaidDataInfo.TimeBonus = 0;
            account.ContentInfo.EliminateRaidDataInfo.BestRankingPoint = 0;
            account.ContentInfo.EliminateRaidDataInfo.TotalRankingPoint = 0;

            var eliminateRaidLobby = context.GetAccountEliminateRaidLobbyInfos(account.ServerId).FirstOrDefault();
            var eliminateRaid = context.GetAccountRaids(account.ServerId).Where(x => x.RaidState == RaidStatus.Playing && x.ContentType == ContentType.EliminateRaid).ToList();
            var eliminateRaidBattle = context.GetAccountRaidBattles(account.ServerId).Where(x => x.ContentType == ContentType.EliminateRaid && x.IsClear == false).ToList();
            var eliminateRaidSummary = account.RaidSummaries.Where(x => x.ContentType == ContentTypeSummary.EliminateRaid && x.BattleStatus == BattleStatus.Pending).ToList();

            eliminateRaid.ForEach(x => x.RaidState = RaidStatus.Clear);
            eliminateRaidBattle.ForEach(x => x.IsClear = true);
            eliminateRaidSummary.ForEach(x => x.BattleStatus = BattleStatus.Lose);

            if (eliminateRaidLobby != null)
            {
                DateTime serverTime = DateTime.Parse(targetSeason.SeasonStartData);
                eliminateRaidLobby.SeasonId = seasonId;
                eliminateRaidLobby.ReceiveRewardIds = targetSeason.SeasonRewardId;
                eliminateRaidLobby.PlayableHighestDifficulty = new()
                {
                    { targetSeason.OpenRaidBossGroup01, Difficulty.Lunatic },
                    { targetSeason.OpenRaidBossGroup02, Difficulty.Lunatic },
                    { targetSeason.OpenRaidBossGroup03, Difficulty.Lunatic }
                };
                eliminateRaidLobby.SeasonStartDate = serverTime.AddHours(-3);
                eliminateRaidLobby.SeasonEndDate = serverTime.AddDays(4);
                eliminateRaidLobby.SettlementEndDate = serverTime.AddDays(5);
                eliminateRaidLobby.NextSeasonId = account.ContentInfo.RaidDataInfo.SeasonId + 1;
                eliminateRaidLobby.NextSeasonStartDate = serverTime.AddMonths(1);
                eliminateRaidLobby.NextSeasonEndDate = serverTime.AddMonths(1).AddDays(7);
                eliminateRaidLobby.NextSettlementEndDate = serverTime.AddMonths(1).AddDays(8);
                eliminateRaidLobby.BestRankingPoint = 0;
                eliminateRaidLobby.TotalRankingPoint = 0;
                eliminateRaidLobby.PlayingRaidDB = null;
                eliminateRaidLobby.ParticipateCharacterServerIds = [];
                context.EliminateRaidLobbyInfos.Update(eliminateRaidLobby);
            }
            context.Raids.UpdateRange(eliminateRaid);
            context.RaidBattles.UpdateRange(eliminateRaidBattle);
            context.RaidSummaries.UpdateRange(eliminateRaidSummary);

            await context.SaveChangesAsync();
        }

        public static async Task SetMultiFloorRaidSeason(SchaleDataContext context, AccountDBServer account, long seasonId)
        {
            account.ContentInfo.MultiFloorRaidDataInfo.SeasonId = seasonId;
            account.GameSettings.EnableMultiFloorRaid = true;

            await context.SaveChangesAsync();
        }
    }
}
