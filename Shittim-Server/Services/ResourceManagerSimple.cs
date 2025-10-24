using Ionic.Zip;
using Plana.Crypto;

namespace BlueArchiveAPI.Services
{
    public class ResourceManagerSimple
    {
        public static string ResourceDir = Path.Join(AppContext.BaseDirectory, "Resources");
        public static string DownloadDir = Path.Join(ResourceDir, "Downloaded");
        public static string DumpedDir = Path.Join(ResourceDir, "Dumped");

        // Game version - update this when game updates
        // This is the CDN path identifier, not the client version
        private const string GAME_VERSION = "b97c05ca58a24270";

        private static readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        static ResourceManagerSimple()
        {
            // Add User-Agent to avoid CDN blocking
            httpClient.DefaultRequestHeaders.Add("User-Agent", "UnityPlayer/2022.3.21f1 (UnityWebRequest/1.0, libcurl/8.5.0-DEV)");
            httpClient.DefaultRequestHeaders.Add("Accept", "*/*");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "identity");
        }

        public static async Task LoadResources(ILogger logger)
        {
            var versionFile = Path.Combine(ResourceDir, "version.txt");
            var baseUrl = $"https://ba.dn.nexoncdn.co.kr/com.nexon.bluearchive/{GAME_VERSION}";
            
            var resources = new List<string>
            {
                "/Preload/TableBundles/ExcelDB.db",
                "/Preload/TableBundles/Excel.zip",
                "/GameData/TableBundles/HexaMap.zip"
            };
            
            // Check if already downloaded
            if (File.Exists(versionFile) && File.ReadAllText(versionFile) == GAME_VERSION)
            {
                logger.LogInformation("Resources already downloaded (version {Version})", GAME_VERSION);
                return;
            }

            // Delete old resources if version mismatch
            if (Directory.Exists(DumpedDir))
            {
                logger.LogInformation("Original resources does not matched; deleting old resources...");
                Directory.Delete(DumpedDir, recursive: true);
            }

            logger.LogInformation("Using original resources, this may take a while...");

            // Create directories
            Directory.CreateDirectory(DownloadDir);
            Directory.CreateDirectory(DumpedDir);

            // Download and process all resources
            foreach (var resource in resources)
            {
                var downloadUrl = baseUrl + resource;
                var downloadPath = Path.Combine(DownloadDir, Path.GetFileName(resource));
                
                await DownloadFile(downloadUrl, downloadPath, logger);
                
                if (Path.GetExtension(downloadPath) == ".zip")
                {
                    var extractName = Path.GetFileNameWithoutExtension(downloadPath);
                    ExtractZip(downloadPath, Path.Combine(DumpedDir, extractName), logger);
                }
                else
                {
                    File.Copy(downloadPath, Path.Combine(DumpedDir, Path.GetFileName(resource)), true);
                }
            }

            // Save version
            File.WriteAllText(versionFile, GAME_VERSION);
            
            logger.LogInformation("Resources extraction/dumping finished!");
        }

        private static async Task DownloadFile(string url, string outputPath, ILogger logger)
        {
            var fileName = Path.GetFileName(outputPath);

            if (File.Exists(outputPath))
            {
                logger.LogInformation("Skipping download of {FileName}", fileName);
                return;
            }

            logger.LogInformation("Downloading {FileName}...", fileName);
            var data = await httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(outputPath, data);
            logger.LogInformation("Downloaded {FileName} ({Size:N0} bytes)", fileName, data.Length);
        }

        private static void ExtractZip(string zipPath, string extractPath, ILogger logger)
        {
            logger.LogInformation("Extracting {FileName}...", Path.GetFileNameWithoutExtension(zipPath));
            
            var fileName = Path.GetFileName(zipPath);
            var password = TableService.CreatePassword(fileName);
            var passwordBase64 = Convert.ToBase64String(password);
            
            logger.LogInformation("DEBUG: Filename for password: {FileName}", fileName);
            logger.LogInformation("DEBUG: Password bytes: {PasswordBytes}", BitConverter.ToString(password));
            logger.LogInformation("DEBUG: Password (base64): {PasswordBase64}", passwordBase64);

            using var zip = ZipFile.Read(zipPath);
            zip.Password = passwordBase64;
            zip.ExtractAll(extractPath, ExtractExistingFileAction.OverwriteSilently);
            
            logger.LogInformation("Extracted {FileName}", Path.GetFileNameWithoutExtension(zipPath));
        }
    }
}
