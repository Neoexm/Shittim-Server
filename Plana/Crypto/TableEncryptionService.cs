using Plana.Crypto;
using System.Buffers.Binary;
using System.Collections;
using System.Reflection;
using System.Text;

namespace Plana.Crypto
{
    public static class TableEncryptionService
    {
        public static bool UseEncryption;
        private static readonly Dictionary<Type, MethodInfo?> converterCache = [];
        private static readonly Dictionary<string, byte[]> keyCache = [];

        public static byte[] CreateKey(string name)
        {
            if (keyCache.TryGetValue(name, out var key))
                return key;

            byte[] password = GC.AllocateUninitializedArray<byte>(8);

            using var xxhash = XXHash32.Create();
            xxhash.ComputeHash(Encoding.UTF8.GetBytes(name));

            var mt = new MersenneTwister((int)xxhash.HashUInt32);

            int i = 0;
            while (i < password.Length)
            {
                Array.Copy(BitConverter.GetBytes(mt.Next()), 0, password, i, Math.Min(4, password.Length - i));
                i += 4;
            }

            keyCache.Add(name, password);

            return password;
        }

        /// <summary>
        /// Used for decrypting .bytes flatbuffers bin. Doesn't work yet
        /// </summary>
        /// <param name="name"></param>
        /// <param name="bytes"></param>
        public static void XOR(string name, byte[] bytes)
        {
            using var xxhash = XXHash32.Create();
            xxhash.ComputeHash(Encoding.UTF8.GetBytes(name));

            var mt = new MersenneTwister((int)xxhash.HashUInt32);
            var key = mt.NextBytes(bytes.Length);
            Crypto.XOR.Crypt(bytes, key);
        }

        private static int CalculateModulus(byte[] key)
        {
            if (key == null || key.Length == 0)
                return 1;

            int modulus = key[0] % 10;
            if (modulus <= 1)
                modulus = 7;
            if ((key[0] & 1) != 0)
                modulus = -modulus;

            return modulus;
        }

        public static MethodInfo? GetConvertMethod(Type type)
        {
            if (!converterCache.TryGetValue(type, out MethodInfo? convertMethod))
            {
                convertMethod = typeof(TableEncryptionService).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(x => x.Name == nameof(Convert) && (x.ReturnType == type));
                converterCache.Add(type, convertMethod);
            }

            return convertMethod;
        }

        public static List<T> Convert<T>(List<T> value, byte[] key) where T : class
        {
            var baseType = value.GetType().GenericTypeArguments[0];
            var convertMethod = GetConvertMethod(baseType.IsEnum ? Enum.GetUnderlyingType(value.GetType()) : baseType);
            if (convertMethod is null)
                return value;

            for (int i = 0; i < value.Count; i++)
            {
                value[i] = (T)convertMethod.Invoke(null, [value[i], key])!;
            }

            return value;
        }

        public static T Convert<T>(T value, byte[] key) where T : Enum
        {
            var convertMethod = GetConvertMethod(Enum.GetUnderlyingType(value.GetType()));
            if (convertMethod is null)
                return value;

            return (T)convertMethod.Invoke(null, [value, key])!;
        }

        public static bool Convert(bool value, byte[] key)
        {
            return value;
        }

        public static sbyte Convert(sbyte value, byte[] key)
        {
            var bytes = GC.AllocateUninitializedArray<byte>(sizeof(sbyte));
            bytes[0] = unchecked((byte)value);
            Crypto.XOR.Crypt(bytes, key);

            return unchecked((sbyte)bytes[0]);
        }

        public static int Convert(int value, byte[] key)
        {
            var bytes = GC.AllocateUninitializedArray<byte>(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
            Crypto.XOR.Crypt(bytes, key);

            return BinaryPrimitives.ReadInt32LittleEndian(bytes);
        }

        public static long Convert(long value, byte[] key)
        {
            var bytes = GC.AllocateUninitializedArray<byte>(sizeof(long));
            BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
            Crypto.XOR.Crypt(bytes, key);

            return BinaryPrimitives.ReadInt64LittleEndian(bytes);
        }

        public static uint Convert(uint value, byte[] key)
        {
            var bytes = GC.AllocateUninitializedArray<byte>(sizeof(uint));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            Crypto.XOR.Crypt(bytes, key);

            return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        }

        public static ulong Convert(ulong value, byte[] key)
        {
            var bytes = GC.AllocateUninitializedArray<byte>(sizeof(ulong));
            BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
            Crypto.XOR.Crypt(bytes, key);

            return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }

        public static float Convert(float value, byte[] key)
        {
            if (key == null || key.Length == 0)
                return value;

            int modulus = CalculateModulus(key);
            return value != 0.0f ? (value / modulus) / 10000.0f : 0.0f;
        }

        public static double Convert(double value, byte[] key)
        {
            if (key == null || key.Length == 0)
                return value;

            int modulus = CalculateModulus(key);
            return value != 0.0 ? (value / modulus) / 10000.0 : 0.0;
        }

        public static string Convert(string value, byte[] key)
        {
            var strBytes = System.Convert.FromBase64String(value);
            Crypto.XOR.Crypt(strBytes, key);

            return Encoding.Unicode.GetString(strBytes);
        }

        public static float Encrypt(float value, byte[] key)
        {
            if (key == null || key.Length == 0)
                return value;

            int modulus = CalculateModulus(key);
            return value != 0.0f ? (value * 10000.0f) * modulus : 0.0f;
        }

        public static double Encrypt(double value, byte[] key)
        {
            if (key == null || key.Length == 0)
                return value;

            int modulus = CalculateModulus(key);
            return value != 0.0 ? (value * 10000.0) * modulus : 0.0;
        }
    }
}
