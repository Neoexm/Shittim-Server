using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Schale.Crypto
{
    public class NXCrypto
    {
        private const int AES_KEY_LENGTH_16 = 16;
        public const int CIPHER_MODE_DECRYPT = 2;
        public const int CIPHER_MODE_ENCRYPT = 1;
        private const string SHARED_AES_KEY = "dd4763541be100910b568ca6d48268e3";
        private const string NXCOM_SIGNUP_KEY = "36974d58eab59d8b";

        public static byte[] PreGatewayAesEncrypt(string plaintext)
        {
            byte[] keyBytes = NXCryptoHelper.HexStringToBytes(SHARED_AES_KEY);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

            using Aes cipher = Aes.Create();
            cipher.Key = keyBytes;
            cipher.Mode = CipherMode.ECB;
            cipher.Padding = PaddingMode.PKCS7;

            ICryptoTransform encryptor = cipher.CreateEncryptor(cipher.Key, cipher.IV);

            using MemoryStream outputStream = new MemoryStream();
            using (CryptoStream cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write))
            {
                cryptoStream.Write(plaintextBytes, 0, plaintextBytes.Length);
            }
            return outputStream.ToArray();
        }

        public static string PreGatewayAesDecrypt(byte[] ciphertext)
        {
            byte[] keyBytes = NXCryptoHelper.HexStringToBytes(SHARED_AES_KEY);

            using Aes cipher = Aes.Create();
            cipher.Key = keyBytes;
            cipher.Mode = CipherMode.ECB;
            cipher.Padding = PaddingMode.PKCS7;

            ICryptoTransform decryptor = cipher.CreateDecryptor(cipher.Key, cipher.IV);

            using MemoryStream inputStream = new MemoryStream(ciphertext);
            using CryptoStream cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
            using StreamReader textReader = new StreamReader(cryptoStream, Encoding.UTF8);
            return textReader.ReadToEnd();
        }

        public byte[]? PostGatewayAesEncrypt(byte[] plainData, NXToyCryptoType cryptoType, long accountNpsn)
        {
            INXToyEncrypt encryptor;

            switch (cryptoType)
            {
                case NXToyCryptoType.NONE:
                    encryptor = new NoOpEncryptor();
                    break;
                case NXToyCryptoType.NPSN:
                    var npsnKey = GenerateNpsnAes128Key(accountNpsn);
                    if (npsnKey == null) return null;
                    encryptor = new NpsnEncryptor(npsnKey);
                    break;
                case NXToyCryptoType.COMMON:
                    var sharedKey = NXCryptoHelper.HexStringToBytes(SHARED_AES_KEY);
                    encryptor = new CommonEncryptor(sharedKey);
                    break;
                default:
                    Console.WriteLine("Invalid encrypt type specified");
                    return null;
            }

            try
            {
                return encryptor.Encrypt(plainData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Encryption failed: " + ex.Message);
                return null;
            }
        }

        public byte[]? PostGatewayAesDecrypt(byte[] cipherData, NXToyCryptoType cryptoType, long accountNpsn)
        {
            INXToyDecrypt decryptor;

            switch (cryptoType)
            {
                case NXToyCryptoType.NONE:
                    decryptor = new NoOpDecryptor();
                    break;
                case NXToyCryptoType.NPSN:
                    var npsnKey = GenerateNpsnAes128Key(accountNpsn);
                    if (npsnKey == null) return null;
                    decryptor = new NpsnDecryptor(npsnKey);
                    break;
                case NXToyCryptoType.COMMON:
                    var sharedKey = NXCryptoHelper.HexStringToBytes(SHARED_AES_KEY);
                    decryptor = new CommonDecryptor(sharedKey);
                    break;
                case NXToyCryptoType.NXCOMSIGNUP:
                    decryptor = new NxComSignupDecryptor(NXCOM_SIGNUP_KEY);
                    break;
                default:
                    Console.WriteLine("Invalid decrypt type specified");
                    return null;
            }

            try
            {
                return decryptor.Decrypt(cipherData);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Decryption failed: " + ex.Message);
                return null;
            }
        }

        private byte[]? GenerateNpsnAes128Key(long npsn)
        {
            try
            {
                var npsnStr = string.Format(CultureInfo.InvariantCulture, "{0:D19}", npsn);
                var keyStr = npsnStr.Substring(3);
                var plainBytes = Encoding.UTF8.GetBytes(npsnStr.Substring(4));
                return EncryptWithAes128(keyStr, plainBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        private static byte[]? PerformAes128Cipher(byte[] key, byte[] targetBytes, int cipherMode)
        {
            if (key == null || targetBytes == null) return null;
            if (cipherMode != CIPHER_MODE_ENCRYPT && cipherMode != CIPHER_MODE_DECRYPT) return null;

            var key16 = new byte[AES_KEY_LENGTH_16];
            Array.Copy(key, key16, Math.Min(key.Length, AES_KEY_LENGTH_16));

            using var cipher = Aes.Create();
            cipher.KeySize = 128;
            cipher.Key = key16;
            cipher.Mode = CipherMode.ECB;
            cipher.Padding = PaddingMode.PKCS7;

            ICryptoTransform transform = (cipherMode == CIPHER_MODE_ENCRYPT) 
                ? cipher.CreateEncryptor() 
                : cipher.CreateDecryptor();

            using var outputStream = new MemoryStream();
            using (var cryptoStream = new CryptoStream(outputStream, transform, CryptoStreamMode.Write))
            {
                cryptoStream.Write(targetBytes, 0, targetBytes.Length);
            }
            return outputStream.ToArray();
        }

        public static byte[]? EncryptWithAes128(string key, byte[] data)
        {
            return PerformAes128Cipher(Encoding.UTF8.GetBytes(key), data, CIPHER_MODE_ENCRYPT);
        }

        public static byte[]? EncryptWithAes128(byte[] key, byte[] data)
        {
            return PerformAes128Cipher(key, data, CIPHER_MODE_ENCRYPT);
        }

        public static byte[]? DecryptWithAes128(string key, byte[] data)
        {
            return PerformAes128Cipher(Encoding.UTF8.GetBytes(key), data, CIPHER_MODE_DECRYPT);
        }

        public static byte[]? DecryptWithAes128(byte[] key, byte[] data)
        {
            return PerformAes128Cipher(key, data, CIPHER_MODE_DECRYPT);
        }

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

        private class NoOpEncryptor : INXToyEncrypt
        {
            public byte[] Encrypt(byte[] plain) => plain;
        }

        private class NoOpDecryptor : INXToyDecrypt
        {
            public byte[] Decrypt(byte[] plain) => plain;
        }

        private class NpsnEncryptor(byte[] encryptionKey) : INXToyEncrypt
        {
            public byte[]? Encrypt(byte[] plain) => EncryptWithAes128(encryptionKey, plain);
        }

        private class NpsnDecryptor(byte[] decryptionKey) : INXToyDecrypt
        {
            public byte[]? Decrypt(byte[] plain) => DecryptWithAes128(decryptionKey, plain);
        }

        private class CommonEncryptor(byte[] encryptionKey) : INXToyEncrypt
        {
            public byte[]? Encrypt(byte[] plain) => EncryptWithAes128(encryptionKey, plain);
        }

        private class CommonDecryptor(byte[] decryptionKey) : INXToyDecrypt
        {
            public byte[]? Decrypt(byte[] plain) => DecryptWithAes128(decryptionKey, plain);
        }

        private class NxComSignupDecryptor(string decryptionKey) : INXToyDecrypt
        {
            public byte[]? Decrypt(byte[] plain) => DecryptWithAes128(decryptionKey, plain);
        }
    }

    public class NXCryptoHelper
    {
        public static byte[] HexStringToBytes(string hexString)
        {
            if (hexString.Length % 2 != 0) 
                throw new ArgumentException("Hex string must have an even length.");
            
            byte[] result = new byte[hexString.Length / 2];
            for (int index = 0; index < hexString.Length; index += 2)
            {
                result[index / 2] = byte.Parse(hexString.Substring(index, 2), NumberStyles.HexNumber);
            }
            return result;
        }
    }
}


