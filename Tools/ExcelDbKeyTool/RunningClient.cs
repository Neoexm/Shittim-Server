using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace ExcelDbKeyTool;

// The client never holds the ExcelDB key in one piece. The holder it keeps it in splits the 32 bytes across three byte[] fields at 10, 10 and 12 and only ever joins them inside a property getter, so there is no key-shaped run of bytes on the heap to search for, and the base64 it arrived as and the hex it was handed to SQLCipher as are both garbage a few seconds later - which is why looking for either of those only ever worked by winning a race against the lobby's allocations. The three arrays themselves stay live for as long as the client does, and the object pointing at them is just three pointers side by side, so this reads every 10 and 12 byte managed array out of the process and then looks for a triple of pointers at 10/10/12 sharing one class. What that joins up to is judged by recomputing page 1's HMAC rather than by opening the file, and the winner is confirmed with a real open before it is returned.
internal static class RunningClient
{
    private const string ExcelDbRelative = @"BlueArchive_Data\StreamingAssets\PUB\Resource\Preload\TableBundles\ExcelDB.db";

    // An array header and its elements come to 0x2c bytes and a holder is three pointers, so chunks only have to share enough for the longer of the two to survive the boundary.
    private const int Overlap = 256;

    // il2cpp lays a managed array out as class pointer, monitor, bounds, length, then the elements.
    private const int ArrayBounds = 0x10;
    private const int ArrayLength = 0x18;
    private const int ArrayData = 0x20;

    private static int sqliteReady;

    public static (DecodeResult Key, string Source) ReadKey(Action<string> progress)
    {
        if (Interlocked.Exchange(ref sqliteReady, 1) == 0)
        {
            raw.SetProvider(new SQLite3Provider_e_sqlcipher());
            raw.FreezeProvider();
        }

        var process = Process.GetProcessesByName("BlueArchive").FirstOrDefault();
        if (process == null)
            throw new InvalidOperationException("BlueArchive.exe is not running. Start the game, let it get past the title screen so it has asked for the crypto keys, then try again.");

        // Before MainModule, which needs the same rights and would otherwise report an elevated client as a bare "Access is denied".
        var processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, process.Id);
        if (processHandle == IntPtr.Zero)
            throw new InvalidOperationException($"Could not open BlueArchive.exe (PID {process.Id}) for reading. Run this tool as administrator.");

        string excelDbPath;
        string? keyHex = null;
        try
        {
            excelDbPath = Path.Combine(Path.GetDirectoryName(process.MainModule!.FileName)!, ExcelDbRelative);
            if (!File.Exists(excelDbPath))
                throw new InvalidOperationException($"Found BlueArchive.exe (PID {process.Id}) but there is no ExcelDB.db at {excelDbPath}, and without it a candidate key cannot be checked.");

            var page1 = new byte[PageValidator.PageSize];
            using (var file = File.OpenRead(excelDbPath))
                file.ReadExactly(page1);

            var arrays = new Dictionary<long, (long Klass, byte[] Data)>();
            Sweep(processHandle, "Reading the client's heap", progress, (address, buffer, read) =>
            {
                CollectArrays(address, buffer, read, arrays);
                return false;
            });

            progress($"{arrays.Count:N0} arrays the right size to be a piece of the key. Looking for what holds them...");

            var validator = new PageValidator(page1);
            Sweep(processHandle, "Looking for the key's holder", progress, (_, buffer, read) => (keyHex = FindHolder(buffer, read, arrays, validator)) != null);
        }
        finally
        {
            CloseHandle(processHandle);
        }

        if (keyHex == null)
            throw new InvalidOperationException("Nothing in the client's memory was holding the three pieces of an ExcelDB key. It only builds them once it has been through Queuing_GetCryptoKeys, so take the game past the title screen first.");

        if (!Opens(excelDbPath, keyHex))
            throw new InvalidOperationException($"The key read out of the client did not open {excelDbPath}.");

