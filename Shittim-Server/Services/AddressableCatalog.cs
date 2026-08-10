using System.Text;

namespace Shittim_Server.Services
{
    // catalog_Remote.bytes is Unity's ContentCatalogData wrapped in a "CCDB" v1 container - no encryption, no checksum inside the file. Keys, buckets, entries and the extra-data blob are the stock Addressables encodings, so an entry minted here loads through the same providers the shipped ones do.
    public class AddressableCatalog
    {
        public string LocatorId;
        public string InstanceProviderData;
        public string SceneProviderData;
        public List<string> ResourceProviderData = new();
        public List<string> ProviderIds = new();
        public List<string> InternalIds = new();
        public List<string> ResourceTypes = new();
        public List<string> InternalIdPrefixes = new();

        public List<CatalogKey> Keys = new();
        public List<CatalogBucket> Buckets = new();
        public List<CatalogEntry> Entries = new();

        // extra data is addressed by byte offset from the entry, not by index, so the offsets have to survive a rewrite untouched
        public List<CatalogKey> Extras = new();

        public static AddressableCatalog Read(byte[] data)
        {
            var r = new Cursor(data);
            if (data.Length < 5 || data[0] != 'C' || data[1] != 'C' || data[2] != 'D' || data[3] != 'B')
                throw new InvalidDataException("not a CCDB catalog");
            if (data[4] != 1)
                throw new InvalidDataException($"unsupported catalog version {data[4]}");
            r.Pos = 5;

            var c = new AddressableCatalog
            {
                LocatorId = r.String(),
                InstanceProviderData = r.String(),
                SceneProviderData = r.String()
            };
            c.ResourceProviderData = r.StringList();
            c.ProviderIds = r.StringList();
            c.InternalIds = r.StringList();
            var keyData = r.Blob();
            var bucketData = r.Blob();
            var entryData = r.Blob();
            var extraData = r.Blob();
            c.ResourceTypes = r.StringList();
            c.InternalIdPrefixes = r.StringList();
            if (r.Pos != data.Length)
                throw new InvalidDataException($"catalog has {data.Length - r.Pos} trailing bytes");

            var keyOffsets = new Dictionary<int, int>();
            var k = new Cursor(keyData) { Pos = 4 };
            while (k.Pos < keyData.Length)
            {
                keyOffsets[k.Pos] = c.Keys.Count;
                c.Keys.Add(CatalogKey.Read(k));
            }

            var b = new Cursor(bucketData);
            var bucketCount = b.Int32();
            for (var i = 0; i < bucketCount; i++)
            {
                var offset = b.Int32();
                var count = b.Int32();
                var bucket = new CatalogBucket { KeyIndex = keyOffsets[offset] };
                for (var j = 0; j < count; j++)
                    bucket.Entries.Add(b.Int32());
                c.Buckets.Add(bucket);
            }

            var e = new Cursor(entryData);
            var entryCount = e.Int32();
            for (var i = 0; i < entryCount; i++)
            {
                c.Entries.Add(new CatalogEntry
                {
                    InternalId = e.Int32(),
                    ProviderIndex = e.Int32(),
                    DependencyBucket = e.Int32(),
                    DependencyHash = e.Int32(),
                    ExtraIndex = e.Int32(),
                    PrimaryKey = e.Int32(),
                    ResourceType = e.Int32()
                });
            }

            var x = new Cursor(extraData);
            var extraOffsets = new Dictionary<int, int>();
            while (x.Pos < extraData.Length)
            {
                extraOffsets[x.Pos] = c.Extras.Count;
                c.Extras.Add(CatalogKey.Read(x));
            }
            foreach (var entry in c.Entries)
                entry.ExtraIndex = entry.ExtraIndex < 0 ? -1 : extraOffsets[entry.ExtraIndex];

            return c;
        }

