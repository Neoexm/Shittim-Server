using System.Security.Cryptography;
using System.Text;
using Serilog;
using Serilog.Events;

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

        // Gateway AES: ECB/PKCS7, matching the Nexon toy SDK crypto.
        // The obfuscated client decryptor (MX.Core.Crypto) is control-flow flattened and can't be read statically, so the mode was established empirically against the live client - CBC/PKCS7 is rejected by it.
        // Used for both the handshake EncryptedKey/IV and in-session responses.
        public static byte[] EncryptGatewayResponse(byte[] plain, byte[] key, byte[] iv)
        {
            // Fires on every encrypted response, so it is Debug and the hex conversion is skipped entirely when Debug is off (session key material never reaches the default log).
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("[Gateway] AES encrypt keyHex={KeyHex} ivHex={IvHex} plainLen={PlainLen}",
                    Convert.ToHexString(key), Convert.ToHexString(iv), plain.Length);
            }

            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(plain, 0, plain.Length);
        }
    }
}
