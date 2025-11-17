using System.Security.Cryptography;

namespace Schale.Crypto
{
    public static class HybridCryptor
    {
        public static byte[] EncryptTextAES(byte[] key, byte[] iv, byte[] plainText)
        {
            using var aesAlg = Aes.Create();

            aesAlg.BlockSize = 128;
            aesAlg.Mode = CipherMode.CBC;
            aesAlg.Padding = PaddingMode.PKCS7;
            aesAlg.Key = key;
            aesAlg.IV = iv;

            using var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
            using var msEncrypt = new MemoryStream();
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            {
                csEncrypt.Write(plainText, 0, plainText.Length);
                csEncrypt.FlushFinalBlock();
            }
            return msEncrypt.ToArray();
        }

        public static byte[] DecryptTextAES(byte[] key, byte[] iv, byte[] cipherText)
        {
            using var aesAlg = Aes.Create();

            aesAlg.BlockSize = 128;
            aesAlg.Mode = CipherMode.CBC;
            aesAlg.Padding = PaddingMode.PKCS7;
            aesAlg.Key = key;
            aesAlg.IV = iv;

            using var decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
            using var msDecrypt = new MemoryStream(cipherText);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var msPlain = new MemoryStream();

            csDecrypt.CopyTo(msPlain);
            return msPlain.ToArray();
        }
    }
}


