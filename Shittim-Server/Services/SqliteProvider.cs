using SQLitePCL;

namespace BlueArchiveAPI.Services
{
    internal static class SqliteProvider
    {
        private static int initialized;

        public static void EnsureInitialized()
        {
            if (Interlocked.Exchange(ref initialized, 1) == 1)
                return;

            raw.SetProvider(new SQLite3Provider_e_sqlcipher());
            raw.FreezeProvider();
        }
    }
}
