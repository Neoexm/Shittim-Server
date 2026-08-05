using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Core.Crypto;
using Ionic.Zip;
using System.Text;
using Schale.Crypto;

namespace BlueArchiveAPI.Services
{
    public class ResourceService
    {
        public static string ResourceDir = Path.Join(Path.GetDirectoryName(AppContext.BaseDirectory), "Resources");
        public static string DownloadDir = Path.Join(ResourceDir, "Downloaded");
        public static string CustomDir = Path.Join(ResourceDir, "Custom");
        public static string DumpedDir = Path.Join(ResourceDir, "Dumped");

        private static readonly HttpClient httpClient = new()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        // A deadline on the whole transfer is wrong in both directions: ExcelDB.db on a slow line gets killed near the end with everything to do again, and a socket that has gone silent with most of the file left keeps the server sitting there until it expires. This is the gap between two reads instead.
        internal static TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

        static ResourceService()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        }

        public static async Task LoadResources(bool useCustomFile = false)
        {
            if (!Directory.Exists(ResourceDir)) Directory.CreateDirectory(ResourceDir);
            if (!Directory.Exists(CustomDir)) Directory.CreateDirectory(CustomDir);
            if (!Directory.Exists(DumpedDir)) Directory.CreateDirectory(DumpedDir);

            string versionId = Config.Instance.ServerConfiguration.VersionId;
            
            // Version check is handled at startup by BlueArchiveVersionResolver
            Console.WriteLine($"[Resource Manager] Using VersionId: {versionId}");

            var versionTxtPath = Path.Combine(ResourceDir, "original_version.txt");
            var customTxtPath = Path.Combine(ResourceDir, "custom_version.txt");
            var baseUrl = Config.Instance.ServerConfiguration.CdnBaseUrl;
            var resources = new List<string>() {
                "/Preload/TableBundles/ExcelDB.db",
                "/Preload/TableBundles/Excel.zip",
                "/GameData/TableBundles/HexaMap.zip"
            };

            string txtPath = useCustomFile ? customTxtPath : versionTxtPath;
            string otherTxtPath = !useCustomFile ? customTxtPath : versionTxtPath;
            string expected = useCustomFile
                            ? CheckCustomFileSize(resources).ToString()
                            : versionId;
            string typeLabel = useCustomFile ? "Custom" : "Original";

            if (File.Exists(otherTxtPath)) File.Delete(otherTxtPath);
            if (File.Exists(txtPath) && File.ReadAllText(txtPath) == expected && Directory.Exists(DumpedDir) && Directory.GetFiles(DumpedDir, "*", SearchOption.AllDirectories).Length > 0)
            {
                Console.WriteLine($"{typeLabel} resources are already up to date.");
                return;
            }

            // When auto resource update is off and we already have resources, keep the existing ones instead of re-downloading on a version change.
            // A first-time bootstrap (no resources present yet) still downloads so the server can start.
            bool hasExistingResources = Directory.Exists(DumpedDir)
                && Directory.GetFiles(DumpedDir, "*", SearchOption.AllDirectories).Length > 0;
            if (!Config.Instance.ServerConfiguration.AutoUpdateResources && hasExistingResources)
            {
                Console.WriteLine($"{typeLabel} resources are out of date, but auto resource update is disabled; keeping existing resources.");
                return;
            }

            Console.WriteLine($"{typeLabel} resources version mismatch; fetching new resources...");

            if (useCustomFile)
            {
                Console.WriteLine("Using custom resources, this may take a while...");
                ResetDumped();
                var filesize = await CustomFiles(baseUrl, resources);
                File.WriteAllText(customTxtPath, filesize.ToString());
            }
            else
            {
                Console.WriteLine("Using original resources, this may take a while...");
                await DownloadFiles(baseUrl, resources);
                File.WriteAllText(versionTxtPath, versionId);
            }

            Console.WriteLine("Resource extraction finished!");
        }

        private static async Task DownloadFiles(string baseUrl, List<string> resourcesList)
        {
            if (!Directory.Exists(DownloadDir)) Directory.CreateDirectory(DownloadDir);

            var fetched = new List<string>();
            foreach (var filename in resourcesList)
            {
                var downloadUrl = baseUrl + filename;
                var downloadFilePath = Path.Combine(DownloadDir, filename.Split('/').Last());
                await DownloadFile(downloadUrl, downloadFilePath);
                fetched.Add(downloadFilePath);
            }

            // Every file is on disk before the old set goes. An update that dies mid-transfer leaves the server running on the resources it already had rather than on nothing at all.
            ResetDumped();

            foreach (var downloadFilePath in fetched)
            {
                if (Path.GetExtension(downloadFilePath) == ".zip")
                    ExtractExcels(downloadFilePath);
                else
                    File.Copy(downloadFilePath, Path.Combine(DumpedDir, Path.GetFileName(downloadFilePath)), true);
            }
        }

        private static void ResetDumped()
        {
            if (Directory.Exists(DumpedDir))
                Directory.Delete(DumpedDir, recursive: true);
            Directory.CreateDirectory(DumpedDir);
        }

