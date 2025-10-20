using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BlueArchiveAPI.Models;
using BlueArchiveAPI.NetworkModels;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace BlueArchiveAPI
{
    public static class Utils
    {
        private static readonly Dictionary<string, Protocol> _protocolEndpoints = Enum.GetValues<Protocol>()
            .ToDictionary(x => GetProtocolPath(x), x => x);

        private static readonly Dictionary<string, Protocol> _protocolHash = Enum.GetValues<Protocol>()
            .ToDictionary(x => GetProtocolHash(x), x => x);

        private const string B32_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        public static string Base32Encode(byte[] bs)
        {
            var sb = new StringBuilder();
            var i = 0;
            var b = 0;
            var bLen = 0;
            while (i < bs.Length)
            {
                b = (b << 8) | bs[i++];
                bLen += 8;
                while (bLen >= 5)
                {
                    sb.Append(B32_ALPHABET[(b >> (bLen - 5)) & 31]);
                    bLen -= 5;
                }
            }
            if (bLen > 0)
            {
                sb.Append(B32_ALPHABET[(b << (5 - bLen)) & 31]);
            }

            return sb.ToString();
        }

        public static string GetProtocolHash(Protocol protocol)
        {
            if (protocol == Protocol.Campaign_ConfirmTutorialStage)
                return "RL4MMUF2L7GQFJ7HNX2TP5CZJY";
            var b = Encoding.Unicode.GetBytes(protocol.ToString());
            using var md5 = MD5.Create();
            return Base32Encode(md5.ComputeHash(b));
        }

        public static string GetProtocolPath(Protocol protocol)
        {
            return protocol.ToString().Replace("_", "/").ToLower();
        }

        public static Protocol ParseProtocolPath(string path)
        {
            return _protocolEndpoints[path];
        }

        public static Protocol ParseProtocolHash(string hash)
        {
            return _protocolHash[hash];
        }

        public static T GetDataFromFile<T>(string filename, bool ignoreMissing)
        {
            filename += ".json";

            if (ignoreMissing && !File.Exists(filename))
            {
                return default;
            }

            return JsonConvert.DeserializeObject<T>(File.ReadAllText(filename));
        }
        public static void SaveDataToFile<T>(string filename, T t)
        {
            filename += ".json";
            File.WriteAllText(filename, JsonConvert.SerializeObject(t));
        }

        public static byte[] ReadAllBytes(this Stream stream)
        {
            using var ms = new MemoryStream();
            int b;

            do
            {
                var t = new byte[1024];
                b = stream.Read(t, 0, 1024);
                if (b > 0)
                    ms.Write(t, 0, b);
            } while (b > 0);

            return ms.ToArray();
        }

        public static async Task<byte[]> ReadAllBytesAsync(this Stream stream)
        {
            using var ms = new MemoryStream();
            int b;

            do
            {
                var t = new byte[1024];
                b = await stream.ReadAsync(t, 0, 1024);
                if (b > 0)
                    ms.Write(t, 0, b);
            } while (b > 0);

            return ms.ToArray();
        }

        public static byte[] GZipDecompress(byte[] compressed, int startIndex = 0)
        {
            using var gzipStream = new GZipStream(new MemoryStream(compressed, startIndex, compressed.Length - startIndex), CompressionMode.Decompress);
            return gzipStream.ReadAllBytes();
        }

        public static byte[] GZipCompress(byte[] raw, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            using var ms = new MemoryStream();
            using var gzipStream = new GZipStream(ms, compressionLevel, true);
            gzipStream.Write(raw, 0, raw.Length);
            gzipStream.Close();
            return ms.ToArray();
        }

        public static string DecryptRequestPacket(string packet)
        {
            var decoded = Convert.FromBase64String(packet);
            var packetSize = BitConverter.ToInt32(decoded);
            var decompressed = GZipDecompress(decoded, 4);
            if (packetSize != decompressed.Length)
                throw new Exception($"packet decompressed size mismatch: {packetSize} != {decompressed.Length}");
            return Encoding.UTF8.GetString(decompressed);
        }
        /*
        public static string EncryptRequestPacket(JToken data)
        {
            var json = data.ToString(Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);
            var compressed = GZipCompress(bytes);
            var packetSize = BitConverter.GetBytes(bytes.Length);
            var packet = new byte[packetSize.Length + compressed.Length];
            Array.Copy(packetSize, packet, packetSize.Length);
            Array.Copy(compressed, 0, packet, packetSize.Length, compressed.Length);
            return Convert.ToBase64String(packet);
        }
        
        public static JToken DecryptResponsePacket(byte[] packet, out Protocol proto)
        {
            var decompressed = GZipDecompress(packet);
            var result = JsonConvert.DeserializeObject<ServerPacket>(Encoding.UTF8.GetString(decompressed));
            // if (proto != result.protocol && result.protocol != Protocol.Error)
            //    throw new Exception($"Protocol mismatch: {result.protocol} != {proto}");
            proto = result.protocol;
            return JToken.Parse(result.packet);
        }
        */
        private static readonly JsonSerializerSettings settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };
        
        private static readonly JsonSerializerSettings packetSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };
        
        public static byte[] EncryptResponsePacket<TResponse>(TResponse packet, Protocol proto) where TResponse : ResponsePacket
        {
            var spacket = new ServerPacket(proto, JsonConvert.SerializeObject(packet, Formatting.None, packetSettings));
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(spacket));
        }

        // Pre-Gateway AES Encryption (for SDK endpoints like getCountry.nx, getPromotion.nx)
        private const string COMMON_AES_KEY = "dd4763541be100910b568ca6d48268e3";

        public static byte[] PreGatewayAesEncrypt(string plaintext)
        {
            byte[] key = HexStringToBytes(COMMON_AES_KEY);
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

        private static byte[] HexStringToBytes(string hex)
        {
            if (hex.Length % 2 != 0) throw new ArgumentException("Hex string must have an even length.");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = byte.Parse(hex.Substring(i, 2), System.Globalization.NumberStyles.HexNumber);
            }
            return bytes;
        }
    }
}
