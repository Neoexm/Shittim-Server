using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MemoryDumper;

class Program
{
    [DllImport("kernel32.dll")]
    static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll")]
    static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [DllImport("kernel32.dll")]
    static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr hObject);

    const int PROCESS_QUERY_INFORMATION = 0x0400;
    const int PROCESS_WM_READ = 0x0010;
    const int PROCESS_VM_OPERATION = 0x0008;
    const int PAGE_NOACCESS = 0x01;
    const int PAGE_GUARD = 0x100;
    const int MEM_COMMIT = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEM_INFO
    {
        public ushort processorArchitecture;
        ushort reserved;
        public uint pageSize;
        public IntPtr minimumApplicationAddress;
        public IntPtr maximumApplicationAddress;
        public IntPtr activeProcessorMask;
        public uint numberOfProcessors;
        public uint processorType;
        public uint allocationGranularity;
        public ushort processorLevel;
        public ushort processorRevision;
    }

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

    static void Main(string[] args)
    {
        Console.WriteLine("Blue Archive Memory Dumper");
        Console.WriteLine("Looking for BlueArchive.exe...");

        Process? process = null;
        while (process == null)
        {
            var processes = Process.GetProcessesByName("BlueArchive");
            if (processes.Length > 0)
            {
                process = processes[0];
                Console.WriteLine($"Found BlueArchive.exe (PID: {process.Id})");
            }
            else
            {
                Console.WriteLine("Waiting for BlueArchive.exe to start...");
                Thread.Sleep(2000);
            }
        }

        string outputFile = $"bluearchive_memory_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        Console.WriteLine($"Dumping memory to: {outputFile}");
        Console.WriteLine("Press Ctrl+C to stop...\n");

        IntPtr processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ | PROCESS_VM_OPERATION, false, process.Id);
        if (processHandle == IntPtr.Zero)
        {
            Console.WriteLine("Failed to open process. Run as administrator.");
            return;
        }

        SYSTEM_INFO sysInfo;
        GetSystemInfo(out sysInfo);

        var scannedRegions = new HashSet<string>();
        var regionData = new Dictionary<string, byte[]>();

        using (var writer = new StreamWriter(outputFile, true, Encoding.UTF8, 65536))
        {
            writer.AutoFlush = false;

            long totalBytesWritten = 0;
            int scanCount = 0;

            try
            {
                while (!process.HasExited)
                {
                    scanCount++;
                    IntPtr address = sysInfo.minimumApplicationAddress;
                    var newRegionsFound = 0;
                    var updatedRegions = 0;

                    while (address.ToInt64() < sysInfo.maximumApplicationAddress.ToInt64())
                    {
                        MEMORY_BASIC_INFORMATION memInfo;
                        int result = VirtualQueryEx(processHandle, address, out memInfo, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));

                        if (result == 0)
                            break;

                        if (memInfo.State == MEM_COMMIT &&
                            memInfo.Protect != PAGE_NOACCESS &&
                            (memInfo.Protect & PAGE_GUARD) == 0)
                        {
                            int regionSize = (int)memInfo.RegionSize;
                            if (regionSize > 0 && regionSize < 100 * 1024 * 1024)
                            {
                                byte[] buffer = new byte[regionSize];
                                int bytesRead;

                                if (ReadProcessMemory(processHandle, memInfo.BaseAddress, buffer, regionSize, out bytesRead) && bytesRead > 0)
                                {
                                    string regionKey = $"{memInfo.BaseAddress.ToInt64():X16}";

                                    if (!regionData.ContainsKey(regionKey))
                                    {
                                        regionData[regionKey] = buffer;
                                        scannedRegions.Add(regionKey);
                                        newRegionsFound++;

                                        writer.WriteLine($"\n========== NEW MEMORY REGION ==========");
                                        writer.WriteLine($"Scan: {scanCount} | Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                                        writer.WriteLine($"Base Address: 0x{memInfo.BaseAddress.ToInt64():X16}");
                                        writer.WriteLine($"Size: {bytesRead:N0} bytes ({bytesRead / 1024.0:F2} KB)");
                                        writer.WriteLine($"Protection: 0x{memInfo.Protect:X8}");
                                        writer.WriteLine("==========================================\n");

                                        WriteMemoryData(writer, buffer, bytesRead, memInfo.BaseAddress.ToInt64());
                                        totalBytesWritten += bytesRead;
                                    }
                                    else
                                    {
                                        var oldData = regionData[regionKey];
                                        if (!buffer.AsSpan(0, bytesRead).SequenceEqual(oldData.AsSpan(0, Math.Min(bytesRead, oldData.Length))))
                                        {
                                            regionData[regionKey] = buffer;
                                            updatedRegions++;

                                            writer.WriteLine($"\n========== MEMORY REGION UPDATED ==========");
                                            writer.WriteLine($"Scan: {scanCount} | Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                                            writer.WriteLine($"Base Address: 0x{memInfo.BaseAddress.ToInt64():X16}");
                                            writer.WriteLine($"Size: {bytesRead:N0} bytes");
                                            writer.WriteLine("===========================================\n");

                                            WriteMemoryData(writer, buffer, bytesRead, memInfo.BaseAddress.ToInt64());
                                            totalBytesWritten += bytesRead;
                                        }
                                    }
                                }
                            }
                        }

                        address = new IntPtr(memInfo.BaseAddress.ToInt64() + (long)memInfo.RegionSize);
                    }

                    writer.Flush();

                    Console.Write($"\rScan #{scanCount} | Regions: {scannedRegions.Count} | New: {newRegionsFound} | Updated: {updatedRegions} | Written: {totalBytesWritten / (1024.0 * 1024.0):F2} MB");

                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine($"\n\nERROR: {ex.Message}");
                Console.WriteLine($"\nError: {ex.Message}");
            }
            finally
            {
                writer.Flush();
                CloseHandle(processHandle);
                Console.WriteLine("\n\nProcess exited or dumping stopped.");
                Console.WriteLine($"Total data written: {totalBytesWritten / (1024.0 * 1024.0):F2} MB");
            }
        }
    }

    static void WriteMemoryData(StreamWriter writer, byte[] buffer, int size, long baseAddress)
    {
        const int bytesPerLine = 16;

        for (int i = 0; i < size; i += bytesPerLine)
        {
            int lineSize = Math.Min(bytesPerLine, size - i);

            writer.Write($"0x{(baseAddress + i):X16}  ");

            for (int j = 0; j < bytesPerLine; j++)
            {
                if (j < lineSize)
                    writer.Write($"{buffer[i + j]:X2} ");
                else
                    writer.Write("   ");

                if (j == 7)
                    writer.Write(" ");
            }

            writer.Write(" |");
            for (int j = 0; j < lineSize; j++)
            {
                byte b = buffer[i + j];
                writer.Write(b >= 32 && b < 127 ? (char)b : '.');
            }
            writer.WriteLine("|");
        }

        var asciiText = ExtractAsciiStrings(buffer, size);
        if (!string.IsNullOrEmpty(asciiText))
        {
            writer.WriteLine("\n--- Extracted ASCII Strings (min length 4) ---");
            writer.WriteLine(asciiText);
            writer.WriteLine("----------------------------------------------\n");
        }
    }

    static string ExtractAsciiStrings(byte[] buffer, int size)
    {
        var sb = new StringBuilder();
        var currentString = new StringBuilder();

        for (int i = 0; i < size; i++)
        {
            byte b = buffer[i];
            if ((b >= 32 && b < 127) || b == 9 || b == 10 || b == 13)
            {
                currentString.Append((char)b);
            }
            else
            {
                if (currentString.Length >= 4)
                {
                    sb.AppendLine(currentString.ToString().Trim());
                }
                currentString.Clear();
            }
        }

        if (currentString.Length >= 4)
        {
            sb.AppendLine(currentString.ToString().Trim());
        }

        return sb.ToString();
    }
}
