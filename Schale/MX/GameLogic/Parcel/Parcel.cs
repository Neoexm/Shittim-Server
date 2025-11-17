using System.Text.Json;
using System.Text.Json.Serialization;
using Schale.FlatData;
using Schale.MX.Core.Math;
using Schale.MX.Data;
using Schale.MX.GameLogic.DBModel;

namespace Schale.MX.GameLogic.Parcel
{
    public enum ParcelChangeType
    {
        NoChange,
        Terminated,
        MailSend,
        Converted
    }

    public class CurrencyValue
    {
        public long AcademyTicket { get; set; }
        public long ActionPoint { get; set; }
        public long ArenaTicket { get; set; }
        public long ChaserTotalTicket { get; set; }
        public long EliminateTicketA { get; set; }
        public long EliminateTicketB { get; set; }
        public long EliminateTicketC { get; set; }
        public long EliminateTicketD { get; set; }
        public long Gem { get; set; }
        public long GemBonus { get; set; }
        public long GemPaid { get; set; }
        public long Gold { get; set; }
        public bool IsEmpty { get; set; }
        public long MasterCoin{ get; set; }
        public Dictionary<CurrencyTypes, long>? Property { get; set; }
        public long RaidTicket { get; set; }
        public long SchoolDungeonATicket{ get; set; }
        public long SchoolDungeonBTicket { get; set; }
        public long SchoolDungeonCTicket { get; set; }
        public long SchoolDungeonTotalTicket { get; set; }
        public Dictionary<CurrencyTypes, long>? Tickets { get; set; }
        public long TimeAttackDungeonTicket { get; set; }
        public Dictionary<CurrencyTypes, long>? Values { get; set; }
        public long WeekDungeonBloodTicket { get; set; }
        public long WeekDungeonChaserATicket { get; set; }
        public long WeekDungeonChaserBTicket { get; set; }
        public long WeekDungeonChaserCTicket { get; set; }
        public long WeekDungeonFindGiftTicket { get; set; }
        public long WorldRaidTicketA { get; set; }
        public long WorldRaidTicketB { get; set; }
        public long WorldRaidTicketC { get; set; }
    }

    public class FavorExpTransaction
    {
        public ParcelType Type { get; set; }

        [JsonIgnore]
        public IEnumerable<ParcelInfo>? ParcelInfos { get; set; }
        
        public long TargetCharacterUniqueId { get; set; }
        public long Amount { get; set; }
        public long Prob { get; set; }
    }

    public class FavorParcelValue
    {
        public int FavorRank { get; set; }
        public int FavorExp { get; set; }
        public bool IsEmpty { get; set; }
        public IList<FavorLevelReward>? Rewards { get; set; }
    }

    public class LocationExpTransaction
    {
        public ParcelType Type { get; set; }

        [JsonIgnore]
        public IEnumerable<ParcelInfo>? ParcelInfos { get; set; }

        public long TargetLocationUniqueId { get; set; }
        public long Amount { get; set; }
    }

    public abstract class ParcelBase
    {
        public abstract ParcelType Type { get; }

        [JsonIgnore]
        public abstract IEnumerable<ParcelInfo>? ParcelInfos { get; }
    }

    public class ParcelCost
    {
        private List<ConsumableItemBaseDB>? _consumableItemBaseDBs;

        [JsonIgnore]
        public IEnumerable<ConsumableItemBaseDB>? ConsumableItemBaseDBs { get; set; }

        public ConsumeCondition ConsumeCondition { get; set; }
        public CurrencyTransaction? Currency { get; set; }
        public List<EquipmentDB>? EquipmentDBs { get; set; }
        public List<FurnitureDB>? FurnitureDBs { get; set; }

        [JsonIgnore]
        public bool HasCurrency { get; set; }

        [JsonIgnore]
        public bool HasItem { get; set; }

        [JsonIgnore]
        public bool IsEmpty { get; set; }

        public List<ItemDB>? ItemDBs { get; set; }
        public List<ParcelInfo>? ParcelInfos { get; set; }
    }


    public class ParcelDetail
    {
        public ParcelInfo? OriginParcel { get; set; }
        public ParcelInfo? MailSendParcel { get; set; }
        public List<ParcelInfo>? ConvertedParcelInfos { get; set; }
        public ParcelChangeType ParcelChangeType { get; set; }
    }

