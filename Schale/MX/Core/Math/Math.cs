namespace Schale.MX.Core.Math
{
    [Serializable]
    public struct BasisPoint : IEquatable<BasisPoint>, IComparable<BasisPoint>
    {
        private static readonly long Multiplier = 10000L;
        private static readonly double OneOver10_4 = 1.0 / Multiplier;

        public static readonly BasisPoint Zero = new BasisPoint(0L);
        public static readonly BasisPoint One = new BasisPoint(Multiplier);
        public static readonly BasisPoint Epsilon = new BasisPoint(1L);
        public static readonly double DoubleEpsilon = 1.0 / Multiplier;

        private long RawValue;

        public long rawValue { get => RawValue; set => RawValue = value; }

        public BasisPoint(long RawValue)
        {
            this.RawValue = RawValue;
        }

        public static BasisPoint Min(BasisPoint lhs, BasisPoint rhs) =>
            lhs.RawValue < rhs.RawValue ? lhs : rhs;

        public static BasisPoint Max(BasisPoint lhs, BasisPoint rhs) =>
            lhs.RawValue > rhs.RawValue ? lhs : rhs;

        public static BasisPoint Clamp(BasisPoint value, BasisPoint min, BasisPoint max) =>
            new BasisPoint(System.Math.Clamp(value.RawValue, min.RawValue, max.RawValue));

        public static BasisPoint FromFloat(float value) =>
            new BasisPoint((long)(value * Multiplier));

        public static BasisPoint FromDouble(double value) =>
            new BasisPoint((long)(value * Multiplier));

        public static BasisPoint FromLong(long value) =>
            new BasisPoint(value * Multiplier);

        public double ToDouble() => RawValue * OneOver10_4;

        public float ToFloat() => (float)(RawValue * OneOver10_4);

        public long ToLong() => RawValue / Multiplier;

        public static BasisPoint Sum(IEnumerable<BasisPoint> list)
        {
            long sum = 0;
            foreach (var item in list)
                sum += item.RawValue;
            return new BasisPoint(sum);
        }

        public static long MultiplyLong(long value, BasisPoint basisPoint)
        {
            return (long)(value * basisPoint.ToDouble());
        }

        public static BasisPoint Multiply(BasisPoint lhs, BasisPoint rhs)
        {
            return new BasisPoint((long)((lhs.RawValue * (double)rhs.RawValue) / Multiplier));
        }

        public static BasisPoint Divide(BasisPoint lhs, BasisPoint rhs)
        {
            return new BasisPoint((long)((lhs.RawValue * (double)Multiplier) / rhs.RawValue));
        }

        public static BasisPoint Divide(long lhs, BasisPoint rhs)
        {
            return new BasisPoint((long)((lhs * Multiplier) / rhs.ToDouble()));
        }

        public static BasisPoint Divide(BasisPoint lhs, long rhs)
        {
            return new BasisPoint(lhs.RawValue / rhs);
        }

        public static BasisPoint operator *(BasisPoint lhs, BasisPoint rhs) => Multiply(lhs, rhs);
        public static long operator *(long lhs, BasisPoint rhs) => MultiplyLong(lhs, rhs);
        public static long operator *(BasisPoint lhs, long rhs) => MultiplyLong(rhs, lhs);
        public static BasisPoint operator /(BasisPoint lhs, BasisPoint rhs) => Divide(lhs, rhs);
        public static BasisPoint operator /(long lhs, BasisPoint rhs) => Divide(lhs, rhs);
        public static BasisPoint operator /(BasisPoint lhs, long rhs) => Divide(lhs, rhs);
        public static BasisPoint operator +(BasisPoint lhs, BasisPoint rhs) => new BasisPoint(lhs.RawValue + rhs.RawValue);
        public static BasisPoint operator +(long lhs, BasisPoint rhs) => new BasisPoint(lhs * Multiplier + rhs.RawValue);
        public static BasisPoint operator +(BasisPoint lhs, long rhs) => new BasisPoint(lhs.RawValue + rhs * Multiplier);
        public static BasisPoint operator -(BasisPoint lhs, BasisPoint rhs) => new BasisPoint(lhs.RawValue - rhs.RawValue);
        public static BasisPoint operator -(long lhs, BasisPoint rhs) => new BasisPoint(lhs * Multiplier - rhs.RawValue);
        public static BasisPoint operator -(BasisPoint lhs, long rhs) => new BasisPoint(lhs.RawValue - rhs * Multiplier);

        public static bool operator ==(BasisPoint x, BasisPoint y) => x.RawValue == y.RawValue;
        public static bool operator !=(BasisPoint x, BasisPoint y) => x.RawValue != y.RawValue;

        public int CompareTo(BasisPoint other) => RawValue.CompareTo(other.RawValue);
        public static bool operator <(BasisPoint left, BasisPoint right) => left.RawValue < right.RawValue;
        public static bool operator <=(BasisPoint left, BasisPoint right) => left.RawValue <= right.RawValue;
        public static bool operator >(BasisPoint left, BasisPoint right) => left.RawValue > right.RawValue;
        public static bool operator >=(BasisPoint left, BasisPoint right) => left.RawValue >= right.RawValue;

        public override int GetHashCode() => RawValue.GetHashCode();

        public override bool Equals(object? obj) => obj is BasisPoint other && RawValue == other.RawValue;

        public bool Equals(BasisPoint other) => RawValue == other.RawValue;

        public override string ToString() => ToDouble().ToString();
    }
}



