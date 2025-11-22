using Ionic.Zip;
using Schale.Crypto;
using Serilog;

namespace BlueArchiveAPI.Configuration
{
    public class Resources
    {
        public static string ResourceDir = Path.Join(Path.GetDirectoryName(AppContext.BaseDirectory), "Resources");
        public static string DownloadDir = Path.Join(ResourceDir, "Downloaded");
        public static string CustomDir = Path.Join(ResourceDir, "Custom");
        public static string DumpedDir = Path.Join(ResourceDir, "Dumped");

        private static readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        public static async Task LoadResources()
        {
            if (!Directory.Exists(ResourceDir)) Directory.CreateDirectory(ResourceDir);
            if (!Directory.Exists(CustomDir)) Directory.CreateDirectory(CustomDir);

            var useCustomFile = Config.Instance.ServerConfiguration.UseCustomExcel;
            var versionTxtPath = Path.Combine(ResourceDir, "original_version.txt");
            var customTxtPath = Path.Combine(ResourceDir, "custom_version.txt");
            var baseUrl = $"https://ba.dn.nexoncdn.co.kr/com.nexon.bluearchive/{Config.Instance.ServerConfiguration.VersionId}";
            var resources = new List<string>() {
                "/Preload/TableBundles/ExcelDB.db",
                "/Preload/TableBundles/Excel.zip",
                "/GameData/TableBundles/HexaMap.zip"
            };

            string txtPath = useCustomFile ? customTxtPath : versionTxtPath;
            string otherTxtPath = !useCustomFile ? customTxtPath : versionTxtPath;
            string expected = useCustomFile
                            ? CheckCustomFileSize(resources).ToString()
                            : Config.Instance.ServerConfiguration.VersionId;
            string typeLabel = useCustomFile ? "Custom" : "Original";

            if (File.Exists(otherTxtPath)) File.Delete(otherTxtPath);
            if (File.Exists(txtPath) && File.ReadAllText(txtPath) == expected)
            {
                Log.Information($"{typeLabel} resources are already being processed…");
                return;
            }

            Log.Information($"{typeLabel} resources does not matched; deleting old resources…");
            if (Directory.Exists(DumpedDir))
                Directory.Delete(DumpedDir, recursive: true);

            if (!Directory.Exists(DumpedDir)) Directory.CreateDirectory(DumpedDir);

            if (useCustomFile)
            {
                Log.Information("Using custom resources, this may take a while...");
                var filesize = await CustomFiles(baseUrl, resources);
                File.WriteAllText(customTxtPath, filesize.ToString());
            }
            else
            {
                Log.Information("Using original resources, this may take a while...");
                await DownloadFiles(baseUrl, resources);
                File.WriteAllText(versionTxtPath, Config.Instance.ServerConfiguration.VersionId);
            }
#if DEBUG
            DumpResources(useCustomFile);
#endif
            Log.Information($"Resources extraction/dumping finished!");
        }

        // Original Excel Function.
        private static async Task DownloadFiles(string baseUrl, List<string> resourcesList)
        {
            if (!Directory.Exists(DownloadDir)) Directory.CreateDirectory(DownloadDir);

            foreach (var filename in resourcesList)
            {
                var downloadUrl = baseUrl + filename;
                var downloadFilePath = Path.Combine(DownloadDir, filename.Split('/').Last());
                await DownloadFile(downloadUrl, downloadFilePath);


                if (Path.GetExtension(downloadFilePath) == ".zip") ExtractExcels(downloadFilePath);
                else File.Copy(downloadFilePath, Path.Combine(DumpedDir, filename.Split('/').Last()));
            }
        }

        private static async Task DownloadFile(string url, string outputFile)
        {
            var fileName = Path.GetFileName(outputFile);

            // Get remote size
            long? remoteSize = await GetRemoteFileSizeAsync(url);

            // If local file exists and sizes match, skip download
            if (File.Exists(outputFile) && remoteSize.HasValue)
            {
                long localSize = new FileInfo(outputFile).Length;
                if (localSize == remoteSize.Value)
                {
                    Log.Information("Skipping download of {FileName}", fileName);
                    Log.Debug(
                        "Local size {Local:N0} / Remote size {Remote:N0} bytes",
                        localSize, remoteSize.Value
                    );
                    return;
                }
            }

            // Otherwise, download fresh
            Log.Information("Downloading {FileName}...", fileName);
            byte[] data = await httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(outputFile, data);
        }

        // Custom Excel Function

        private static async Task<long?> GetRemoteFileSizeAsync(string url)
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
                return null;

            return response.Content.Headers.ContentLength;
        }

        private static async Task<long> CustomFiles(string url, List<string> resourcesList)
        {
            // Uses filesize as a check for the custom file
            long filesize = 0;
            foreach (var filename in resourcesList)
            {
                var customFilePath = Path.Combine(CustomDir, filename.Split('/').Last());

                // If the custom file doesn't exist, copy it from the downloaded file
                if (!File.Exists(customFilePath))
                {
                    await DownloadFile(Path.Combine(url, filename), Path.Combine(DownloadDir, filename.Split('/').Last()));
                    File.Copy(Path.Combine(DownloadDir, filename.Split('/').Last()), customFilePath);
                }

                filesize += new FileInfo(customFilePath).Length;

                if (Path.GetExtension(customFilePath) == ".zip") ExtractExcels(customFilePath);
                else File.Copy(customFilePath, Path.Combine(DumpedDir, filename.Split('/').Last()));
            }
            return filesize;
        }

        private static long CheckCustomFileSize(List<string> resourcesList)
        {
            long filesize = 0;
            foreach (var filename in resourcesList)
            {
                var customFilePath = Path.Combine(CustomDir, filename);
                if (File.Exists(customFilePath)) filesize += new FileInfo(customFilePath).Length;
            }
            return filesize;
        }

        private static void ExtractExcels(string filePath)
        {
            Log.Information("Extracting {fileName}...", Path.GetFileNameWithoutExtension(filePath));
            using var zip = ZipFile.Read(filePath);
            zip.Password = Convert.ToBase64String(TableService.CreatePassword(Path.GetFileName(filePath)));
            zip.ExtractAll(Path.Combine(DumpedDir, Path.GetFileNameWithoutExtension(filePath)), ExtractExistingFileAction.OverwriteSilently);
        }

#if DEBUG
        private static void DumpResources(bool useCustomFile)
        {
            Log.Debug("Dumping resources...");
            var excelDbDir = useCustomFile ? CustomDir : DownloadDir;
            var excelPath = Path.Combine(DumpedDir, "Excel");
            var excelDbPath = Path.Combine(excelDbDir, "ExcelDB.db");
            var excelDumpPath = Path.Combine(ResourceDir, DumpedDir, "Dumped_Excel");
            var excelDbDumpPath = Path.Combine(ResourceDir, DumpedDir, "Dumped_ExcelDB");
            Directory.CreateDirectory(excelDumpPath);
            Directory.CreateDirectory(excelDbDumpPath);
            TableService.DumpExcels(excelPath, excelDumpPath);
            TableService.DumpExcelDB(excelDbPath, excelDbDumpPath);
        }
#endif
    }
}
