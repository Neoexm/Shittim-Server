using BlueArchiveAPI.Configuration;
using BlueArchiveAPI.Services;
using Google.FlatBuffers;
using Microsoft.Data.Sqlite;
using Schale.Crypto;
using Schale.FlatData;

namespace Shittim_Server.Services
{
    // The "View All Recruitments" banner list is built entirely client-side from the client's
    // encrypted ExcelDB.db (the server never serves it). Some recruitment rows are active but
    // have an empty GachaBannerPath, so the client draws a blank, still-selectable banner (and
    // the empty slot pushes the real banners into a gap). That's confusing — you can pick a
    // banner you can't see.
    //
    // This service fixes that at the source: at startup it opens the client's ExcelDB.db
    // (SQLCipher, same key ExcelTableService reads with), and for every recruitment row whose
    // GachaBannerPath is empty it fills in the art of the most closely-related banner (longest
    // shared Id prefix = same event/family), so the banner renders instead of showing blank.
    // Same idea as ClientMetadataPatchService patching global-metadata.dat in the install tree.
    public class ClientExcelBannerPatchService : IHostedService
    {
        private const string SchemaTable = "ShopRecruitDBSchema";

        private readonly ILogger<ClientExcelBannerPatchService> logger;

        public ClientExcelBannerPatchService(ILogger<ClientExcelBannerPatchService> logger)
        {
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!Config.Instance.ServerConfiguration.AutoPatchClientBanners)
                    return Task.CompletedTask;

                var dbPath = GetExcelDbPath();
                if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
                {
                    logger.LogWarning("Client banner auto-patch enabled but client ExcelDB.db was not found ({Path})", dbPath);
                    return Task.CompletedTask;
                }

                PatchBanners(dbPath);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to patch client recruitment banners");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private void PatchBanners(string dbPath)
        {
            SqliteProvider.EnsureInitialized();
            // ExcelDB flatbuffer strings are stored plaintext (the SQLCipher layer is the only
            // encryption); read and write them as-is so non-modified fields round-trip byte-equal.
            TableEncryptionService.UseEncryption = false;

            using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWrite
            }.ToString());
            conn.Open();
            ApplyKey(conn);

            var rows = new List<BannerRow>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"SELECT rowid, Bytes FROM [{SchemaTable}]";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var rowid = reader.GetInt64(0);
                    var bytes = (byte[])reader[1];
                    var rec = ShopRecruitExcel.GetRootAsShopRecruitExcel(new ByteBuffer(bytes)).UnPack();
                    rows.Add(new BannerRow(rowid, rec.Id, rec.GachaBannerPath ?? string.Empty, rec.SalePeriodTo, bytes));
                }
            }

            var withArt = rows.Where(r => !string.IsNullOrEmpty(r.Art)).ToList();
            if (withArt.Count == 0)
            {
                logger.LogWarning("Client recruitment banners: no banner carries art to copy; skipping");
                return;
            }

            var now = DateTime.Now;
            // Only touch banners that can still be displayed (no end date / future / unparseable),
            // so we don't rewrite hundreds of expired historical rows.
            var targets = rows
                .Where(r => string.IsNullOrEmpty(r.Art))
                .Where(r => !IsExpired(r.SalePeriodTo, now))
                .ToList();

            if (targets.Count == 0)
            {
                logger.LogInformation("Client recruitment banners: no displayable empty-art rows to patch");
                return;
            }

            var patched = 0;
            using (var tx = conn.BeginTransaction())
            {
                foreach (var target in targets)
                {
                    var art = PickRelatedArt(target.Id, withArt);
                    var rec = ShopRecruitExcel.GetRootAsShopRecruitExcel(new ByteBuffer(target.Bytes)).UnPack();
                    rec.GachaBannerPath = art;

                    var fbb = new FlatBufferBuilder(Math.Max(64, target.Bytes.Length + 64));
                    fbb.Finish(ShopRecruitExcel.Pack(fbb, rec).Value);
                    var newBytes = fbb.SizedByteArray();

                    using var upd = conn.CreateCommand();
                    upd.Transaction = tx;
                    upd.CommandText = $"UPDATE [{SchemaTable}] SET Bytes = @b WHERE rowid = @r";
                    upd.Parameters.Add("@b", SqliteType.Blob).Value = newBytes;
                    upd.Parameters.Add("@r", SqliteType.Integer).Value = target.RowId;
                    upd.ExecuteNonQuery();
                    patched++;

                    logger.LogInformation("Banner {Id}: empty art -> {Art}", target.Id, art);
                }
                tx.Commit();
            }

            logger.LogInformation("Patched {Count} empty recruitment banner art path(s) in client ExcelDB", patched);
        }

        // Reuse the art of the banner whose Id shares the longest leading-digit run -> same
        // event/family, so the blank slot shows that event's real banner image.
        private static string PickRelatedArt(long emptyId, List<BannerRow> withArt)
        {
            var key = emptyId.ToString();
            var best = withArt[0].Art;
            var bestScore = -1;
            foreach (var candidate in withArt)
            {
                var score = CommonPrefixLength(key, candidate.Id.ToString());
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate.Art;
                }
            }
            return best;
        }

        private static int CommonPrefixLength(string a, string b)
        {
            var n = Math.Min(a.Length, b.Length);
            var i = 0;
            while (i < n && a[i] == b[i]) i++;
            return i;
        }

        private static bool IsExpired(string salePeriodTo, DateTime now)
        {
            if (string.IsNullOrWhiteSpace(salePeriodTo))
                return false;
            return DateTime.TryParse(salePeriodTo, out var to) && to < now;
        }

        private static void ApplyKey(SqliteConnection conn)
        {
            var key = Environment.GetEnvironmentVariable("SHITTIM_EXCELDB_SQLCIPHER_KEY");
            if (string.IsNullOrWhiteSpace(key))
                key = Config.Instance.ServerConfiguration.ExcelDbSqlCipherKey;

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA key = \"x'{key.Trim()}'\";";
            cmd.ExecuteNonQuery();
        }

        private static string GetExcelDbPath()
        {
            var configured = Environment.GetEnvironmentVariable("SHITTIM_CLIENT_EXCELDB_PATH");
            if (string.IsNullOrWhiteSpace(configured))
                configured = Config.Instance.ServerConfiguration.ClientExcelDbPath;
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                return configured;

            const string relative = @"BlueArchive_Data\StreamingAssets\PUB\Resource\Preload\TableBundles\ExcelDB.db";

            // Derive from the configured client-metadata install root (same install tree).
            var metaPath = Config.Instance.ServerConfiguration.ClientMetadataPath;
            if (!string.IsNullOrWhiteSpace(metaPath))
            {
                var idx = metaPath.IndexOf(@"\BlueArchive_Data", StringComparison.OrdinalIgnoreCase);
                if (idx > 0)
                {
                    var candidate = Path.Combine(metaPath[..idx], relative);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            var fallback = Path.Combine(@"F:\SteamLibrary\steamapps\common\BlueArchive", relative);
            return File.Exists(fallback) ? fallback : string.Empty;
        }

        private readonly record struct BannerRow(long RowId, long Id, string Art, string SalePeriodTo, byte[] Bytes);
    }
}
