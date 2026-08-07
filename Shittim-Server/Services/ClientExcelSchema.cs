using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using Shittim_Server.Services;

namespace BlueArchiveAPI.Services
{
    // The FlatData models are pinned to the client build they were reverse-engineered from, and a game update that inserts a field mid-table shifts every later slot - the GoodsExcel failure mode, wrong values on the wire with no error anywhere.
    // The installed client already carries the current layout: its flatc-generated reader structs (MX.Data.Excel.FooExcel) declare one property per schema field in slot order, and property declarations live in global-metadata.dat.
    // Reading that order out and diffing it against the compiled model gives, per table, where each of our fields sits in the current data. ExcelTableService uses the map to re-read a drifted table instead of serving garbage until someone regenerates the models.
    internal static class ClientExcelSchema
    {
        private static readonly Lazy<Dictionary<string, List<string>>?> Orders = new(ReadClientFieldOrders);

        internal static bool Available => Orders.Value != null;

        // ourType is the FooExcelT object-API class; its property declaration order is the compiled model's slot order.
        // Returns null when the table needs no realignment: layouts identical, client only appended fields, or the metadata is not readable.
        internal static int[]? SlotMapFor(Type ourType, string excelName)
        {
            var client = Orders.Value?.GetValueOrDefault(excelName);
            if (client == null)
                return null;

            var ours = ourType.GetProperties().Select(p => p.Name).ToArray();
            var map = Align(ours, client);

            for (var i = 0; i < map.Length; i++)
                if (map[i] >= 0 && map[i] != i)
                    return map;

            return null;
        }

        // Anchor by name, then pair off equal-length unmatched runs between anchors positionally - that is what a rename is, and it is also what recovers our UnknownSlotNN placeholders (RaidStageExcel slot 14 is RaidBossGroupType in the current client).
        internal static int[] Align(string[] ours, List<string> client)
        {
            // LCS over the name-match relation; vector fields surface in the reader as FooLength, so Tags matches TagsLength.
            static bool Matches(string ourName, string clientName) =>
                clientName == ourName || (clientName.Length == ourName.Length + 6 && clientName.StartsWith(ourName) && clientName.EndsWith("Length"));

            var lcs = new int[ours.Length + 1, client.Count + 1];
            for (var i = ours.Length - 1; i >= 0; i--)
                for (var j = client.Count - 1; j >= 0; j--)
                    lcs[i, j] = Matches(ours[i], client[j]) ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

            var map = new int[ours.Length];
            Array.Fill(map, -1);

            int a = 0, b = 0, runA = 0, runB = 0;
            while (a < ours.Length && b < client.Count)
            {
                if (Matches(ours[a], client[b]))
                {
                    // close out any rename run sitting between this anchor and the previous one
                    if (a - runA > 0 && a - runA == b - runB)
                        for (var k = 0; k < a - runA; k++)
                            map[runA + k] = runB + k;

                    map[a] = b;
                    a++; b++;
                    runA = a; runB = b;
                }
                else if (lcs[a + 1, b] >= lcs[a, b + 1])
                    a++;
                else
                    b++;
            }
            if (ours.Length - runA > 0 && ours.Length - runA == client.Count - runB)
                for (var k = 0; k < ours.Length - runA; k++)
                    map[runA + k] = runB + k;

            return map;
        }

        private static Dictionary<string, List<string>>? ReadClientFieldOrders()
        {
            try
            {
                var path = ClientMetadataPatchService.GetMetadataPath();
                if (path == null || !File.Exists(path))
                    return null;

                var data = File.ReadAllBytes(path);
                if (data.Length < 168 || BinaryPrimitives.ReadUInt32LittleEndian(data) != 0xFAB11BAF)
                    return null;

                var version = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4));
                if (version is < 24 or > 31)
                {
                    Console.WriteLine($"[ClientExcelSchema] global-metadata.dat is IL2CPP metadata version {version}, which this reader does not know; schema realignment disabled.");
                    return null;
                }

