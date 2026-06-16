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

        // DIAGNOSTIC: gateway AES mode is selectable via sweep.txt ("<mode> <padding>", e.g. "ECB PKCS7")
        // while we identify the obfuscated client decryptor's mode. Used for BOTH the handshake
        // EncryptedKey/IV (so the client can recover serverKey) and in-session responses (same mode).
        public static byte[] EncryptSweep(byte[] plain, byte[] key, byte[] iv)
        {
            string mode = "ECB", pad = "PKCS7";
            bool zeroIv = false;
            try
            {
                const string f = @"C:\Users\tomda\Documents\Shittim-Server\sweep.txt";
                if (System.IO.File.Exists(f))
                {
                    using var fs = new System.IO.FileStream(f, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                    using var sr = new System.IO.StreamReader(fs);
                    foreach (var tok in sr.ReadToEnd().Trim().ToUpperInvariant().Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (tok is "CBC" or "ECB" or "CFB" or "CTR") mode = tok;
                        else if (tok is "PKCS7" or "NONE" or "ZEROS" or "ANSIX923") pad = tok;
                        else if (tok == "ZEROIV") zeroIv = true;
                    }
                }
            }
            catch { }

            if (zeroIv) iv = new byte[16];

            Console.WriteLine($"[SWEEP] mode={mode} pad={pad} keyHex={Convert.ToHexString(key)} ivHex={Convert.ToHexString(iv)} plainLen={plain.Length}");

            var padMode = pad switch
            {
                "NONE" => PaddingMode.None,
                "ZEROS" => PaddingMode.Zeros,
                "ANSIX923" => PaddingMode.ANSIX923,
                _ => PaddingMode.PKCS7,
            };

            if (mode == "CTR")
                return AesCtr(plain, key, iv);

            if (padMode == PaddingMode.None && plain.Length % 16 != 0)
            {
                var padded = new byte[((plain.Length / 16) + 1) * 16];
                Buffer.BlockCopy(plain, 0, padded, 0, plain.Length);
                plain = padded;
            }

            using var aes = Aes.Create();
            aes.Key = key;
            aes.Padding = padMode;
            switch (mode)
            {
                case "CBC": aes.Mode = CipherMode.CBC; aes.IV = iv; break;
                case "CFB": aes.Mode = CipherMode.CFB; aes.FeedbackSize = 128; aes.IV = iv; break;
                case "ECB": default: aes.Mode = CipherMode.ECB; break;
            }
            using var encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(plain, 0, plain.Length);
        }

        private static byte[] AesCtr(byte[] data, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            using var ecb = aes.CreateEncryptor();

            var output = new byte[data.Length];
            var counter = (byte[])iv.Clone();
            var ks = new byte[16];
            for (int off = 0; off < data.Length; off += 16)
            {
                ecb.TransformBlock(counter, 0, 16, ks, 0);
                int b = Math.Min(16, data.Length - off);
                for (int i = 0; i < b; i++) output[off + i] = (byte)(data[off + i] ^ ks[i]);
                for (int i = 15; i >= 0 && ++counter[i] == 0; i--) { }
            }
            return output;
        }
    }
}