        return (new DecodeResult(keyHex, Convert.ToBase64String(Convert.FromHexString(keyHex))), $"BlueArchive.exe (PID {process.Id})");
    }

    private static void Sweep(IntPtr processHandle, string what, Action<string> progress, Func<long, byte[], int, bool> onChunk)
    {
        var buffer = new byte[4 * 1024 * 1024];
        long scanned = 0;
        long reported = 0;

        var address = IntPtr.Zero;
        while (VirtualQueryEx(processHandle, address, out var memInfo, (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) != 0)
        {
            long regionSize = (long)memInfo.RegionSize;
            if (regionSize <= 0)
                break;

            long next = memInfo.BaseAddress.ToInt64() + regionSize;
            uint protect = memInfo.Protect & 0xFF;

            // The managed heap sits on writable commit. Skipping everything else leaves out the mapped images and the read-only asset views, which is most of a Unity process.
            if (memInfo.State == MEM_COMMIT && (memInfo.Protect & PAGE_GUARD) == 0 &&
                (protect == PAGE_READWRITE || protect == PAGE_WRITECOPY || protect == PAGE_EXECUTE_READWRITE))
            {
                for (long offset = 0; offset < regionSize; offset += buffer.Length - Overlap)
                {
                    int want = (int)Math.Min(buffer.Length, regionSize - offset);
                    long chunkBase = memInfo.BaseAddress.ToInt64() + offset;
                    if (!ReadProcessMemory(processHandle, new IntPtr(chunkBase), buffer, want, out int read) || read <= 0)
                        continue;

                    scanned += read;
                    if (onChunk(chunkBase, buffer, read))
                        return;
                }

                if (scanned - reported >= 256L * 1024 * 1024)
                {
                    reported = scanned;
                    progress($"{what}: {scanned / (1024 * 1024):N0} MB...");
                }
            }

            address = new IntPtr(next);
            if (next <= 0)
                break;
        }
    }

    // The class pointer is not known ahead of time, so the only filters going in are a null bounds pointer, a length of exactly 10 or 12, and a first word that could be a pointer at all. That keeps every same-sized array in the process, which is the point - the triple is what narrows it.
    public static void CollectArrays(long baseAddress, byte[] buffer, int length, Dictionary<long, (long Klass, byte[] Data)> arrays)
    {
        for (var i = 0; i + ArrayData + 12 <= length; i += 8)
        {
            if (BitConverter.ToInt64(buffer, i + ArrayBounds) != 0)
                continue;

            var count = BitConverter.ToInt64(buffer, i + ArrayLength);
            if (count != 10 && count != 12)
                continue;

            var klass = BitConverter.ToInt64(buffer, i);
            if (klass < 0x10000 || (klass & 7) != 0)
                continue;

            var data = new byte[count];
            Buffer.BlockCopy(buffer, i + ArrayData, data, 0, (int)count);
            arrays[baseAddress + i] = (klass, data);
        }
    }

    // Three reference fields in declaration order come out as three pointers side by side. Two same-sized arrays landing next to each other is ordinary, but three at 10, 10 and 12 in that order sharing a class is not, and page 1 settles whatever is left.
    public static string? FindHolder(byte[] buffer, int length, Dictionary<long, (long Klass, byte[] Data)> arrays, PageValidator validator)
    {
        var key = new byte[32];

        for (var i = 0; i + 24 <= length; i += 8)
        {
            if (!arrays.TryGetValue(BitConverter.ToInt64(buffer, i), out var part1) || part1.Data.Length != 10)
                continue;

            if (!arrays.TryGetValue(BitConverter.ToInt64(buffer, i + 8), out var part2) || part2.Data.Length != 10 || part2.Klass != part1.Klass)
                continue;

            if (!arrays.TryGetValue(BitConverter.ToInt64(buffer, i + 16), out var part3) || part3.Data.Length != 12 || part3.Klass != part1.Klass)
                continue;

            part1.Data.CopyTo(key, 0);
            part2.Data.CopyTo(key, 10);
            part3.Data.CopyTo(key, 20);

            if (validator.Validate(key))
                return Convert.ToHexString(key).ToLowerInvariant();
        }

        return null;
    }

    private static bool Opens(string dbPath, string keyHex)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());

        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA key = \"x'{keyHex}'\";";
        command.ExecuteNonQuery();

        // A wrong key fails on the first page decrypt with SQLITE_NOTADB rather than on the pragma.
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
        try
        {
            command.ExecuteScalar();
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr hObject);

    const int PROCESS_QUERY_INFORMATION = 0x0400;
    const int PROCESS_VM_READ = 0x0010;
    const uint PAGE_READWRITE = 0x04;
    const uint PAGE_WRITECOPY = 0x08;
    const uint PAGE_EXECUTE_READWRITE = 0x40;
    const uint PAGE_GUARD = 0x100;
    const uint MEM_COMMIT = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }
}

// Reproduces SQLCipher 4's page-1 HMAC so a candidate can be judged without opening the file. The main key is used raw (a 64 hex PRAGMA key skips the KDF), only the HMAC subkey is derived, and a match on page 1's stored MAC is the same certainty a successful open gives.
internal sealed class PageValidator
{
    public const int PageSize = 4096;
    private const int Reserve = 80;      // iv(16) + hmac-sha512(64)
    private const int IvSz = 16;
    private const int SaltSz = 16;
    private const int KeySz = 32;
    private const int FastKdfIter = 2;
    private const byte HmacSaltMask = 0x3a;

