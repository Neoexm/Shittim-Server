using Schale.Data;
using Schale.Data.GameModel;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.Logic.Data;

namespace Shittim_Server.Services;

public static class RaidService
{
    public static void CalculateRaidCollection(RaidBattleDBServer raidBattle, RaidSummary summary)
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

    public static AssistCharacterDB FinishingAssistCharacterInfo(
        AssistCharacterDB? assistCharacter, 
        ClanAssistUseInfo clanAssistUse)
    {
        if (assistCharacter == null)
            return new AssistCharacterDB();

        assistCharacter.CombatStyleIndex = clanAssistUse.CombatStyleIndex;
        assistCharacter.IsMulligan = clanAssistUse.IsMulligan;
        assistCharacter.IsTSAInteraction = clanAssistUse.IsTSAInteraction;
        return assistCharacter;
    }

    public static List<long> CharacterParticipation(GroupSummary groupSummary, bool bypassTeam)
    {
        if (bypassTeam) 
            return new List<long>();

        var heroes = groupSummary.Heroes.Select(x => x.ServerId);
        var supporters = groupSummary.Supporters?.Select(x => x.ServerId) ?? Enumerable.Empty<long>();
        
        return heroes.Concat(supporters).ToList();
    }

    public static List<RaidBossDBServer> CreateRaidBosses(
        List<long> bossIds, 
        List<CharacterStatExcelT> characterStatExcels)
    {
        return bossIds.Select((bossId, index) =>
        {
            var characterStat = characterStatExcels.FirstOrDefault(y => y.CharacterId == bossId);
            
            return new RaidBossDBServer
            {
                ContentType = ContentType.Raid,
                BossCurrentHP = characterStat?.MaxHP100 ?? 10000000,
                BossGroggyPoint = 0,
                BossIndex = index
            };
        }).ToList();
    }

    public static void UpdateCharacterParticipation(
        AccountDBServer account,
        RaidLobbyInfoDBServer raidLobby,
        BattleSummary summary)
    {
        var characterIds = CharacterParticipation(
            summary.Group01Summary, 
            account.GameSettings.BypassTeamDeployment);

        if (raidLobby.PlayingRaidDB.ParticipateCharacterServerIds == null)
            raidLobby.PlayingRaidDB.ParticipateCharacterServerIds = new Dictionary<long, List<long>>();

        if (raidLobby.PlayingRaidDB.ParticipateCharacterServerIds.ContainsKey(account.ServerId))
        {
            raidLobby.PlayingRaidDB.ParticipateCharacterServerIds[account.ServerId].AddRange(characterIds);
            raidLobby.ParticipateCharacterServerIds.AddRange(characterIds);
        }
        else
        {
            raidLobby.PlayingRaidDB.ParticipateCharacterServerIds[account.ServerId] = characterIds;
            raidLobby.ParticipateCharacterServerIds = characterIds;
        }
    }

    public static BattleSummaryDB CreateBattleSummary(
        AccountDBServer account,
        RaidSummaryDB raidSummary,
        List<long> bossIds,
        RaidDBServer raid,
        RaidBattleDBServer raidBattle,
        string battleId,
        BattleSummary summary)
    {
        var newBattleSummary = new BattleSummaryDB
        {
            AccountServerId = account.ServerId,
            SnapshotId = raidSummary.SnapshotId,
            BattleId = battleId
        };

        newBattleSummary.SaveTimingData(account, summary);
        newBattleSummary.SaveCharacterData(account.ServerId, summary);
        newBattleSummary.SaveBossData(bossIds, raid.RaidBossDBs, summary, raidBattle.RaidMembers);

        return newBattleSummary;
    }