                // header is (offset, size) int32 pairs after the 8-byte magic+version; this prefix order has been stable since v19
                (int Offset, int Size) Pair(int index) => (
                    BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8 + index * 8)),
                    BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(12 + index * 8)));

                var strings = Pair(2);
                var properties = Pair(4);
                var typeDefs = Pair(19);

                string Name(int index)
                {
                    if (index < 0 || index >= strings.Size)
                        throw new InvalidDataException("string index out of range");
                    var start = strings.Offset + index;
                    var end = Array.IndexOf(data, (byte)0, start);
                    return Encoding.UTF8.GetString(data, start, end - start);
                }

                // Il2CppTypeDefinition is 88 bytes from v24.5 on: 16 int32 indices, 8 uint16 counts, bitfield, token.
                const int TypeDefSize = 88;
                const int PropertyDefSize = 20;
                if (typeDefs.Size % TypeDefSize != 0)
                    return null;

                var orders = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                for (var offset = typeDefs.Offset; offset < typeDefs.Offset + typeDefs.Size; offset += TypeDefSize)
                {
                    string name;
                    try { name = Name(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset))); }
                    catch (InvalidDataException) { continue; }

                    if (!name.EndsWith("Excel", StringComparison.Ordinal))
                        continue;
                    if (Name(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 4))) != "MX.Data.Excel")
                        continue;

                    var propertyStart = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 44));
                    var propertyCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 66));

                    var fields = new List<string>(propertyCount);
                    for (var j = 0; j < propertyCount; j++)
                    {
                        var propName = Name(BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(properties.Offset + (propertyStart + j) * PropertyDefSize)));
                        if (propName != "ByteBuffer")
                            fields.Add(propName);
                    }
                    orders[name] = fields;
                }

                // wrong struct layout assumptions would not produce this shape, so treat it as the parse's self-check
                if (!orders.TryGetValue("CharacterExcel", out var character) || character.Count < 30 || character[0] != "Id")
                {
                    Console.WriteLine("[ClientExcelSchema] global-metadata.dat did not parse into the expected excel reader shapes; schema realignment disabled.");
                    return null;
                }

                Console.WriteLine($"[ClientExcelSchema] read {orders.Count} excel layouts from the installed client's global-metadata.dat");
                return orders;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientExcelSchema] could not read the client's global-metadata.dat ({ex.GetBaseException().Message}); schema realignment disabled.");
                return null;
            }
        }
    }

    // Reads one ExcelDB row into a FooExcelT instance with each compiled property taken from the slot the current client puts it at, instead of the slot the generated reader was compiled against.
    // Only for ExcelDB rows: those are plaintext FlatBuffers, so no per-field XOR conversion is involved.
    internal static class RealignedRowReader
    {
        internal static object Read(byte[] row, Type ourType, int[] slotMap)
        {
            var instance = Activator.CreateInstance(ourType)!;
            var table = Follow(row, 0);
            var vtable = table - BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(table));
            var vtableBytes = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(vtable));

            var props = ourType.GetProperties();
            for (var i = 0; i < props.Length; i++)
            {
                // generated UnPack news up every list even when the vector is absent, and downstream code leans on that
                if (props[i].PropertyType.IsGenericType && props[i].PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                    props[i].SetValue(instance, Activator.CreateInstance(props[i].PropertyType));

                var slot = slotMap[i];
                if (slot < 0)
                    continue;

                var slotPos = 4 + slot * 2;
                if (slotPos + 2 > vtableBytes)
                    continue;

                var fieldOffset = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(vtable + slotPos));
                if (fieldOffset == 0)
                    continue;

                props[i].SetValue(instance, ReadValue(row, table + fieldOffset, props[i].PropertyType));
            }

            return instance;
        }

        // The client auto-updates on Steam's schedule and the CDN data follows whenever the server next downloads it, so the client's field order is only ever a candidate for how these particular rows were written. Field width and the shape of whatever an offset points at are decidable from the bytes alone, so score both layouts over the same properties and let the rows pick; only the two returned numbers are comparable, never counts from different tables.
        // A row too malformed to walk is not evidence either way, and scores as a draw rather than costing the caller a row it could otherwise unpack.
        internal static (int Compiled, int Candidate) ScoreMaps(byte[] row, PropertyInfo[] props, int[] slotMap)
        {
            try { return Score(row, props, slotMap); }
            catch { return (0, 0); }
        }

        private static (int Compiled, int Candidate) Score(byte[] row, PropertyInfo[] props, int[] slotMap)
        {
            var table = Follow(row, 0);
            var vtable = table - BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(table));
            var vtableBytes = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(vtable));
            var tableBytes = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(vtable + 2));

            var offsets = new SortedSet<int>();
            for (var slotPos = 4; slotPos + 2 <= vtableBytes; slotPos += 2)
            {
                var fieldOffset = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(vtable + slotPos));
                if (fieldOffset != 0)
                    offsets.Add(fieldOffset);
            }

            int compiled = 0, candidate = 0;
            for (var i = 0; i < props.Length; i++)
            {
                if (slotMap[i] < 0)
                    continue;

                compiled += Misfits(row, table, vtable, vtableBytes, tableBytes, offsets, i, props[i].PropertyType);
                candidate += Misfits(row, table, vtable, vtableBytes, tableBytes, offsets, slotMap[i], props[i].PropertyType);
            }
            return (compiled, candidate);
        }

        private static int Misfits(byte[] row, int table, int vtable, int vtableBytes, int tableBytes, SortedSet<int> offsets, int slot, Type type)
        {
            var slotPos = 4 + slot * 2;
            if (slotPos + 2 > vtableBytes)
                return 0;

            var fieldOffset = BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(vtable + slotPos));
            if (fieldOffset == 0)
                return 0;

            // flatc emits fields widest-first, so the gap to the next occupied offset is an upper bound on this field's width once trailing padding is allowed for. Only "too narrow to hold this type" is sound evidence; too wide is padding.
            var next = offsets.GetViewBetween(fieldOffset + 1, ushort.MaxValue).Min;
            var room = (next == 0 ? tableBytes : next) - fieldOffset;

            var isOffset = type == typeof(string) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
            var width = isOffset ? 4 : ScalarSize(type.IsEnum ? Enum.GetUnderlyingType(type) : type);
            if (width > room)
                return 1;
            if (!isOffset)
                return 0;

            try
            {
                var target = Follow(row, table + fieldOffset);
                var length = BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(target));
                if (length < 0)
                    return 1;

                if (type == typeof(string))
                    return target + 4 + length < row.Length && row[target + 4 + length] == 0 ? 0 : 1;

                var elem = type.GetGenericArguments()[0];
                var elemSize = elem == typeof(string) ? 4 : ScalarSize(elem.IsEnum ? Enum.GetUnderlyingType(elem) : elem);
                return target + 4 + length * elemSize <= row.Length ? 0 : 1;
            }
            catch
            {
                return 1;
            }
        }

        private static object ReadValue(byte[] row, int pos, Type type)
        {
            if (type.IsEnum)
                return Enum.ToObject(type, ReadValue(row, pos, Enum.GetUnderlyingType(type)));

            if (type == typeof(string))
                return ReadString(row, Follow(row, pos));

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elemType = type.GetGenericArguments()[0];
                var vector = Follow(row, pos);
                var length = BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(vector));
                var list = (System.Collections.IList)Activator.CreateInstance(type)!;

                var elemSize = elemType == typeof(string) ? 4 : ScalarSize(elemType.IsEnum ? Enum.GetUnderlyingType(elemType) : elemType);
                for (var k = 0; k < length; k++)
                {
                    var elemPos = vector + 4 + k * elemSize;
                    list.Add(elemType == typeof(string) ? ReadString(row, Follow(row, elemPos)) : ReadValue(row, elemPos, elemType));
                }
                return list;
            }

            return ReadScalar(row, pos, type);
        }

        private static object ReadScalar(byte[] row, int pos, Type type) => type switch
        {
            _ when type == typeof(bool) => row[pos] != 0,
            _ when type == typeof(sbyte) => (sbyte)row[pos],
            _ when type == typeof(byte) => row[pos],
            _ when type == typeof(short) => BinaryPrimitives.ReadInt16LittleEndian(row.AsSpan(pos)),
            _ when type == typeof(ushort) => BinaryPrimitives.ReadUInt16LittleEndian(row.AsSpan(pos)),
            _ when type == typeof(int) => BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(pos)),
            _ when type == typeof(uint) => BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(pos)),
            _ when type == typeof(long) => BinaryPrimitives.ReadInt64LittleEndian(row.AsSpan(pos)),
            _ when type == typeof(ulong) => BinaryPrimitives.ReadUInt64LittleEndian(row.AsSpan(pos)),
            _ when type == typeof(float) => BinaryPrimitives.ReadSingleLittleEndian(row.AsSpan(pos)),
            _ when type == typeof(double) => BinaryPrimitives.ReadDoubleLittleEndian(row.AsSpan(pos)),
            _ => throw new NotSupportedException($"field type {type.Name} is not realignable")
        };

        private static int ScalarSize(Type type)
        {
            if (type == typeof(bool) || type == typeof(sbyte) || type == typeof(byte)) return 1;
            if (type == typeof(short) || type == typeof(ushort)) return 2;
            if (type == typeof(long) || type == typeof(ulong) || type == typeof(double)) return 8;
            return 4;
        }

        private static string ReadString(byte[] row, int pos)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(pos));
            return Encoding.UTF8.GetString(row, pos + 4, length);
        }

        private static int Follow(byte[] row, int pos) => pos + BinaryPrimitives.ReadInt32LittleEndian(row.AsSpan(pos));
    }
}