    private readonly byte[] page;
    private readonly byte[] hmacSalt = new byte[SaltSz];

    public PageValidator(byte[] page1)
    {
        page = page1;
        for (var i = 0; i < SaltSz; i++)
            hmacSalt[i] = (byte)(page[i] ^ HmacSaltMask);
    }

    public bool Validate(ReadOnlySpan<byte> key)
    {
        Span<byte> hmacKey = stackalloc byte[KeySz];
        Rfc2898DeriveBytes.Pbkdf2(key, hmacSalt, hmacKey, FastKdfIter, HashAlgorithmName.SHA512);

        int dataLen = PageSize - Reserve - SaltSz + IvSz;   // ciphertext after the salt, plus the iv
        Span<byte> input = stackalloc byte[dataLen + 4];
        page.AsSpan(SaltSz, dataLen).CopyTo(input);
        input[dataLen] = 1;                                 // page number, little-endian

        Span<byte> computed = stackalloc byte[64];
        using (var mac = new HMACSHA512(hmacKey.ToArray()))
            mac.TryComputeHash(input, computed, out _);

        return computed.SequenceEqual(page.AsSpan(PageSize - Reserve + IvSz, 64));
    }
}

internal static class MemoryScanSelfTest
{
    private const long HeapBase = 0x1000000;
    private const long Klass = 0x7ff800112240;

    public static int Run()
    {
        var key = Enumerable.Range(0, 32).Select(value => (byte)(value * 7 + 3)).ToArray();
        var expected = Convert.ToHexString(key).ToLowerInvariant();

        var heap = new byte[8192];
        Array.Fill(heap, (byte)0xEE);

        // Same order and spacing the client leaves them in: part3 first, something else in the gap, then part2 and part1 back to back.
        var part3 = WriteArray(heap, 0x40, key, 20, 12);
        var part2 = WriteArray(heap, 0xa0, key, 10, 10);
        var part1 = WriteArray(heap, 0xd0, key, 0, 10);

        // A decoy triple of the right lengths and the wrong bytes, so this only passes if page 1 is what picks the winner.
        var decoy3 = WriteArray(heap, 0x400, key, 0, 12);
        var decoy2 = WriteArray(heap, 0x430, key, 0, 10);
        var decoy1 = WriteArray(heap, 0x460, key, 4, 10);

        WriteHolder(heap, 0x800, decoy1, decoy2, decoy3);
        WriteHolder(heap, 0x900, part1, part2, part3);

        var arrays = new Dictionary<long, (long Klass, byte[] Data)>();
        RunningClient.CollectArrays(HeapBase, heap, heap.Length, arrays);

        var found = RunningClient.FindHolder(heap, heap.Length, arrays, new PageValidator(BuildPage(key)));
        if (found != expected)
        {
            Console.Error.WriteLine($"The split key was not put back together out of the heap image (got {found ?? "nothing"}).");
            return 1;
        }

        return 0;
    }

    private static int WriteArray(byte[] heap, int at, byte[] key, int from, int count)
    {
        BitConverter.GetBytes(Klass).CopyTo(heap, at);
        BitConverter.GetBytes(0L).CopyTo(heap, at + 0x08);
        BitConverter.GetBytes(0L).CopyTo(heap, at + 0x10);
        BitConverter.GetBytes((long)count).CopyTo(heap, at + 0x18);
        Buffer.BlockCopy(key, from, heap, at + 0x20, count);
        return at;
    }

    private static void WriteHolder(byte[] heap, int at, int part1, int part2, int part3)
    {
        BitConverter.GetBytes(HeapBase + part1).CopyTo(heap, at);
        BitConverter.GetBytes(HeapBase + part2).CopyTo(heap, at + 8);
        BitConverter.GetBytes(HeapBase + part3).CopyTo(heap, at + 16);
    }

    // A page 1 whose stored MAC is the one this key produces, so the validator has something real to say yes to.
    private static byte[] BuildPage(byte[] key)
    {
        var page = new byte[PageValidator.PageSize];
        for (var i = 0; i < page.Length; i++)
            page[i] = (byte)(i * 31 + 7);

        var hmacSalt = new byte[16];
        for (var i = 0; i < hmacSalt.Length; i++)
            hmacSalt[i] = (byte)(page[i] ^ 0x3a);

        var hmacKey = Rfc2898DeriveBytes.Pbkdf2(key, hmacSalt, 2, HashAlgorithmName.SHA512, 32);

        var input = new byte[4016 + 4];
        Buffer.BlockCopy(page, 16, input, 0, 4016);
        input[4016] = 1;

        using var mac = new HMACSHA512(hmacKey);
        mac.ComputeHash(input).CopyTo(page, 4032);
        return page;
    }
}