    public static void UpdateRaidBossStats(
        BattleSummary summary,
        List<CharacterStatExcelT> characterStatExcels,
        string groundDevName,
        Difficulty difficulty,
        List<long> bossCharacterId,
        RaidDBServer raid,
        RaidBattleDBServer raidBattle,
        RaidLobbyInfoDBServer raidLobby)
    {
        foreach (var bossResult in summary.RaidSummary.RaidBossResults)
        {
            var characterStat = characterStatExcels.FirstOrDefault(x => 
                x.CharacterId == bossCharacterId[bossResult.RaidDamage.Index]);

            long hpLeft = raid.RaidBossDBs[bossResult.RaidDamage.Index].BossCurrentHP - 
                          bossResult.RaidDamage.GivenDamage;
            long givenGroggy = raid.RaidBossDBs[bossResult.RaidDamage.Index].BossGroggyPoint + 
                               bossResult.RaidDamage.GivenGroggyPoint;
            long groggyPoint = givenGroggy;
            long bossAIPhase = AIPhaseCheck(
                bossResult.RaidDamage.Index, hpLeft, bossResult.AIPhase,
                groundDevName, difficulty, bossCharacterId, characterStatExcels);

            if (hpLeft <= 0)
            {
                raid.RaidBossDBs[bossResult.RaidDamage.Index].BossCurrentHP = 0;
                raid.RaidBossDBs[bossResult.RaidDamage.Index].BossGroggyPoint = groggyPoint;

                int nextBossIndex = bossResult.RaidDamage.Index + 1;
                if (nextBossIndex < raid.RaidBossDBs.Count)
                {
                    long nextBossAIPhase = AIPhaseCheck(
                        nextBossIndex, hpLeft, bossResult.AIPhase,
                        groundDevName, difficulty, bossCharacterId, characterStatExcels);
                    
                    var nextBoss = raid.RaidBossDBs[nextBossIndex];
                    raidBattle.CurrentBossHP = nextBoss.BossCurrentHP;
                    raidBattle.CurrentBossGroggy = 0;
                    raidBattle.CurrentBossAIPhase = nextBossAIPhase;
                    raidBattle.SubPartsHPs = bossResult.SubPartsHPs;
                    raidBattle.RaidBossIndex = nextBossIndex;
                }
                else
                {
                    raidBattle.CurrentBossHP = 0;
                    raidBattle.CurrentBossGroggy = groggyPoint;
                    raidBattle.CurrentBossAIPhase = bossResult.AIPhase;
                    raidBattle.SubPartsHPs = bossResult.SubPartsHPs;
                }
            }
            else
            {
                raid.RaidBossDBs[bossResult.RaidDamage.Index].BossCurrentHP = hpLeft;
                raid.RaidBossDBs[bossResult.RaidDamage.Index].BossGroggyPoint = groggyPoint;

                raidBattle.CurrentBossHP = hpLeft;
                raidBattle.CurrentBossGroggy = groggyPoint;
                raidBattle.CurrentBossAIPhase = bossAIPhase;
                raidBattle.SubPartsHPs = bossResult.SubPartsHPs;
            }
        }

        raidLobby.PlayingRaidDB.RaidBossDBs = raid.RaidBossDBs;
    }

    public static long AIPhaseCheck(
        int bossIndex,
        long bossHp,
        long previousPhase,
        string bossName,
        Difficulty difficulty,
        List<long> bossCharacterId,
        List<CharacterStatExcelT> characterStatExcels)
    {
        var characterStat = characterStatExcels.FirstOrDefault(x => 
            x.CharacterId == bossCharacterId[bossIndex]);
        var maxHp = characterStat?.MaxHP100 ?? 0;

        if (bossName == "HOD") 
            return previousPhase;

        var indexBasedPhases = new Dictionary<string, (int targetIndex, int phaseValue)>
        {
            { "ShiroKuro", (1, 7) },
            { "Kaitenger", (1, 1) },
            { "HoverCraft", (1, 4) }
        };

        if (indexBasedPhases.TryGetValue(bossName, out var indexPhase) && 
            bossIndex == indexPhase.targetIndex)
        {
            return indexPhase.phaseValue;
        }

        var hpThresholds = new Dictionary<string, (string targetDiff, float threshold, int phase)[]>
        {
            { "Chesed", new[] { ("", 1f, 1) } },
            { "Hieronymus", new[] { ("", 0.5f, 1) } },
            { "Goz", new[] {
                ("", 0.6f, 1),
                ("Lunatic", 0.75f, 1)
            }},
            { "EN0006", new[] {
                ("", 0.75f, 1),
                ("", 0.1f, 2)
            }},
            { "EN0010", new[] { ("", 0.6f, 1) } },
        };

        if (hpThresholds.TryGetValue(bossName, out var thresholdsForBoss))
        {
            var effectivePhaseThresholds = new Dictionary<float, int>();

            foreach (var (targetDiffName, threshold, phase) in thresholdsForBoss)
            {
                if (string.IsNullOrEmpty(targetDiffName))
                {
                    effectivePhaseThresholds[threshold] = phase;
                }
            }

            foreach (var (targetDiffName, threshold, phase) in thresholdsForBoss)
            {
                if (targetDiffName.Equals(difficulty.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    effectivePhaseThresholds[threshold] = phase;
                }
            }

            foreach (var entry in effectivePhaseThresholds.OrderBy(e => e.Key))
            {
                if (bossHp <= (maxHp * entry.Key))
                {
                    return entry.Value;
                }
            }
        }

        if (bossName == "Binah")
        {
            var difficultyIndex = (int)difficulty;
            var secondPhase = new[] { 2f / 3, 5f / 8, 7f / 11, 12f / 20, 3.5f / 6, 4f / 7, 13.2f / 23 };
            var thirdPhase = new[] { 1f / 3, 2f / 8, 3f / 11, 4f / 20, 1f / 6, 1.5f / 7, 4.9f / 23 };

            if (bossHp <= (maxHp * thirdPhase[difficultyIndex])) 
                return 2;
            else if (bossHp <= (maxHp * secondPhase[difficultyIndex])) 
                return 1;
        }

        return 0;
    }
}
