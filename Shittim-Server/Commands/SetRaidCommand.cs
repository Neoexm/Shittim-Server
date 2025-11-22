using Shittim.Services.Client;
using Schale.Data.GameModel;
using Schale.Data.ModelMapping;
using Schale.FlatData;
using Microsoft.IdentityModel.Tokens;
using Schale.Data;

namespace Shittim.Commands
{
    [CommandHandler("setraid", "Command to change content teams data", "/setraid [type] [options] [snapshotid] [battleId]")]
    internal class SetRaidCommand : Command
    {
        public SetRaidCommand(IClientConnection connection, string[] args, bool validate = true) : base(connection, args, validate) { }

        [Argument(0, @"^(total|grand)$", "Type of content to set", ArgumentFlags.IgnoreCase)]
        public string Type { get; set; } = string.Empty;

        [Argument(1, @"^(to|show)$", "Type of options", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string Options { get; set; } = string.Empty;

        [Argument(2, @"^(?:[0-9])+$", "Snapshot ID (1, 2, 3...)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string SnapshotNum { get; set; } = string.Empty;

        [Argument(3, @"^(?:[0-9])+$", "Battle ID (1, 2, 3...)", ArgumentFlags.IgnoreCase | ArgumentFlags.Optional)]
        public string BattleNum { get; set; } = string.Empty;

        public override async Task Execute()
        {
            if (string.IsNullOrEmpty(Type) || Type.ToLower() == "help")
            {
                await ShowHelp();
                return;
            }

            using var context = await connection.Context.CreateDbContextAsync();
            var account = context.GetAccount(connection.AccountServerId);

            var parsedType = Type.ToLower() switch
            {
                "total" => ContentTypeSummary.Raid,
                "grand" => ContentTypeSummary.EliminateRaid,
                _ => ContentTypeSummary.Raid,
            };
            long parsedSnapshotNum = string.IsNullOrEmpty(SnapshotNum) ? -1 : long.Parse(SnapshotNum);
            long parsedBattleNum = string.IsNullOrEmpty(BattleNum) ? 0 : long.Parse(BattleNum);

            switch (Options.ToLower())
            {
                case "show":
                    await ShowSnapshotData(account, parsedType, parsedSnapshotNum, parsedBattleNum);
                    break;
                case "to":
                    await SetRaidData(context, account, parsedType, parsedSnapshotNum, parsedBattleNum);
                    break;
                default:
                    await ShowHelp();
                    return;
            }

            await context.SaveChangesAsync();
        }

        private async Task SetRaidData(
            SchaleDataContext context, AccountDBServer account, ContentTypeSummary contentType,
            long snapshotNum, long battleNum)
        {
            var raidType = contentType == ContentTypeSummary.Raid ? ContentType.Raid : ContentType.EliminateRaid;
            var raidSummary = account.RaidSummaries.FirstOrDefault(x => x.BattleStatus == BattleStatus.Pending && x.ContentType == contentType);
            if (raidSummary == null)
            {
                await connection.SendChatMessage("No pending raid data found");
                return;
            }

            RaidLobbyInfoDBServer? thisRaidLobby = raidType == ContentType.Raid ?
                context.GetAccountSingleRaidLobbyInfos(account.ServerId).FirstOrDefault() :
                context.GetAccountEliminateRaidLobbyInfos(account.ServerId).FirstOrDefault();
            if (thisRaidLobby == null)
            {
                await connection.SendChatMessage("No raid lobby found");
                return;
            }

            if (snapshotNum < 0)
            {
                await connection.SendChatMessage("Invalid snapshot number");
                return;
            }

            var raid = context.Raids.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.RaidState == RaidStatus.Playing && x.ContentType == raidType);
            var raidBattle = context.RaidBattles.FirstOrDefault(x => x.AccountServerId == account.ServerId && x.IsClear == false && x.ContentType == raidType);
            var isCurrent = snapshotNum == 0 || raidSummary.BattleSnapshotDatas.Count == 0;
            var snapshotDatas = isCurrent ?
                raidSummary.BattleSummaryIds :
                raidSummary.BattleSnapshotDatas.FirstOrDefault(x => x.Key == snapshotNum).Value;

            if (!snapshotDatas.Any())
            {
                await connection.SendChatMessage("Invalid snapshot number");
                return;
            }

            if (battleNum <= 0 || battleNum > snapshotDatas.Count)
            {
                await connection.SendChatMessage("Invalid battle number");
                await connection.SendChatMessage($"Battle Team Count: {snapshotDatas.Count}");
                return;
            }

            await ChangeRaidData(
                account, raidSummary,
                thisRaidLobby, raid, raidBattle,
                snapshotDatas, battleNum);

            var msgPart = isCurrent ? "current data" : $"snapshot {snapshotNum}";
            await connection.SendChatMessage($"Raid data has been rolled back to {msgPart}, battle {battleNum}");

            context.Raids.Update(raid);
            context.RaidBattles.Update(raidBattle);
            context.RaidSummaries.Update(raidSummary);
            if (thisRaidLobby is SingleRaidLobbyInfoDBServer singleRaidLobby)
                context.SingleRaidLobbyInfos.Update(singleRaidLobby);
            else if (thisRaidLobby is EliminateRaidLobbyInfoDBServer eliminateRaidLobby)
                context.EliminateRaidLobbyInfos.Update(eliminateRaidLobby);
            context.Entry(account).Property(x => x.ContentInfo).IsModified = true;

            await context.SaveChangesAsync();
        }

        private async Task ShowSnapshotData(
            AccountDBServer account, ContentTypeSummary contentType,
            long snapshotNum, long battleNum)
        {
            var raidType = contentType == ContentTypeSummary.Raid ? ContentType.Raid : ContentType.EliminateRaid;
            var raidSummary = account.RaidSummaries.FirstOrDefault(x => x.BattleStatus == BattleStatus.Pending && x.ContentType == contentType);
            if (raidSummary == null)
            {
                await connection.SendChatMessage("No pending raid data found");
                return;
            }

            if (snapshotNum < 0)
            {
                await connection.SendChatMessage($"----- [ Current Battles ] -----");
                await connection.SendChatMessage($"Battle Team Count: {raidSummary.BattleSummaryIds.Count}");
                foreach (var snapshot in raidSummary.BattleSnapshotDatas)
                {
                    await connection.SendChatMessage($"----- [ Snapshot {snapshot.Key} ] -----");
                    await connection.SendChatMessage($"Battle Team Count: {snapshot.Value.Count}");
                }
                return;
            }

            var characterStatExcels = connection.ExcelTableService.GetTable<CharacterStatExcelT>();
            var snapshotDatas = (snapshotNum == 0 || raidSummary.BattleSnapshotDatas.Count == 0) ?
                raidSummary.BattleSummaryIds :
                raidSummary.BattleSnapshotDatas.FirstOrDefault(x => x.Key == snapshotNum).Value;
            float timeElapsed = 0;

            if (!snapshotDatas.Any())
            {
                await connection.SendChatMessage("Invalid snapshot number");
                return;
            }

            if (battleNum < 0 || battleNum > snapshotDatas.Count)
            {
                await connection.SendChatMessage("Invalid battle number");
                return;
            }

            if (battleNum != 0)
            {
                var battles = account.BattleSummaries.Where(x => snapshotDatas.Contains(x.BattleId));
                timeElapsed = battles.Sum(x => x.ElapsedRealtime);
                var battle = account.BattleSummaries.FirstOrDefault(x => x.BattleId == snapshotDatas[(int)battleNum - 1]);

                await connection.SendChatMessage($"----- [ Team {battleNum} ] -----");
                await ShowBossHP(connection, characterStatExcels, battle);
                await connection.SendChatMessage($"Battle Time: {ShowTime(battle.ElapsedRealtime)}");
                await connection.SendChatMessage($"Total Time: {ShowTime(timeElapsed)}");

                return;
            }

            for (int i = 0; i < snapshotDatas.Count; i++)
            {
                var battle = account.BattleSummaries.FirstOrDefault(x => x.BattleId == snapshotDatas[i]);
                timeElapsed += battle.ElapsedRealtime;

                await connection.SendChatMessage($"----- [ Team {i + 1} ] -----");
                await ShowBossHP(connection, characterStatExcels, battle);
                await connection.SendChatMessage($"Battle Time: {ShowTime(battle.ElapsedRealtime)}");
            }
            await connection.SendChatMessage($"Total Time: {ShowTime(timeElapsed)}");
        }

        private async Task ChangeRaidData(
            AccountDBServer account, RaidSummaryDB raidSummary,
            RaidLobbyInfoDBServer raidLobby, RaidDBServer raid, RaidBattleDBServer raidBattle,
            List<string> battles, long battleNum)
        {
            var selectedBattle = account.BattleSummaries.FirstOrDefault(x => x.BattleId == battles[(int)battleNum - 1]);
            
            var newCurrentBattles = battles.Take((int)battleNum).ToList();
            bool snapshotExists = raidSummary.BattleSnapshotDatas.Values
                .Any(existingSnapshotList => 
                    existingSnapshotList.Count == newCurrentBattles.Count && 
                    existingSnapshotList.All(newCurrentBattles.Contains));

            if (!snapshotExists)
            {
                raidSummary.BattleSnapshotDatas[raidSummary.SnapshotId] = raidSummary.BattleSummaryIds;
                raidSummary.BattleSummaryIds = newCurrentBattles;
                raidSummary.SnapshotId++;
            }
            raidSummary.CurrentTeam = (int)battleNum;
            raid.RaidBossDBs = selectedBattle.ConvertToRaidBossDB();
            raidBattle.RaidMembers = selectedBattle.RaidMembers;
            
            foreach (var boss in selectedBattle.BossDatas)
            {
                if (boss.BossCurrentHP <= 0) continue;
                raidBattle.RaidBossIndex = boss.BossIndex;
                raidBattle.CurrentBossHP = boss.BossCurrentHP;
                raidBattle.CurrentBossGroggy = boss.BossGroggyPoint;
                raidBattle.CurrentBossAIPhase = boss.BossAIPhase;
                raidBattle.SubPartsHPs = boss.BossSubPartsHPs;
                break;
            }

            var allBattles = account.BattleSummaries.Where(x => raidSummary.BattleSummaryIds.Contains(x.BattleId)).ToList();

            raid.ParticipateCharacterServerIds = [];
            raidLobby.ParticipateCharacterServerIds = [];
            foreach (var battle in allBattles)
            {
                if (account.GameSettings.BypassTeamDeployment) break;
                List<long> characterId = battle.Characters.Select(x => {
                    if (x.IsBorrowed) return 0L;
                    return x.CharacterDBId;
                })
                .Where(id => id != 0L).Distinct().ToList();
                if (!raid.ParticipateCharacterServerIds.ContainsKey(account.ServerId))
                    raid.ParticipateCharacterServerIds[account.ServerId] = [];
                raid.ParticipateCharacterServerIds[account.ServerId].AddRange(characterId);
                raidLobby.ParticipateCharacterServerIds.AddRange(characterId);
            }

            raidLobby.PlayingRaidDB = raid;
        }

        private string ShowTime(float elapsedTimeInSeconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(elapsedTimeInSeconds);
            int minutes = (int)timeSpan.TotalMinutes;
            double seconds = timeSpan.Seconds + timeSpan.Milliseconds / 1000.0;
            if (minutes == 0) return $"{seconds:F2}s";
            return $"{minutes}m {seconds:F0}s";
        }

        private async Task ShowBossHP(IClientConnection connection, List<CharacterStatExcelT> characterStatExcels, BattleSummaryDB battle)
        {
            foreach (var (boss, index) in battle.BossDatas.Select((item, index) => (item, index)))
            {
                var bossData = characterStatExcels.FirstOrDefault(x => x.CharacterId == boss.BossId);
                await connection.SendChatMessage($"> Boss {index+1} <");
                await connection.SendChatMessage($"HP: {boss.BossCurrentHP} / {bossData.MaxHP100}");
                await connection.SendChatMessage($"Groggy: {boss.BossGroggyPoint} / {bossData.GroggyGauge}");
                await connection.SendChatMessage($"Phase: {boss.BossAIPhase}");
            }
        }

        private async Task ShowHelp()
        {
            await connection.SendChatMessage("/setraid - Command to change content teams data");
            await connection.SendChatMessage("Usage: /setraid <total|grand> <show|to> [snapshotNum] [battleNum]");
            await connection.SendChatMessage("Type: total, grand");
            await connection.SendChatMessage("Option: show, to");
            await connection.SendChatMessage(" show - Show current raid data");
            await connection.SendChatMessage(" to - Set raid data to current raid progress");
            await connection.SendChatMessage("SnapshotID: 0, 1, 2, 3, ... (0 for current raid data)");
            await connection.SendChatMessage("BattleID: 1, 2, 3, ...");
            await connection.SendChatMessage("/setraid total show 0 - Show all current raid data");
            await connection.SendChatMessage("/setraid total set 2 2 - Set current raid data to snapshot 2 team 2");
        }
    }
}