    public class ParcelInfo : IEquatable<ParcelInfo>
    {
        public required ParcelKeyPair Key { get; set; }
        public long Amount { get; set; }
        public BasisPoint Multiplier { get; set; }
        public long MultipliedAmount { get { return Amount * Multiplier; } }
        public BasisPoint Probability { get; set; }

        public bool Equals(ParcelInfo? other)
        {
            if (other == null || Key == null) return false;
            return Key.Equals(other.Key);
        }
        
        public static List<ParcelInfo> CreateParcelInfo(ParcelType type, long id, long amount = 1)
        {
            return new List<ParcelInfo>()
            {
                new()
                {
                    Key = new ParcelKeyPair() { Type = type, Id = id },
                    Amount = amount,
                    Multiplier = BasisPoint.One,
                    Probability = BasisPoint.One
                }
            };
        }

        public static List<ParcelInfo> CreateParcelInfo(List<ParcelType> types, List<long> ids, List<long> amounts)
        {
            if (types.Count != ids.Count || ids.Count != amounts.Count)
                throw new ArgumentException("All input lists must have the same length.");

            List<ParcelInfo> parcelInfos = new();
            for (int i = 0; i < types.Count; i++)
            {
                parcelInfos.Add(new ParcelInfo()
                {
                    Key = new ParcelKeyPair() { Type = types[i], Id = ids[i] },
                    Amount = amounts[i],
                    Multiplier = BasisPoint.One,
                    Probability = BasisPoint.One
                });
            }
            return parcelInfos;
        }
    }

    public class ParcelKeyPair : IEquatable<ParcelKeyPair>, IComparable<ParcelKeyPair>
    {
        public static readonly ParcelKeyPair Empty = new();
        public ParcelType Type { get; set; }
        public long Id { get; set; }

        public int CompareTo(ParcelKeyPair? other) =>
            other == null ? 0 : Id.CompareTo(other.Id);

        public bool Equals(ParcelKeyPair? other) =>
            other != null && Id.Equals(other.Id);

        public ParcelKeyPair() { }

        public ParcelKeyPair(ParcelType parcelType, CurrencyTypes currencyType)
        {
            Type = parcelType;
            Id = (long)currencyType;
        }
    }


    public class ParcelResultDB
    {
        public List<AcademyLocationDB> AcademyLocationDBs { get; set; } = new();
        public List<ParcelInfo> AcquiredItems { get; set; } = new();
        public AccountCurrencyDB AccountCurrencyDB { get; set; } = new();
        public AccountDB AccountDB { get; set; } = new();
        public long AdditionalAccountExp { get; set; }
        public long BaseAccountExp { get; set; }
        public List<CharacterDB> CharacterDBs { get; set; } = new();
        public List<long> CharacterNewUniqueIds { get; set; } = new();
        public List<ParcelInfo> ConsumedItems { get; set; } = new();
        public List<CostumeDB> CostumeDBs { get; set; } = new();
        public List<ParcelInfo> DisplaySequence { get; set; } = new();
        public List<EmblemDB> EmblemDBs { get; set; } = new();
        public Dictionary<long, EquipmentDB> EquipmentDBs { get; set; } = new();
        public Dictionary<long, FurnitureDB> FurnitureDBs { get; set; } = new();

        // [JsonIgnore]
        public List<long>? GachaResultCharacters { get; set; }
        
        public Dictionary<long, IdCardBackgroundDB> IdCardBackgroundDBs { get; set; } = new();
        public Dictionary<long, ItemDB> ItemDBs { get; set; } = new();
        public List<MemoryLobbyDB> MemoryLobbyDBs { get; set; } = new();
        public long NewbieBoostAccountExp { get; set; }
        public List<ParcelInfo> ParcelForMission { get; set; } = new();
        public List<ParcelResultStepInfo> ParcelResultStepInfoList { get; set; } = new();
        public List<long> RemovedEquipmentIds { get; set; } = new();
        public List<long> RemovedFurnitureIds { get; set; } = new();
        public List<long> RemovedItemIds { get; set; } = new();
        public Dictionary<long, int> SecretStoneCharacterIdAndCounts { get; set; } = new();
        public List<StickerDB> StickerDBs { get; set; } = new();
        public List<CharacterDB> TSSCharacterDBs { get; set; } = new();
        public List<WeaponDB> WeaponDBs { get; set; } = new();
    }

    public enum ParcelProcessActionType
    {
        None,
        Cost,
        Reward
    }

