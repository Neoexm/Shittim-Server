using System.Text.Json.Serialization;

namespace Schale.MX.Logic.BattlesEntities
{
	[Serializable]
	public struct EntityId : IComparable, IComparable<EntityId>, IEquatable<EntityId>
	{
		private const uint typeMask = 4278190080;
		private const int instanceIdMask = 16777215;

		public static EntityId Invalid { get; }

		[JsonIgnore]
		public BattleEntityType EntityType { get; }

		[JsonIgnore]
		public int InstanceId { get; }

		[JsonIgnore]
		public int UniqueId
		{
			get { return uniqueId; }
		}

		public int uniqueId { get; set; }
		// This field has `private` keywrod in the source code
		// and deserialized from json.

		[JsonIgnore]
		public bool IsValid { get; }

		public int CompareTo(object? obj)
		{
			return obj is EntityId other ? CompareTo(other) : CompareTo(obj);
		}

		public int CompareTo(EntityId other)
		{
			return UniqueId.CompareTo(other.UniqueId);
		}

		public bool Equals(EntityId other)
		{
			return UniqueId == other.UniqueId && EntityType == other.EntityType && InstanceId == other.InstanceId;
		}

		public override bool Equals(object? obj)
		{
			if (obj == null) return false;
			if (obj is EntityId other) return Equals(other);
			return false;
		}

		public static bool operator ==(EntityId left, EntityId right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(EntityId left, EntityId right)
		{
			return !(left == right);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(UniqueId, EntityType, InstanceId);
		}
    }

	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum BattleEntityType
	{
		None = 0,
		Character = 16777216,
		SkillActor = 33554432,
		Obstacle = 67108864,
		Point = 134217728,
		Projectile = 268435456,
		EffectArea = 536870912,
		Supporter = 1073741824,
		BattleItem = -2147483648,
	}

	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum SkillSlot
	{
		None,
		NormalAttack01,
		NormalAttack02,
		NormalAttack03,
		NormalAttack04,
		NormalAttack05,
		NormalAttack06,
		NormalAttack07,
		NormalAttack08,
		NormalAttack09,
		NormalAttack10,
		ExSkill01,
		ExSkill02,
		ExSkill03,
		ExSkill04,
		ExSkill05,
		ExSkill06,
		ExSkill07,
		ExSkill08,
		ExSkill09,
		ExSkill10,
		Passive01,
		Passive02,
		Passive03,
		Passive04,
		Passive05,
		Passive06,
		Passive07,
		Passive08,
		Passive09,
		Passive10,
		ExtraPassive01,
		ExtraPassive02,
		ExtraPassive03,
		ExtraPassive04,
		ExtraPassive05,
		ExtraPassive06,
		ExtraPassive07,
		ExtraPassive08,
		ExtraPassive09,
		ExtraPassive10,
		Support01,
		Support02,
		Support03,
		Support04,
		Support05,
		Support06,
		Support07,
		Support08,
		Support09,
		Support10,
		EnterBattleGround,
		LeaderSkill01,
		LeaderSkill02,
		LeaderSkill03,
		LeaderSkill04,
		LeaderSkill05,
		LeaderSkill06,
		LeaderSkill07,
		LeaderSkill08,
		LeaderSkill09,
		LeaderSkill10,
		Equipment01,
		Equipment02,
		Equipment03,
		Equipment04,
		Equipment05,
		Equipment06,
		Equipment07,
		Equipment08,
		Equipment09,
		Equipment10,
		PublicSkill01,
		PublicSkill02,
		PublicSkill03,
		PublicSkill04,
		PublicSkill05,
		PublicSkill06,
		PublicSkill07,
		PublicSkill08,
		PublicSkill09,
		PublicSkill10,
		GroupBuff01,
		HexaBuff01,
		EventBuff01,
		EventBuff02,
		EventBuff03,
		MoveAttack01,
		MetamorphNormalAttack,
		GroundPassive01,
		GroundPassive02,
		GroundPassive03,
		GroundPassive04,
		GroundPassive05,
		GroundPassive06,
		GroundPassive07,
		GroundPassive08,
		GroundPassive09,
		GroundPassive10,
		HiddenPassive01,
		HiddenPassive02,
		HiddenPassive03,
		HiddenPassive04,
		HiddenPassive05,
		HiddenPassive06,
		HiddenPassive07,
		HiddenPassive08,
		HiddenPassive09,
		HiddenPassive10,
		Count
	}
}




