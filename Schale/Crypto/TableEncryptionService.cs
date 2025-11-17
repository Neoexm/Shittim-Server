using Schale.Crypto;
using System.Buffers.Binary;
using System.Reflection;
using System.Text;

namespace Schale.Crypto
{
    public static class TableEncryptionService
    {
        public static bool UseEncryption;
        private static readonly Dictionary<Type, MethodInfo?> methodCache = [];
        private static readonly Dictionary<string, byte[]> encryptionKeyCache = [];

        public static byte[] CreateKey(string tableName)
        {
            if (encryptionKeyCache.TryGetValue(tableName, out var cachedKey))
                return cachedKey;

            byte[] keyData = GC.AllocateUninitializedArray<byte>(8);

            using var hasher = XXHash32.Create();
            hasher.ComputeHash(Encoding.UTF8.GetBytes(tableName));

            var randomGen = new MersenneTwister((int)hasher.HashUInt32);

            int position = 0;
            while (position < keyData.Length)
            {
                Array.Copy(BitConverter.GetBytes(randomGen.Next()), 0, keyData, position, Math.Min(4, keyData.Length - position));
                position += 4;
            }

            encryptionKeyCache.Add(tableName, keyData);

            return keyData;
        }

        public static void XOR(string tableName, byte[] data)
        {
            using var hasher = XXHash32.Create();
            hasher.ComputeHash(Encoding.UTF8.GetBytes(tableName));

            var randomGen = new MersenneTwister((int)hasher.HashUInt32);
            var xorKey = randomGen.NextBytes(data.Length);
            Crypto.XOR.Crypt(data, xorKey);
        }

        private static int ComputeModulus(byte[] encryptionKey)
        {
            if (encryptionKey == null || encryptionKey.Length == 0)
                return 1;

            int mod = encryptionKey[0] % 10;
            if (mod <= 1)
                mod = 7;
            if ((encryptionKey[0] & 1) != 0)
                mod = -mod;

            return mod;
        }

        public static MethodInfo? GetConvertMethod(Type targetType)
        {
            if (!methodCache.TryGetValue(targetType, out MethodInfo? method))
            {
                method = typeof(TableEncryptionService).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(m => m.Name == nameof(Convert) && (m.ReturnType == targetType));
                methodCache.Add(targetType, method);
            }

            return method;
        }

        public static List<T> Convert<T>(List<T> items, byte[] key) where T : class
        {
            var itemType = items.GetType().GenericTypeArguments[0];
            var convertMethod = GetConvertMethod(itemType.IsEnum ? Enum.GetUnderlyingType(items.GetType()) : itemType);
            if (convertMethod is null)
                return items;

            for (int idx = 0; idx < items.Count; idx++)
            {
                items[idx] = (T)convertMethod.Invoke(null, [items[idx], key])!;
            }

            return items;
        }

        public static T Convert<T>(T enumValue, byte[] key) where T : Enum
        {
            var convertMethod = GetConvertMethod(Enum.GetUnderlyingType(enumValue.GetType()));
            if (convertMethod is null)
                return enumValue;

            return (T)convertMethod.Invoke(null, [enumValue, key])!;
        }

        public static bool Convert(bool value, byte[] key)
        {
            return value;
        }

        public static sbyte Convert(sbyte value, byte[] key)
        {
            var buffer = GC.AllocateUninitializedArray<byte>(sizeof(sbyte));
            buffer[0] = unchecked((byte)value);
            Crypto.XOR.Crypt(buffer, key);

            return unchecked((sbyte)buffer[0]);
        }

        public static int Convert(int value, byte[] key)
        {
            var buffer = GC.AllocateUninitializedArray<byte>(sizeof(int));
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            Crypto.XOR.Crypt(buffer, key);

            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        public static long Convert(long value, byte[] key)
        {
            var buffer = GC.AllocateUninitializedArray<byte>(sizeof(long));
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            Crypto.XOR.Crypt(buffer, key);

            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        public static uint Convert(uint value, byte[] key)
        {
            var buffer = GC.AllocateUninitializedArray<byte>(sizeof(uint));
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            Crypto.XOR.Crypt(buffer, key);

            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        public static ulong Convert(ulong value, byte[] key)
        {
            var buffer = GC.AllocateUninitializedArray<byte>(sizeof(ulong));
            BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            Crypto.XOR.Crypt(buffer, key);

            return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        public static float Convert(float value, byte[] key)
        {
            if (key == null || key.Length == 0)
                return value;

            int modulus = ComputeModulus(key);
            return value != 0.0f ? (value / modulus) / 10000.0f : 0.0f;
        }

        public static double Convert(double value, byte[] key)
        {
            if (key == null || key.Length == 0)
                return value;

            int modulus = ComputeModulus(key);
            return value != 0.0 ? (value / modulus) / 10000.0 : 0.0;
        }

        public static string Convert(string value, byte[] key)
        {
            var encodedBytes = System.Convert.FromBase64String(value);
            Crypto.XOR.Crypt(encodedBytes, key);

            return Encoding.Unicode.GetString(encodedBytes);
        }

        public static float Encrypt(float value, byte[] key)
        {
            if (key == null || key.Length == 0)
                return value;

            int modulus = ComputeModulus(key);
            return value != 0.0f ? (value * 10000.0f) * modulus : 0.0f;
        }

        public static double Encrypt(double value, byte[] key)
        {
            if (key == null || key.Length == 0)
                return value;

            int modulus = ComputeModulus(key);
            return value != 0.0 ? (value * 10000.0) * modulus : 0.0;
        }
    }
}


