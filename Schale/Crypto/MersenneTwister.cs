namespace Schale.Crypto
{
    public class MersenneTwister
    {
        private const int StateSize = 624;
        private const int ShiftSize = 397;
        private const uint MatrixConstant = 0x9908B0DF;
        private const uint UpperBitmask = 0x80000000;
        private const uint LowerBitmask = 0x7FFFFFFF;
        private const int MaxRandInteger = 0x7FFFFFFF;

        private readonly uint[] lookupTable = { 0x0, MatrixConstant };
        private readonly uint[] stateVector = new uint[StateSize];
        private int stateIndex = StateSize + 1;

        public MersenneTwister(int? seed = null)
        {
            seed ??= (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            InitializeGenerator((uint)seed);
        }

        public int Next(int minValue = 0, int? maxValue = null)
        {
            if (maxValue == null)
            {
                return GenerateInt31();
            }

            if (minValue > maxValue)
            {
                (minValue, maxValue) = (maxValue.Value, minValue);
            }

            return (int)Math.Floor((maxValue.Value - minValue + 1) * GenerateReal1() + minValue);
        }

        public double NextDouble(bool includeOne = false)
        {
            return includeOne ? GenerateReal1() : GenerateReal2();
        }

        public double NextDoublePositive()
        {
            return GenerateReal3();
        }

        public double Next53BitRes()
        {
            return GenerateReal53();
        }

        public byte[] NextBytes(int length)
        {
            byte[] result = new byte[length];
            for (int i = 0; i < length; i += 4)
            {
                uint randomValue = (uint)GenerateInt31();
                byte[] valueBytes = BitConverter.GetBytes(randomValue);
                Array.Copy(valueBytes, 0, result, i, Math.Min(4, length - i));
            }
            return result;
        }

        private void InitializeGenerator(uint seedValue)
        {
            stateVector[0] = seedValue;
            for (int i = 1; i < StateSize; i++)
            {
                stateVector[i] = (uint)(1812433253 * (stateVector[i - 1] ^ (stateVector[i - 1] >> 30)) + i);
            }
            stateIndex = StateSize;
        }

        private uint GenerateUInt32()
        {
            uint value;
            if (stateIndex >= StateSize)
            {
                if (stateIndex == StateSize + 1)
                {
                    InitializeGenerator(5489);
                }
                for (int k = 0; k < StateSize - ShiftSize; k++)
                {
                    value = (stateVector[k] & UpperBitmask) | (stateVector[k + 1] & LowerBitmask);
                    stateVector[k] = stateVector[k + ShiftSize] ^ (value >> 1) ^ lookupTable[value & 0x1];
                }
                for (int k = StateSize - ShiftSize; k < StateSize - 1; k++)
                {
                    value = (stateVector[k] & UpperBitmask) | (stateVector[k + 1] & LowerBitmask);
                    stateVector[k] = stateVector[k + (ShiftSize - StateSize)] ^ (value >> 1) ^ lookupTable[value & 0x1];
                }
                value = (stateVector[StateSize - 1] & UpperBitmask) | (stateVector[0] & LowerBitmask);
                stateVector[StateSize - 1] = stateVector[ShiftSize - 1] ^ (value >> 1) ^ lookupTable[value & 0x1];
                stateIndex = 0;
            }
            value = stateVector[stateIndex++];
            value ^= (value >> 11);
            value ^= (value << 7) & 0x9D2C5680;
            value ^= (value << 15) & 0xEFC60000;
            value ^= (value >> 18);
            return value;
        }

        private int GenerateInt31()
        {
            return (int)(GenerateUInt32() >> 1);
        }

        private double GenerateReal1()
        {
            return GenerateUInt32() * (1.0 / 4294967295.0);
        }

        private double GenerateReal2()
        {
            return GenerateUInt32() * (1.0 / 4294967296.0);
        }

        private double GenerateReal3()
        {
            return (GenerateUInt32() + 0.5) * (1.0 / 4294967296.0);
        }

        private double GenerateReal53()
        {
            uint upper = GenerateUInt32() >> 5;
            uint lower = GenerateUInt32() >> 6;
            return (upper * 67108864.0 + lower) * (1.0 / 9007199254740992.0);
        }
    }
}


