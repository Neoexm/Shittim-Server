using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Plana.FlatData;
using Plana.MX.Logic.Battles.Summary;
using Plana.MX.Logic.Data;

namespace Plana.Database.GameModel
{
    public class BattleSummaryDB
    {
        [JsonIgnore]
        public virtual AccountDBServer? Account { get; set; }

        public long AccountServerId { get; set; }

        [Key]
        public required string BattleId { get; set; }

        public long SnapshotId { get; set; }
        public List<RaidSummaryBossDB> BossDatas { get; set; } = [];
        public List<CharacterData> Characters { get; set; } = [];
        public RaidMemberCollection RaidMembers { get; set; } = [];
        public float ElapsedRealtime { get; set; }
        public long EndFrame { get; set; }
        public DateTime TimeStamp { get; set; }

        public void SaveTimingData(AccountDBServer account, BattleSummary summary)
        {
            ElapsedRealtime = summary.ElapsedRealtime;
            EndFrame = summary.EndFrame;
            TimeStamp = account.GameSettings?.ServerDateTime() ?? DateTime.Now;
        }

        public void SaveCharacterData(long accountId, BattleSummary summary)
        {
            HeroSummaryCollection allCharacters = [];
            if (summary.Group01Summary != null)
            {
                if (summary.Group01Summary.Heroes != null) allCharacters.Add(summary.Group01Summary.Heroes);
                if (summary.Group01Summary.Supporters != null) allCharacters.Add(summary.Group01Summary.Supporters);
            }

            foreach (var character in allCharacters)
            {
                var characterData = new CharacterData()
                {
                    IsBorrowed = character.OwnerAccountId != accountId,
                    CharacterDBId = character.ServerId,
                    UniqueId = character.CharacterId,
                    StarGrade = character.Grade,
                    Level = character.Level,
                    ExSkillLevel = character.ExSkillLevel,
                    PublicSkillLevel = character.PublicSkillLevel,
                    PassiveSkillLevel = character.PassiveSkillLevel,
                    ExtraPassiveSkillLevel = character.ExtraPassiveSkillLevel,
                    FavorRank = character.FavorRank,
                    PotentialStats = character.PotentialStatLevel != null ? character.PotentialStatLevel.Select(x =>
                    {
                        var serverStatId = x.Key
                        switch
                        {
                            StatType.MaxHP => 1,
                            StatType.AttackPower => 2,
                            StatType.HealPower => 4,
                            _ => 0
                        };
                        return new KeyValuePair<int, int>(serverStatId, x.Value);
                    }).ToDictionary(x => x.Key, x => x.Value) : new () { { 1, 0 }, { 2, 0 }, { 4, 0 } },
                    WeaponDatas = character.CharacterWeapon != null ? new()
                    {
                        UniqueId = character.CharacterWeapon.Value.UniqueId,
                        StarGrade = character.CharacterWeapon.Value.StarGrade,
                        Level = character.CharacterWeapon.Value.Level
                    } : null,
                    EquipmentDatas = character.Equipments != null ?
                    character.Equipments.Select(x =>
                    {
                        return new KeyValuePair<long, EquipmentData>(x.UniqueId, new()
                        {
                            Tier = x.Tier,
                            Level = x.Level
                        });
                    }).ToDictionary(x => x.Key, x => x.Value) : null,
                    GearData = character.CharacterGear != null ? new()
                    {
                        UniqueId = character.CharacterGear.Value.UniqueId,
                        Tier = character.CharacterGear.Value.Tier,
                        Level = character.CharacterGear.Value.Level,
                    } : null
                };
                this.Characters.Add(characterData);
            }
        }

        public void SaveBossData(List<long> bossId, List<RaidBossDBServer> bossDatas, BattleSummary summary, RaidMemberCollection raidMembers)
        {
            if (summary.RaidSummary == null) return;
            for (var i = 0; i < bossId.Count; i++)
            {
                var bossData = bossDatas[i];
                var raidSummary = new RaidSummaryBossDB()
                {
                    ContentType = bossData.ContentType,
                    BossId = bossId[i],
                    BossIndex = bossData.BossIndex,
                    BossCurrentHP = bossData.BossCurrentHP,
                    BossGroggyPoint = bossData.BossGroggyPoint
                };
                if (summary.RaidSummary.RaidBossResults == null) continue;
                if (summary.RaidSummary.RaidBossResults.Contains(bossData.BossIndex))
                {
                    var bossSummary = summary.RaidSummary.RaidBossResults[bossData.BossIndex];
                    raidSummary.BossAIPhase = bossSummary.AIPhase;
                    raidSummary.BossSubPartsHPs = bossSummary.SubPartsHPs;
                }
                else
                {
                    raidSummary.BossAIPhase = 0;
                    raidSummary.BossSubPartsHPs = null;
                }
                this.BossDatas.Add(raidSummary);

            }
            this.RaidMembers = raidMembers;
        }

        public List<RaidBossDBServer> ConvertToRaidBossDB()
        {
            if (this.BossDatas == null) return new List<RaidBossDBServer>();
            return this.BossDatas.Select(x => new RaidBossDBServer()
            {
                ContentType = x.ContentType,
                BossCurrentHP = x.BossCurrentHP,
                BossGroggyPoint = x.BossGroggyPoint,
                BossIndex = x.BossIndex,
            }).ToList();
        }
    }

    public class RaidSummaryBossDB
    {
        public ContentType ContentType { get; set; }
        public long BossId { get; set; }
        public int BossIndex { get; set; }
        public long BossCurrentHP { get; set; }
        public long BossGroggyPoint { get; set; }
        public long BossAIPhase { get; set; }
        public List<long>? BossSubPartsHPs { get; set; }
    }

    public class CharacterData
    {
        public bool IsBorrowed { get; set; }
        public long CharacterDBId { get; set; }
        public long UniqueId { get; set; }
        public int StarGrade { get; set; }
        public int Level { get; set; }
        public int FavorRank { get; set; }
        public int ExSkillLevel { get; set; }
        public int PublicSkillLevel { get; set; }
        public int PassiveSkillLevel { get; set; }
        public int ExtraPassiveSkillLevel { get; set; }
        public Dictionary<int, int>? PotentialStats { get; set; }
        public WeaponData? WeaponDatas { get; set; }
        public Dictionary<long, EquipmentData>? EquipmentDatas { get; set; }
        public GearData? GearData { get; set; }
    }

    public class WeaponData
    {
        public long UniqueId { get; set; }
        public long StarGrade { get; set; }
        public long Level { get; set; }
    }

    public class EquipmentData
    {
        public long Tier { get; set; }
        public long Level { get; set; }
    }

    public class GearData
    {
        public long UniqueId { get; set; }
        public long Tier { get; set; }
        public long Level { get; set; }
    }
}