        public byte[] Write()
        {
            var keyOffsets = new int[Keys.Count];
            var keyData = new MemoryStream();
            WriteInt(keyData, Keys.Count);
            for (var i = 0; i < Keys.Count; i++)
            {
                keyOffsets[i] = (int)keyData.Position;
                Keys[i].Write(keyData);
            }

            var extraOffsets = new int[Extras.Count];
            var extraData = new MemoryStream();
            for (var i = 0; i < Extras.Count; i++)
            {
                extraOffsets[i] = (int)extraData.Position;
                Extras[i].Write(extraData);
            }

            var bucketData = new MemoryStream();
            WriteInt(bucketData, Buckets.Count);
            foreach (var bucket in Buckets)
            {
                WriteInt(bucketData, keyOffsets[bucket.KeyIndex]);
                WriteInt(bucketData, bucket.Entries.Count);
                foreach (var index in bucket.Entries)
                    WriteInt(bucketData, index);
            }

            var entryData = new MemoryStream();
            WriteInt(entryData, Entries.Count);
            foreach (var entry in Entries)
            {
                WriteInt(entryData, entry.InternalId);
                WriteInt(entryData, entry.ProviderIndex);
                WriteInt(entryData, entry.DependencyBucket);
                WriteInt(entryData, entry.DependencyHash);
                WriteInt(entryData, entry.ExtraIndex < 0 ? -1 : extraOffsets[entry.ExtraIndex]);
                WriteInt(entryData, entry.PrimaryKey);
                WriteInt(entryData, entry.ResourceType);
            }

            var w = new MemoryStream();
            w.Write("CCDB"u8);
            w.WriteByte(1);
            WriteString(w, LocatorId);
            WriteString(w, InstanceProviderData);
            WriteString(w, SceneProviderData);
            WriteStringList(w, ResourceProviderData);
            WriteStringList(w, ProviderIds);
            WriteStringList(w, InternalIds);
            WriteBlob(w, keyData);
            WriteBlob(w, bucketData);
            WriteBlob(w, entryData);
            WriteBlob(w, extraData);
            WriteStringList(w, ResourceTypes);
            WriteStringList(w, InternalIdPrefixes);
            return w.ToArray();
        }

        public int IndexOfResourceType(string className)
        {
            var needle = "\"m_ClassName\":\"" + className + "\"";
            return ResourceTypes.FindIndex(t => t.Contains(needle));
        }

        public int IndexOfProvider(string providerId) => ProviderIds.IndexOf(providerId);

        // an addressable key is looked up case-insensitively by the client but stored lowercased, so both halves of the pair a shipped asset gets are minted the same way
        public int AddBucket(CatalogKey key, params int[] entries)
        {
            Keys.Add(key);
            var bucket = new CatalogBucket { KeyIndex = Keys.Count - 1 };
            bucket.Entries.AddRange(entries);
            Buckets.Add(bucket);
            return Buckets.Count - 1;
        }

        public int AddEntry(CatalogEntry entry)
        {
            Entries.Add(entry);
            return Entries.Count - 1;
        }

        public int AddInternalId(string id)
        {
            InternalIds.Add(id);
            return InternalIds.Count - 1;
        }

        public int AddExtra(CatalogKey extra)
        {
            Extras.Add(extra);
            return Extras.Count - 1;
        }

        private static void WriteInt(Stream s, int v)
        {
            Span<byte> b = stackalloc byte[4];
            BitConverter.TryWriteBytes(b, v);
            s.Write(b);
        }

        internal static void WriteString(Stream s, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var n = (uint)bytes.Length;
            while (true)
            {
                var chunk = (byte)(n & 0x7f);
                n >>= 7;
                if (n != 0)
                    s.WriteByte((byte)(chunk | 0x80));
                else
                {
                    s.WriteByte(chunk);
                    break;
                }
            }
            s.Write(bytes);
        }

        private static void WriteStringList(Stream s, List<string> values)
        {
            WriteInt(s, values.Count);
            foreach (var v in values)
                WriteString(s, v);
        }

        private static void WriteBlob(Stream s, MemoryStream blob)
        {
            WriteInt(s, (int)blob.Length);
            blob.Position = 0;
            blob.CopyTo(s);
        }

        internal class Cursor
        {
            private readonly byte[] _data;
            public int Pos;

            public Cursor(byte[] data) => _data = data;

            public byte Byte() => _data[Pos++];

            public int Int32()
            {
                var v = BitConverter.ToInt32(_data, Pos);
                Pos += 4;
                return v;
            }

            public ushort UInt16()
            {
                var v = BitConverter.ToUInt16(_data, Pos);
                Pos += 2;
                return v;
            }

            public uint UInt32()
            {
                var v = BitConverter.ToUInt32(_data, Pos);
                Pos += 4;
                return v;
            }

            public string Ascii(int length)
            {
                var v = Encoding.ASCII.GetString(_data, Pos, length);
                Pos += length;
                return v;
            }

            public string Unicode(int length)
            {
                var v = Encoding.Unicode.GetString(_data, Pos, length);
                Pos += length;
                return v;
            }

            public string String()
            {
                var n = 0;
                var shift = 0;
                while (true)
                {
                    var c = _data[Pos++];
                    n |= (c & 0x7f) << shift;
                    if ((c & 0x80) == 0)
                        break;
                    shift += 7;
                }
                var v = Encoding.UTF8.GetString(_data, Pos, n);
                Pos += n;
                return v;
            }