        internal static async Task DownloadFile(string url, string outputFile)
        {
            var fileName = Path.GetFileName(outputFile);

            long? remoteSize = await GetRemoteFileSizeAsync(url);

            if (File.Exists(outputFile) && remoteSize.HasValue)
            {
                long localSize = new FileInfo(outputFile).Length;
                if (localSize == remoteSize.Value)
                {
                    Console.WriteLine($"Skipping download of {fileName} (already exists)");
                    return;
                }
            }

            Console.WriteLine($"Downloading {fileName}...");

            var partial = outputFile + ".part";
            long received = 0;
            try
            {
                using var stall = new CancellationTokenSource(StallTimeout);
                using var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stall.Token);
                response.EnsureSuccessStatusCode();
                stall.CancelAfter(StallTimeout);

                var total = response.Content.Headers.ContentLength ?? remoteSize ?? 0;
                var step = total > 0 ? Math.Max(total / 10, 1) : 16L * 1024 * 1024;
                var nextReport = step;

                using (var body = await response.Content.ReadAsStreamAsync(stall.Token))
                using (var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    while (true)
                    {
                        int read = await body.ReadAsync(buffer, stall.Token);
                        if (read == 0) break;

                        // Pushed forward on every read that produced something, so a download that is still moving never expires no matter how long it runs.
                        stall.CancelAfter(StallTimeout);

                        await file.WriteAsync(buffer.AsMemory(0, read), stall.Token);
                        received += read;

                        if (received >= nextReport)
                        {
                            Console.WriteLine(total > 0
                                ? $"Downloading {fileName}... {received * 100 / total}% ({received:N0} of {total:N0} bytes)"
                                : $"Downloading {fileName}... {received:N0} bytes");
                            nextReport += step;
                        }
                    }
                }

                if (total > 0 && received != total)
                    throw new IOException($"{fileName} was cut short: {received:N0} of {total:N0} bytes arrived.");
            }
            catch (OperationCanceledException)
            {
                File.Delete(partial);
                throw new IOException($"{fileName} stopped transferring after {received:N0} bytes and sent nothing for {StallTimeout.TotalSeconds:N0}s. Nothing was written.");
            }
            catch
            {
                File.Delete(partial);
                throw;
            }

            // The download directory is what the version marker and the size check are read against next run, so the name only goes on a file that arrived whole.
            File.Move(partial, outputFile, true);
            Console.WriteLine($"Downloaded {fileName} ({received:N0} bytes)");
        }

        private static async Task<long?> GetRemoteFileSizeAsync(string url)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var timeout = new CancellationTokenSource(StallTimeout);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                return response.IsSuccessStatusCode ? response.Content.Headers.ContentLength : null;
            }
            catch
            {
                return null;
            }
        }

        private static void ExtractExcels(string filePath)
        {
            Console.WriteLine($"Extracting {Path.GetFileNameWithoutExtension(filePath)}...");
            
            var password = CreatePassword(Path.GetFileName(filePath));
            var passwordBase64 = Convert.ToBase64String(password);
            
            using var zip = ZipFile.Read(filePath);
            zip.Password = passwordBase64;
            zip.ExtractAll(Path.Combine(DumpedDir, Path.GetFileNameWithoutExtension(filePath)), ExtractExistingFileAction.OverwriteSilently);
            
            Console.WriteLine($"Extracted {Path.GetFileNameWithoutExtension(filePath)}");
        }

        private static async Task<long> CustomFiles(string url, List<string> resourcesList)
        {
            long filesize = 0;
            foreach (var filename in resourcesList)
            {
                var customFilePath = Path.Combine(CustomDir, filename.Split('/').Last());

                if (!File.Exists(customFilePath))
                {
                    var downloadPath = Path.Combine(DownloadDir, filename.Split('/').Last());
                    await DownloadFile(Path.Combine(url, filename), downloadPath);
                    File.Copy(downloadPath, customFilePath, true);
                }

                filesize += new FileInfo(customFilePath).Length;

                if (Path.GetExtension(customFilePath) == ".zip")
                    ExtractExcels(customFilePath);
                else
                    File.Copy(customFilePath, Path.Combine(DumpedDir, filename.Split('/').Last()), true);
            }
            return filesize;
        }

        private static long CheckCustomFileSize(List<string> resourcesList)
        {
            long filesize = 0;
            foreach (var filename in resourcesList)
            {
                var customFilePath = Path.Combine(CustomDir, filename.Split('/').Last());
                if (File.Exists(customFilePath))
                    filesize += new FileInfo(customFilePath).Length;
            }
            return filesize;
        }

        private static byte[] CreatePassword(string key, int length = 20)
        {
            byte[] password = new byte[(int)Math.Round((decimal)(length / 4 * 3))];

            using var xxhash = XXHash32.Create();
            xxhash.ComputeHash(Encoding.UTF8.GetBytes(key));

            var mt = new MersenneTwister((int)xxhash.HashUInt32);

            int i = 0;
            while (i < password.Length)
            {
                Array.Copy(BitConverter.GetBytes(mt.Next()), 0, password, i, Math.Min(4, password.Length - i));
                i += 4;
            }

            return password;
        }
    }
}