using System.Security.Cryptography;

namespace Plana.Crypto
{
    public static class HybridCryptor
	{
		public static byte[] EncryptTextAES(byte[] key, byte[] iv, byte[] plainText)
		{
			using var aes = Aes.Create();

            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.BlockSize = 128;
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
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
			using var aes = Aes.Create();

            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.BlockSize = 128;
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var msDecrypt = new MemoryStream(cipherText);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using var msPlain = new MemoryStream();

            csDecrypt.CopyTo(msPlain);
            return msPlain.ToArray();
		}

		// public static ValueTuple<byte[], byte[]> GenerateKeyAndIV()
		// {
        //     return default(ValueTuple<byte[], byte[]>);
		// }

		// public static RSA LoadPublicKeyFromPem(string pem)
		// {
        //     return default(RSA);
		// }

		// public static ValueTuple<string, string> EncryptKeyAndIVWithRSA(byte[] aesKey, byte[] aesIV)
		// {
        //     return default(ValueTuple<string, string>);
		// }

		// public static bool VerifySignatureKeyAndIVWithRSA(byte[] encryptedKey, byte[] encryptedIV, byte[] signedKey, byte[] signedIV)
		// {
		// 	return default(bool);
		// }
	}
}
