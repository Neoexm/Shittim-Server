using System.Security.Cryptography;
using System.Text;

namespace BlueArchiveAPI.Core.Crypto
{
    public static class TableEncryptionService
    {
        public static bool UseEncryption { get; set; } = true;

        public static void XOR(string tableName, byte[] bytes)
        {
            if (!UseEncryption) return;

            var key = CreateKey(tableName);
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] ^= key[i % key.Length];
            }
        }

        private static byte[] CreateKey(string tableName)
        {
            using var xxhash = XXHash32.Create();
            xxhash.ComputeHash(Encoding.UTF8.GetBytes(tableName));
            
            var mt = new MersenneTwister((int)xxhash.HashUInt32);
            var key = new byte[16];
            
            for (int i = 0; i < key.Length; i += 4)
            {
                var bytes = BitConverter.GetBytes(mt.Next());
                Array.Copy(bytes, 0, key, i, Math.Min(4, key.Length - i));
            }
            
            return key;
        }
    }

    public class XXHash32 : HashAlgorithm
    {
        private const uint PRIME32_1 = 2654435761U;
        private const uint PRIME32_2 = 2246822519U;
        private const uint PRIME32_3 = 3266489917U;
        private const uint PRIME32_4 = 668265263U;
        private const uint PRIME32_5 = 374761393U;

        private static readonly Func<byte[], int, uint> FuncGetLittleEndianUInt32;
        private static readonly Func<uint, uint> FuncGetFinalHashUInt32;

        private uint _Seed32;
        private uint _ACC32_1;
        private uint _ACC32_2;
        private uint _ACC32_3;
        private uint _ACC32_4;
        private uint _Hash32;
        private int _RemainingLength;
        private long _TotalLength = 0;
        private int _CurrentIndex;
        private byte[] _CurrentArray;

        static XXHash32()
        {
            if (BitConverter.IsLittleEndian)
            {
                FuncGetLittleEndianUInt32 = new Func<byte[], int, uint>((x, i) =>
                {
                    unsafe
                    {
                        fixed (byte* array = x)
                        {
                            return *(uint*)(array + i);
                        }
                    }
                });
                FuncGetFinalHashUInt32 = new Func<uint, uint>(i => (i & 0x000000FFU) << 24 | (i & 0x0000FF00U) << 8 | (i & 0x00FF0000U) >> 8 | (i & 0xFF000000U) >> 24);
            }
            else
            {
                FuncGetLittleEndianUInt32 = new Func<byte[], int, uint>((x, i) =>
                {
                    unsafe
                    {
                        fixed (byte* array = x)
                        {
                            return (uint)(array[i++] | (array[i++] << 8) | (array[i++] << 16) | (array[i] << 24));
                        }
                    }
                });
                FuncGetFinalHashUInt32 = new Func<uint, uint>(i => i);
            }
        }

        public new static XXHash32 Create() => new XXHash32();

        public XXHash32() => Initialize(0);
        public XXHash32(uint seed) => Initialize(seed);

        public uint HashUInt32 => State == 0 ? _Hash32 : throw new InvalidOperationException("Hash computation has not yet completed.");

        public uint Seed
        {
            get => _Seed32;
            set
            {
                if (value != _Seed32)
                {
                    if (State != 0) throw new InvalidOperationException("Hash computation has not yet completed.");
                    _Seed32 = value;
                    Initialize();
                }
            }
        }

        public override void Initialize()
        {
            _ACC32_1 = _Seed32 + PRIME32_1 + PRIME32_2;
            _ACC32_2 = _Seed32 + PRIME32_2;
            _ACC32_3 = _Seed32 + 0;
            _ACC32_4 = _Seed32 - PRIME32_1;
        }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            if (State != 1) State = 1;
            var size = cbSize - ibStart;
            _RemainingLength = size & 15;
            if (cbSize >= 16)
            {
                var limit = size - _RemainingLength;
                do
                {
                    _ACC32_1 = Round32(_ACC32_1, FuncGetLittleEndianUInt32(array, ibStart));
                    ibStart += 4;
                    _ACC32_2 = Round32(_ACC32_2, FuncGetLittleEndianUInt32(array, ibStart));
                    ibStart += 4;
                    _ACC32_3 = Round32(_ACC32_3, FuncGetLittleEndianUInt32(array, ibStart));
                    ibStart += 4;
                    _ACC32_4 = Round32(_ACC32_4, FuncGetLittleEndianUInt32(array, ibStart));
                    ibStart += 4;
                } while (ibStart < limit);
            }
            _TotalLength += cbSize;

            if (_RemainingLength != 0)
            {
                _CurrentArray = array;
                _CurrentIndex = ibStart;
            }
        }

        protected override byte[] HashFinal()
        {
            if (_TotalLength >= 16)
            {
                _Hash32 = RotateLeft32(_ACC32_1, 1) + RotateLeft32(_ACC32_2, 7) + RotateLeft32(_ACC32_3, 12) + RotateLeft32(_ACC32_4, 18);
            }
            else
            {
                _Hash32 = _Seed32 + PRIME32_5;
            }

            _Hash32 += (uint)_TotalLength;

            while (_RemainingLength >= 4)
            {
                _Hash32 = RotateLeft32(_Hash32 + FuncGetLittleEndianUInt32(_CurrentArray, _CurrentIndex) * PRIME32_3, 17) * PRIME32_4;
                _CurrentIndex += 4;
                _RemainingLength -= 4;
            }
            unsafe
            {
                fixed (byte* arrayPtr = _CurrentArray)
                {
                    while (_RemainingLength-- >= 1)
                    {
                        _Hash32 = RotateLeft32(_Hash32 + arrayPtr[_CurrentIndex++] * PRIME32_5, 11) * PRIME32_1;
                    }
                }
            }
            _Hash32 = (_Hash32 ^ (_Hash32 >> 15)) * PRIME32_2;
            _Hash32 = (_Hash32 ^ (_Hash32 >> 13)) * PRIME32_3;
            _Hash32 ^= _Hash32 >> 16;

            _TotalLength = State = 0;

            return BitConverter.GetBytes(FuncGetFinalHashUInt32(_Hash32));
        }

        private static uint Round32(uint input, uint value) => RotateLeft32(input + (value * PRIME32_2), 13) * PRIME32_1;
        private static uint RotateLeft32(uint value, int count) => (value << count) | (value >> (32 - count));

        private void Initialize(uint seed)
        {
            HashSizeValue = 32;
            _Seed32 = seed;
            Initialize();
        }
    }

    public class MersenneTwister
    {
        private const int N = 624;
        private const int M = 397;
        private const uint MATRIX_A = 0x9908B0DF;
        private const uint UPPER_MASK = 0x80000000;
        private const uint LOWER_MASK = 0x7FFFFFFF;

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
    }
}
