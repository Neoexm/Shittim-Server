using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace ExcelDbKeyTool;

// The 32 bytes Queuing_GetCryptoKeys handed over are still on the client's il2cpp heap as the base64 string they arrived in, so with the game open the key can be lifted straight out of it instead of out of a capture. Nothing about the heap layout survives a client update, which is why this looks for the shape of a key rather than walking to an offset, and why every hit is then tried against the client's own ExcelDB.db - a scan that loose would be useless without something to check its answers against, and nothing but the real key opens that file.
internal static class RunningClient
{
    private const string ExcelDbRelative = @"BlueArchive_Data\StreamingAssets\PUB\Resource\Preload\TableBundles\ExcelDB.db";

    // Longest thing Collect can match is a 64 character hex key in UTF-16, so chunks have to share at least that much or a key straddling the boundary is lost.
    private const int Overlap = 256;

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
            throw new InvalidOperationException("BlueArchive.exe is not running. Start the game, let it reach the title screen so it has asked for the crypto keys, then try again.");

        // Before MainModule, which needs the same rights and would otherwise report an elevated client as a bare "Access is denied".
        var processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, process.Id);
        if (processHandle == IntPtr.Zero)
            throw new InvalidOperationException($"Could not open BlueArchive.exe (PID {process.Id}) for reading. Run this tool as administrator.");

        string excelDbPath;
        List<string> candidates;
        try
        {
            excelDbPath = Path.Combine(Path.GetDirectoryName(process.MainModule!.FileName)!, ExcelDbRelative);
            if (!File.Exists(excelDbPath))
                throw new InvalidOperationException($"Found BlueArchive.exe (PID {process.Id}) but there is no ExcelDB.db at {excelDbPath}, and without it a candidate key cannot be checked.");

            candidates = Scan(processHandle, progress);
        }
        finally
        {
            CloseHandle(processHandle);
        }

        progress($"Trying {candidates.Count} candidates against ExcelDB.db...");

        foreach (var hex in candidates)
        {
            if (!Opens(excelDbPath, hex))
                continue;

            return (new DecodeResult(hex, Convert.ToBase64String(Convert.FromHexString(hex))), $"BlueArchive.exe (PID {process.Id})");
        }

        throw new InvalidOperationException(candidates.Count == 0
            ? "Nothing in the client's memory was shaped like an ExcelDB key. The key only lands there once the client has been through Queuing_GetCryptoKeys, so take it as far as the title screen first."
            : $"None of the {candidates.Count} candidates read out of the client opened {excelDbPath}.");
    }

    private static List<string> Scan(IntPtr processHandle, Action<string> progress)
    {
        var hits = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
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

            // Managed strings sit on writable commit. Skipping everything else leaves out the mapped images and the read-only asset views, which is most of a Unity process.
            if (memInfo.State == MEM_COMMIT && (memInfo.Protect & PAGE_GUARD) == 0 &&
                (protect == PAGE_READWRITE || protect == PAGE_WRITECOPY || protect == PAGE_EXECUTE_READWRITE))
            {
                for (long offset = 0; offset < regionSize; offset += buffer.Length - Overlap)
                {
                    int want = (int)Math.Min(buffer.Length, regionSize - offset);
                    if (!ReadProcessMemory(processHandle, new IntPtr(memInfo.BaseAddress.ToInt64() + offset), buffer, want, out int read) || read <= 0)
                        continue;

                    Collect(buffer, read, hits, seen);
                    scanned += read;
                }

                if (scanned - reported >= 256L * 1024 * 1024)
                {
                    reported = scanned;
                    progress($"Read {scanned / (1024 * 1024):N0} MB of the client, {hits.Count} candidates so far...");
                }
            }

            address = new IntPtr(next);
            if (next <= 0)
                break;
        }

        return hits;
    }

    // Both encodings because a managed string is UTF-16 but the same value can have been marshalled out to a narrow one, and both spellings because the key travels as base64 and is handed to SQLCipher as hex.
    public static void Collect(byte[] buffer, int length, List<string> hits, HashSet<string> seen)
    {
        for (var i = 0; i < length; i++)
        {
            if (i + 44 <= length && buffer[i + 43] == (byte)'=' && (i == 0 || !IsBase64(buffer[i - 1])) && IsBase64Run(buffer, i, 1))
                Add(ReadBase64(buffer, i, 1), hits, seen);

            if (i + 88 <= length && buffer[i + 86] == (byte)'=' && buffer[i + 87] == 0 && (i < 2 || !(IsBase64(buffer[i - 2]) && buffer[i - 1] == 0)) && IsBase64Run(buffer, i, 2))
                Add(ReadBase64(buffer, i, 2), hits, seen);

            // Nine in ten bytes are not hex at all, and skipping them here is worth a third of the scan.
            if (!IsHex(buffer[i]))
                continue;

            if (i + 64 <= length && IsHexRun(buffer, i, 1) && (i == 0 || !IsHex(buffer[i - 1])) && (i + 64 >= length || !IsHex(buffer[i + 64])))
                Add(ReadHex(buffer, i, 1), hits, seen);

            if (i + 128 <= length && IsHexRun(buffer, i, 2) && (i < 2 || !(IsHex(buffer[i - 2]) && buffer[i - 1] == 0)) && (i + 128 >= length || !IsHex(buffer[i + 128])))
                Add(ReadHex(buffer, i, 2), hits, seen);
        }
    }

    private static void Add(string? hex, List<string> hits, HashSet<string> seen)
    {
        if (hex != null && seen.Add(hex))
            hits.Add(hex);
    }

    private static bool IsBase64Run(byte[] buffer, int start, int stride)
    {
        for (var j = 0; j < 43; j++)
        {
            var at = start + j * stride;
            if (!IsBase64(buffer[at]) || (stride == 2 && buffer[at + 1] != 0))
                return false;
        }

        return true;
    }

    private static bool IsHexRun(byte[] buffer, int start, int stride)
    {
        for (var j = 0; j < 64; j++)
        {
            var at = start + j * stride;
            if (!IsHex(buffer[at]) || (stride == 2 && buffer[at + 1] != 0))
                return false;
        }

        return true;
    }

    private static string? ReadBase64(byte[] buffer, int start, int stride)
    {
        var chars = new char[44];
        for (var j = 0; j < 44; j++)
            chars[j] = (char)buffer[start + j * stride];

        try
        {
            var bytes = Convert.FromBase64CharArray(chars, 0, chars.Length);
            return bytes.Length == 32 ? Convert.ToHexString(bytes).ToLowerInvariant() : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string ReadHex(byte[] buffer, int start, int stride)
    {
        var chars = new char[64];
        for (var j = 0; j < 64; j++)
            chars[j] = char.ToLowerInvariant((char)buffer[start + j * stride]);

        return new string(chars);
    }

    private static bool IsBase64(byte value)
    {
        return (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z') || (value >= '0' && value <= '9') || value == '+' || value == '/';
    }

    private static bool IsHex(byte value)
    {
        return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');
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

internal static class MemoryScanSelfTest
{
    public static int Run()
    {
        var key = Enumerable.Range(0, 32).Select(value => (byte)(value * 7 + 3)).ToArray();
        var expected = Convert.ToHexString(key).ToLowerInvariant();

        foreach (var text in new[] { Convert.ToBase64String(key), expected })
        {
            foreach (var encoding in new Encoding[] { Encoding.ASCII, Encoding.Unicode })
            {
                var haystack = new byte[8192];
                Array.Fill(haystack, (byte)0xFF);

                var needle = encoding.GetBytes(text);
                Array.Copy(needle, 0, haystack, 2048, needle.Length);
                Array.Fill(haystack, (byte)0, 2044, 4);
                Array.Fill(haystack, (byte)0, 2048 + needle.Length, 4);

                var hits = new List<string>();
                RunningClient.Collect(haystack, haystack.Length, hits, new HashSet<string>(StringComparer.Ordinal));

                if (!hits.Contains(expected))
                {
                    Console.Error.WriteLine($"{encoding.WebName} {(text == expected ? "hex" : "base64")} key was not found by the memory scan.");
                    return 1;
                }
            }
        }

        return 0;
    }
}
