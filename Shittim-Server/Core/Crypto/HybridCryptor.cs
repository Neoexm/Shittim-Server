using System.Security.Cryptography;
using System.Text;

namespace BlueArchiveAPI.Core.Crypto
{
    public static class HybridCryptor
    {
        public static byte[] EncryptTextAES(byte[] plainBytes, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        }

        public static byte[] DecryptTextAES(byte[] encryptedBytes, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        }
    }
}
