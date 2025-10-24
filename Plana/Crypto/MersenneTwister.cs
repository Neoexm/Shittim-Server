namespace Plana.Crypto
{
    public class MersenneTwister
    {
        private const int N = 624;
        private const int M = 397;
        private const uint MATRIX_A = 0x9908B0DF;
        private const uint UPPER_MASK = 0x80000000;
        private const uint LOWER_MASK = 0x7FFFFFFF;
        private const int MAX_RAND_INT = 0x7FFFFFFF;

        private uint[] mag01 = { 0x0, MATRIX_A };
        private uint[] mt = new uint[N];
        private int mti = N + 1;

        public MersenneTwister(int? seed = null)
        {
            seed ??= (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            init_genrand((uint)seed);
        }

        public int Next(int minValue = 0, int? maxValue = null)
        {
            if (maxValue == null)
            {
                return genrand_int31();
            }

            if (minValue > maxValue)
            {
                (minValue, maxValue) = (maxValue.Value, minValue);
            }

            return (int)Math.Floor((maxValue.Value - minValue + 1) * genrand_real1() + minValue);
        }

        public double NextDouble(bool includeOne = false)
        {
            if (includeOne)
            {
                return genrand_real1();
            }
            return genrand_real2();
        }

        public double NextDoublePositive()
        {
            return genrand_real3();
        }

        public double Next53BitRes()
        {
            return genrand_res53();
        }

        public byte[] NextBytes(int length)
        {
            byte[] bytes = new byte[length];
            for (int i = 0; i < length; i += 4)
            {
                uint randInt = (uint)genrand_int31();
                byte[] intBytes = BitConverter.GetBytes(randInt);
                Array.Copy(intBytes, 0, bytes, i, Math.Min(4, length - i));
            }
            return bytes;
        }

        private void init_genrand(uint s)
        {
            mt[0] = s;
            for (int i = 1; i < N; i++)
            {
                mt[i] = (uint)(1812433253 * (mt[i - 1] ^ (mt[i - 1] >> 30)) + i);
            }
            mti = N;
        }

        private uint genrand_int32()
        {
            uint y;
            if (mti >= N)
            {
                if (mti == N + 1)
                {
                    init_genrand(5489);
                }
                for (int kk = 0; kk < N - M; kk++)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + M] ^ (y >> 1) ^ mag01[y & 0x1];
                }
                for (int kk = N - M; kk < N - 1; kk++)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + (M - N)] ^ (y >> 1) ^ mag01[y & 0x1];
                }
                y = (mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
                mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ mag01[y & 0x1];
                mti = 0;
            }
            y = mt[mti++];
            y ^= (y >> 11);
            y ^= (y << 7) & 0x9D2C5680;
            y ^= (y << 15) & 0xEFC60000;
            y ^= (y >> 18);
            return y;
        }

        private int genrand_int31()
        {
            return (int)(genrand_int32() >> 1);
        }

        private double genrand_real1()
        {
            return genrand_int32() * (1.0 / 4294967295.0);
        }

        private double genrand_real2()
        {
            return genrand_int32() * (1.0 / 4294967296.0);
        }

        private double genrand_real3()
        {
            return (genrand_int32() + 0.5) * (1.0 / 4294967296.0);
        }

        private double genrand_res53()
        {
            uint a = genrand_int32() >> 5;
            uint b = genrand_int32() >> 6;
            return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
        }
    }

}