    public class ParcelResultStepInfo
    {
        public ParcelProcessActionType ParcelProcessActionType { get; set; }
        public List<ParcelDetail>? StepParcelDetails { get; set; }
    }

    public class AccountExpTransaction
    {
        public ParcelType Type { get; set; }

        [JsonIgnore]
        public IEnumerable<ParcelInfo>? ParcelInfos { get; set; }

        public long Amount { get; set; }
    }

    public class CharacterExpTransaction
    {
        public ParcelType Type { get; set; }

        [JsonIgnore]
        public IEnumerable<ParcelInfo>? ParcelInfos { get; set; }

        public long TargetCharacterUniqueId { get; set; }
        public long Amount { get; set; }
    }

    public class CurrencySnapshot
    {
        public long AcademyTicket { get; set; }
        public long ActionPoint { get; set; }
        public long ArenaTicket { get; set; }
        public long ChaserTotalTicket { get; set; }
        private CurrencyValue? currencyValue { get; set; }

        [JsonIgnore]
        public Dictionary<CurrencyTypes, long>? CurrencyValues { get; set; }

        public long EliminateTicketA { get; set; }
        public long EliminateTicketB { get; set; }
        public long EliminateTicketC { get; set; }
        public long EliminateTicketD { get; set; }
        public long Gem { get; set; }
        public long GemBonus { get; set; }
        public long GemPaid { get; set; }
        public long Gold { get; set; }
        public AccountCurrencyDB? LastAccountCurrencyDB { get; set; }
        public long MasterCoin { get; set; }
        public long RaidTicket { get; set; }
        public long SchoolDungeonATicket { get; set; }
        public long SchoolDungeonBTicket { get; set; }
        public long SchoolDungeonCTicket { get; set; }
        public long SchoolDungeonTotalTicket { get; set; }

        [JsonIgnore]
        public DateTime ServerTimeSnapshot { get; set; }

        public long TimeAttackDungeonTicket { get; set; }
        public long WeekDungeonBloodTicket { get; set; }
        public long WeekDungeonChaserATicket { get; set; }
        public long WeekDungeonChaserBTicket { get; set; }
        public long WeekDungeonChaserCTicket { get; set; }
        public long WeekDungeonFindGiftTicket { get; set; }
        public long WorldRaidTicketA { get; set; }
        public long WorldRaidTicketB { get; set; }
        public long WorldRaidTicketC { get; set; }
    }

    public class CurrencyTransaction : ParcelBase, IEquatable<CurrencyTransaction>
    {
        public long AcademyTicket { get; set; }
        public long ActionPoint { get; set; }
        public long ArenaTicket { get; set; }
        public long ChaserTotalTicket { get; set; }
        private CurrencyValue? currencyValue { get; set; }

        [JsonIgnore]
        public IDictionary<CurrencyTypes, long>? CurrencyValues { get; set; }

        public long EliminateTicketA { get; set; }
        public long EliminateTicketB { get; set; }
        public long EliminateTicketC { get; set; }
        public long EliminateTicketD { get; set; }
        public long Gem { get; set; }
        public long GemBonus { get; set; }
        public long GemPaid { get; set; }
        public long Gold { get; set; }

        [JsonIgnore]
        public CurrencyTransaction? Inverse { get; set; }

        [JsonIgnore]
        public bool IsEmpty { get; set; }

        public long MasterCoin { get; set; }

        [JsonIgnore]
        public override IEnumerable<ParcelInfo>? ParcelInfos { get; }

        public long RaidTicket { get; set; }
        public long SchoolDungeonATicket { get; set; }
        public long SchoolDungeonBTicket { get; set; }
        public long SchoolDungeonCTicket { get; set; }
        public long SchoolDungeonTotalTicket { get; set; }
        public long TimeAttackDungeonTicket { get; set; }

        [JsonIgnore]
        public override ParcelType Type { get; }

        public long WeekDungeonBloodTicket { get; set; }
        public long WeekDungeonChaserATicket { get; set; }
        public long WeekDungeonChaserBTicket { get; set; }
        public long WeekDungeonChaserCTicket { get; set; }
        public long WeekDungeonFindGiftTicket { get; set; }
        public long WorldRaidTicketA { get; set; }
        public long WorldRaidTicketB { get; set; }
        public long WorldRaidTicketC { get; set; }

        public bool Equals(CurrencyTransaction? other)
        {
            return other != null && this.Type == other.Type;
        }
    }
}