            public List<string> StringList()
            {
                var count = Int32();
                var list = new List<string>(count);
                for (var i = 0; i < count; i++)
                    list.Add(String());
                return list;
            }

            public byte[] Blob()
            {
                var n = Int32();
                var v = new byte[n];
                Array.Copy(_data, Pos, v, 0, n);
                Pos += n;
                return v;
            }
        }
    }

    public enum CatalogObjectType : byte
    {
        AsciiString = 0,
        UnicodeString = 1,
        UInt16 = 2,
        UInt32 = 3,
        Int32 = 4,
        Hash128 = 5,
        Type = 6,
        JsonObject = 7
    }

    public class CatalogKey
    {
        public CatalogObjectType Type;
        public string Text;
        public uint Number;
        public string AssemblyName;
        public string ClassName;
        public string Json;

        public static CatalogKey Ascii(string value) => new() { Type = CatalogObjectType.AsciiString, Text = value };
        public static CatalogKey Int(int value) => new() { Type = CatalogObjectType.Int32, Number = unchecked((uint)value) };

        public static CatalogKey Json7(string assembly, string className, string json) =>
            new() { Type = CatalogObjectType.JsonObject, AssemblyName = assembly, ClassName = className, Json = json };

        internal static CatalogKey Read(AddressableCatalog.Cursor r)
        {
            var type = (CatalogObjectType)r.Byte();
            switch (type)
            {
                case CatalogObjectType.AsciiString:
                    return new CatalogKey { Type = type, Text = r.Ascii(r.Int32()) };
                case CatalogObjectType.UnicodeString:
                    return new CatalogKey { Type = type, Text = r.Unicode(r.Int32()) };
                case CatalogObjectType.UInt16:
                    return new CatalogKey { Type = type, Number = r.UInt16() };
                case CatalogObjectType.UInt32:
                case CatalogObjectType.Int32:
                    return new CatalogKey { Type = type, Number = r.UInt32() };
                case CatalogObjectType.Hash128:
                case CatalogObjectType.Type:
                    return new CatalogKey { Type = type, Text = r.Ascii(r.Byte()) };
                case CatalogObjectType.JsonObject:
                    var assembly = r.Ascii(r.Byte());
                    var className = r.Ascii(r.Byte());
                    return new CatalogKey { Type = type, AssemblyName = assembly, ClassName = className, Json = r.Unicode(r.Int32()) };
                default:
                    throw new InvalidDataException($"unknown catalog object type {(byte)type}");
            }
        }

        public void Write(Stream s)
        {
            s.WriteByte((byte)Type);
            switch (Type)
            {
                case CatalogObjectType.AsciiString:
                {
                    var b = Encoding.ASCII.GetBytes(Text);
                    s.Write(BitConverter.GetBytes(b.Length));
                    s.Write(b);
                    break;
                }
                case CatalogObjectType.UnicodeString:
                {
                    var b = Encoding.Unicode.GetBytes(Text);
                    s.Write(BitConverter.GetBytes(b.Length));
                    s.Write(b);
                    break;
                }
                case CatalogObjectType.UInt16:
                    s.Write(BitConverter.GetBytes((ushort)Number));
                    break;
                case CatalogObjectType.UInt32:
                case CatalogObjectType.Int32:
                    s.Write(BitConverter.GetBytes(Number));
                    break;
                case CatalogObjectType.Hash128:
                case CatalogObjectType.Type:
                {
                    var b = Encoding.ASCII.GetBytes(Text);
                    s.WriteByte((byte)b.Length);
                    s.Write(b);
                    break;
                }
                case CatalogObjectType.JsonObject:
                {
                    var a = Encoding.ASCII.GetBytes(AssemblyName);
                    var c = Encoding.ASCII.GetBytes(ClassName);
                    var j = Encoding.Unicode.GetBytes(Json);
                    s.WriteByte((byte)a.Length);
                    s.Write(a);
                    s.WriteByte((byte)c.Length);
                    s.Write(c);
                    s.Write(BitConverter.GetBytes(j.Length));
                    s.Write(j);
                    break;
                }
            }
        }
    }

    public class CatalogBucket
    {
        public int KeyIndex;
        public List<int> Entries = new();
    }

    public class CatalogEntry
    {
        public int InternalId;
        public int ProviderIndex;
        public int DependencyBucket = -1;
        public int DependencyHash;
        public int ExtraIndex = -1;
        public int PrimaryKey;
        public int ResourceType;
    }
}
