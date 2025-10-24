using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Plana.Crypto
{
    public class NXCrypto
    {
        private const int AES_KEY_SIZE_16 = 16;
        public const int CIPHER_DECRYPT_MODE = 2;
        public const int CIPHER_ENCRYPT_MODE = 1;
        private const string COMMON_AES_KEY = "dd4763541be100910b568ca6d48268e3";
        private const string NXCOM_SIGNUP_AES_KEY = "36974d58eab59d8b";

        // Pre-gateway AES Key
        public static byte[] PreGatewayAesEncrypt(string plaintext)
        {
            byte[] key = NXCryptoHelper.HexStringToBytes(COMMON_AES_KEY);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = key;
            aesAlg.Mode = CipherMode.ECB;
            aesAlg.Padding = PaddingMode.PKCS7;

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using MemoryStream msEncrypt = new MemoryStream();
            using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            {
                csEncrypt.Write(plaintextBytes, 0, plaintextBytes.Length);
            }
            return msEncrypt.ToArray();
        }

        public static string PreGatewayAesDecrypt(byte[] ciphertext)
        {
            byte[] key = NXCryptoHelper.HexStringToBytes(COMMON_AES_KEY);

            using Aes aesAlg = Aes.Create();
            aesAlg.Key = key;
            aesAlg.Mode = CipherMode.ECB;
            aesAlg.Padding = PaddingMode.PKCS7;

            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            using MemoryStream msDecrypt = new MemoryStream(ciphertext);
            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using StreamReader srDecrypt = new StreamReader(csDecrypt, Encoding.UTF8);
            return srDecrypt.ReadToEnd();
        }

        // Post-gateway AES Key
        public byte[]? PostGatewayAesEncrypt(byte[] plain, NXToyCryptoType encryptType, long npsn)
        {
            INXToyEncrypt toyEncrypt;

            switch (encryptType)
            {
                case NXToyCryptoType.NONE:
                    toyEncrypt = new NoneEncryptor();
                    break;
                case NXToyCryptoType.NPSN:
                    var npsnKey = MakeNpsnAes128Key(npsn);
                    if (npsnKey == null) return null;
                    toyEncrypt = new NpsnEncryptor(npsnKey);
                    break;
                case NXToyCryptoType.COMMON:
                    var commonKey = NXCryptoHelper.HexStringToBytes(COMMON_AES_KEY);
                    toyEncrypt = new CommonEncryptor(commonKey);
                    break;
                default:
                    Console.WriteLine("encrypt instance is null, please check encryptType");
                    return null;
            }

            try
            {
                return toyEncrypt.Encrypt(plain);
            }
            catch (Exception e)
            {
                Console.WriteLine("Catch exception. message is : " + e.Message);
                return null;
            }
        }

        public byte[]? PostGatewayAesDecrypt(byte[] plain, NXToyCryptoType decryptType, long npsn)
        {
            INXToyDecrypt toyDecrypt;

            switch (decryptType)
            {
                case NXToyCryptoType.NONE:
                    toyDecrypt = new NoneDecryptor();
                    break;
                case NXToyCryptoType.NPSN:
                    var npsnKey = MakeNpsnAes128Key(npsn);
                    if (npsnKey == null) return null;
                    toyDecrypt = new NpsnDecryptor(npsnKey);
                    break;
                case NXToyCryptoType.COMMON:
                    var commonKey = NXCryptoHelper.HexStringToBytes(COMMON_AES_KEY);
                    toyDecrypt = new CommonDecryptor(commonKey);
                    break;
                case NXToyCryptoType.NXCOMSIGNUP:
                    toyDecrypt = new NxComSignupDecryptor(NXCOM_SIGNUP_AES_KEY);
                    break;
                default:
                    Console.WriteLine("decrypt instance is null, please check decryptType");
                    return null;
            }

            try
            {
                return toyDecrypt.Decrypt(plain);
            }
            catch (Exception e)
            {
                Console.WriteLine("Catch exception. message is : " + e.Message);
                return null;
            }
        }

        // Generate NPSN AES128 Key
        private byte[]? MakeNpsnAes128Key(long npsn)
        {
            try
            {
                var keyStr = string.Format(CultureInfo.InvariantCulture, "{0:D19}", npsn).Substring(3);
                var plainBytes = Encoding.UTF8.GetBytes(string.Format(CultureInfo.InvariantCulture, "{0:D19}", npsn).Substring(4));
                return EncryptWithAes128(keyStr, plainBytes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }

        // Encrypt or Decrypt with AES128
        private static byte[]? CipherWithAes128(byte[] key, byte[] destBytes, int mode)
        {
            if (key == null || destBytes == null) return null;
            if (mode != CIPHER_ENCRYPT_MODE && mode != CIPHER_DECRYPT_MODE) return null;

            var key16Bytes = new byte[AES_KEY_SIZE_16];
            Array.Copy(key, key16Bytes, Math.Min(key.Length, AES_KEY_SIZE_16));

            using var aes = Aes.Create();
            aes.KeySize = 128;
            aes.Key = key16Bytes;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;

            ICryptoTransform transform = (mode == CIPHER_ENCRYPT_MODE) ? aes.CreateEncryptor() : aes.CreateDecryptor();

            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, transform, CryptoStreamMode.Write))
            {
                cs.Write(destBytes, 0, destBytes.Length);
            }
            return ms.ToArray();
        }

        public static byte[]? EncryptWithAes128(string key, byte[] destBytes)
        {
            return CipherWithAes128(Encoding.UTF8.GetBytes(key), destBytes, CIPHER_ENCRYPT_MODE);
        }

        public static byte[]? EncryptWithAes128(byte[] key, byte[] destBytes)
        {
            return CipherWithAes128(key, destBytes, CIPHER_ENCRYPT_MODE);
        }

        public static byte[]? DecryptWithAes128(string key, byte[] destBytes)
        {
            return CipherWithAes128(Encoding.UTF8.GetBytes(key), destBytes, CIPHER_DECRYPT_MODE);
        }

        public static byte[]? DecryptWithAes128(byte[] key, byte[] destBytes)
        {
            return CipherWithAes128(key, destBytes, CIPHER_DECRYPT_MODE);
        }


        // Encryptor and Decryptor classes and interfaces
        public interface INXToyEncrypt
        {
            byte[]? Encrypt(byte[] plain);
        }

        public interface INXToyDecrypt
        {
            byte[]? Decrypt(byte[] plain);
        }

        public enum NXToyCryptoType
        {
            NONE,
            NPSN,
            COMMON,
            NXCOMSIGNUP
        }

        private class NoneEncryptor : INXToyEncrypt
        {
            public byte[] Encrypt(byte[] plain) => plain;
        }

        private class NoneDecryptor : INXToyDecrypt
        {
            public byte[] Decrypt(byte[] plain) => plain;
        }

        private class NpsnEncryptor(byte[] key) : INXToyEncrypt
        {
            public byte[]? Encrypt(byte[] plain) => EncryptWithAes128(key, plain);
        }

        private class NpsnDecryptor(byte[] key) : INXToyDecrypt
        {
            public byte[]? Decrypt(byte[] plain) => DecryptWithAes128(key, plain);
        }

        private class CommonEncryptor(byte[] key) : INXToyEncrypt
        {
            public byte[]? Encrypt(byte[] plain) => EncryptWithAes128(key, plain);
        }

        private class CommonDecryptor(byte[] key) : INXToyDecrypt
        {
            public byte[]? Decrypt(byte[] plain) => DecryptWithAes128(key, plain);
        }

        private class NxComSignupDecryptor(string key) : INXToyDecrypt
        {
            public byte[]? Decrypt(byte[] plain) => DecryptWithAes128(key, plain);
        }
    }

    public class NXCryptoHelper
    {
        public static byte[] HexStringToBytes(string hex)
        {
            if (hex.Length % 2 != 0) throw new ArgumentException("Hex string must have an even length.");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = byte.Parse(hex.Substring(i, 2), NumberStyles.HexNumber);
            }
            return bytes;
        }
    }
}