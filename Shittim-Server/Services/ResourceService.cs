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
            Timeout = TimeSpan.FromMinutes(10)
        };

        static ResourceService()
        {
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        public static async Task LoadResources(bool useCustomFile = false)
        {
            if (!Directory.Exists(ResourceDir)) Directory.CreateDirectory(ResourceDir);
            if (!Directory.Exists(CustomDir)) Directory.CreateDirectory(CustomDir);
            if (!Directory.Exists(DumpedDir)) Directory.CreateDirectory(DumpedDir);

            // Check for version updates if enabled
            string versionId = Config.Instance.ServerConfiguration.VersionId;
            
            if (Config.Instance.ServerConfiguration.AutoCheckVersion)
            {
                Console.WriteLine("\n[Version Check] Checking for game updates...");
                var (needsUpdate, latestVersion) = await VersionFetcherService.CheckForUpdates(
                    versionId,
                    Config.Instance.ServerConfiguration.ServerInfoUrl
                );

                if (needsUpdate && latestVersion != null)
                {
                    if (Config.Instance.ServerConfiguration.AutoUpdateVersion)
                    {
                        Console.WriteLine($"[Version Check] Auto-updating to version: {latestVersion}");
                        VersionFetcherService.UpdateConfigVersion(latestVersion);
                        versionId = latestVersion;
                    }
                    else
                    {
                        Console.WriteLine($"[Version Check] New version available: {latestVersion}");
                        Console.WriteLine($"[Version Check] Set AutoUpdateVersion=true to auto-update or manually update VersionId in config");
                        Console.WriteLine($"[Version Check] Continuing with current version: {versionId}");
                    }
                }
            }

            var versionTxtPath = Path.Combine(ResourceDir, "original_version.txt");
            var customTxtPath = Path.Combine(ResourceDir, "custom_version.txt");
            var baseUrl = $"https://ba.dn.nexoncdn.co.kr/com.nexon.bluearchive/{versionId}";
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

            Console.WriteLine($"{typeLabel} resources version mismatch; deleting old resources...");
            if (Directory.Exists(DumpedDir))
                Directory.Delete(DumpedDir, recursive: true);

            if (!Directory.Exists(DumpedDir)) Directory.CreateDirectory(DumpedDir);

            if (useCustomFile)
            {
                Console.WriteLine("Using custom resources, this may take a while...");
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

            foreach (var filename in resourcesList)
            {
                var downloadUrl = baseUrl + filename;
                var downloadFilePath = Path.Combine(DownloadDir, filename.Split('/').Last());
                await DownloadFile(downloadUrl, downloadFilePath);

                if (Path.GetExtension(downloadFilePath) == ".zip") 
                    ExtractExcels(downloadFilePath);
                else 
                    File.Copy(downloadFilePath, Path.Combine(DumpedDir, filename.Split('/').Last()), true);
            }
        }

        private static async Task DownloadFile(string url, string outputFile)
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
            byte[] data = await httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(outputFile, data);
            Console.WriteLine($"Downloaded {fileName} ({data.Length:N0} bytes)");
        }

        private static async Task<long?> GetRemoteFileSizeAsync(string url)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
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