using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.GameLogic.DBModel;
using Schale.MX.Logic.Battles.Summary;
using Schale.MX.Logic.Data;

namespace Schale.Data.GameModel
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
            var allCharacters = CollectAllCharacters(summary);

            foreach (var character in allCharacters)
            {
                var characterData = BuildCharacterData(accountId, character);
                Characters.Add(characterData);
            }
        }

        private HeroSummaryCollection CollectAllCharacters(BattleSummary summary)
        {
            HeroSummaryCollection collection = [];
            
            if (summary.Group01Summary != null)
            {
                if (summary.Group01Summary.Heroes != null)
                    collection.Add(summary.Group01Summary.Heroes);
                
                if (summary.Group01Summary.Supporters != null)
                    collection.Add(summary.Group01Summary.Supporters);
            }

            return collection;
        }

        private CharacterData BuildCharacterData(long accountId, HeroSummary character)
        {
            return new CharacterData
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
                PotentialStats = ConvertPotentialStats(character.PotentialStatLevel),
                WeaponDatas = ConvertWeaponData(character.CharacterWeapon),
                EquipmentDatas = ConvertEquipmentData(character.Equipments),
                GearData = ConvertGearData(character.CharacterGear)
            };
        }

        private Dictionary<int, int> ConvertPotentialStats(IDictionary<StatType, int>? potentialStats)
        {
            if (potentialStats == null)
                return new() { { 1, 0 }, { 2, 0 }, { 4, 0 } };

            return potentialStats.Select(stat => new KeyValuePair<int, int>(
                MapStatTypeToServerId(stat.Key),
                stat.Value
            )).ToDictionary(x => x.Key, x => x.Value);
        }

        private int MapStatTypeToServerId(StatType statType)
        {
            return statType switch
            {
                StatType.MaxHP => 1,
                StatType.AttackPower => 2,
                StatType.HealPower => 4,
                _ => 0
            };
        }

        private WeaponData? ConvertWeaponData(WeaponSetting? weapon)
        {
            if (weapon == null || !weapon.Value.IsValid) return null;

            return new WeaponData
            {
                UniqueId = weapon.Value.UniqueId,
                StarGrade = weapon.Value.StarGrade,
                Level = weapon.Value.Level
            };
        }

        private Dictionary<long, EquipmentData>? ConvertEquipmentData(List<EquipmentSetting>? equipments)
        {
            if (equipments == null) return null;

            return equipments
                .Where(eq => eq.IsValid)
                .ToDictionary(
                    eq => eq.UniqueId,
                    eq => new EquipmentData { Tier = eq.Tier, Level = eq.Level }
                );
        }

        private GearData? ConvertGearData(GearSetting? gear)
        {
            if (gear == null || !gear.Value.IsValid) return null;

            return new GearData
            {
                UniqueId = gear.Value.UniqueId,
                Tier = gear.Value.Tier,
                Level = gear.Value.Level
            };
        }

        public void SaveBossData(List<long> bossId, List<RaidBossDBServer> bossDatas, BattleSummary summary, RaidMemberCollection raidMembers)
        {
            if (summary.RaidSummary == null) return;

            for (var i = 0; i < bossId.Count; i++)
            {
                var bossData = bossDatas[i];
                var raidSummary = CreateRaidSummaryBoss(bossId[i], bossData, summary);
                BossDatas.Add(raidSummary);
            }
            
            RaidMembers = raidMembers;
        }

        private RaidSummaryBossDB CreateRaidSummaryBoss(long id, RaidBossDBServer bossData, BattleSummary summary)
        {
            var raidSummary = new RaidSummaryBossDB
            {
                ContentType = bossData.ContentType,
                BossId = id,
                BossIndex = bossData.BossIndex,
                BossCurrentHP = bossData.BossCurrentHP,
                BossGroggyPoint = bossData.BossGroggyPoint
            };

            if (summary.RaidSummary.RaidBossResults == null)
                return raidSummary;

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

            return raidSummary;
        }

        public List<RaidBossDBServer> ConvertToRaidBossDB()
        {
            if (BossDatas == null) return [];

            return BossDatas.Select(x => new RaidBossDBServer
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


