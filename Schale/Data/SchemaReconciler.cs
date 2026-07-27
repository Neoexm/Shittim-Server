using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Schale.Data
{
    /// <summary>
    /// Brings an existing SQLite database up to date with the EF model without migrations.
    ///
    /// The server uses <c>EnsureCreated()</c>, which builds the full schema for a brand-new
    /// database and then does nothing at all for one that already exists. Every entity added after
    /// a player's database was first created therefore needed a hand-written
    /// <c>CREATE TABLE IF NOT EXISTS</c>, and — worse — a new *column* on an existing entity had no
    /// equivalent escape hatch: it simply went missing and every read of it failed at runtime.
    ///
    /// This walks the model instead: missing tables are created from EF's own generated script, and
    /// missing columns are added with <c>ALTER TABLE ... ADD COLUMN</c>. It is idempotent, so it can
    /// run on every startup. Column *type* changes, drops and renames are out of scope — SQLite
    /// cannot do them in place, and they need a considered data migration rather than a guess.
    /// </summary>
    public static class SchemaReconciler
    {
        public static async Task<IReadOnlyList<string>> ReconcileAsync(SchaleDataContext context)
        {
            var applied = new List<string>();

            if (!context.Database.IsSqlite())
                return applied;

            var existingTables = await QueryNamesAsync(context,
                "SELECT name FROM sqlite_master WHERE type = 'table'");

            foreach (var entity in context.Model.GetEntityTypes())
            {
                var table = entity.GetTableName();
                if (string.IsNullOrEmpty(table))
                    continue;

                if (!existingTables.Contains(table))
                {
                    // A table EnsureCreated never got the chance to build. EF's create script covers
                    // the whole model, so filter it down to just this table's statement.
                    foreach (var statement in CreateStatementsFor(context, table))
                    {
                        await context.Database.ExecuteSqlRawAsync(statement);
                        applied.Add($"created table {table}");
                    }

                    existingTables.Add(table);
                    continue;
                }

                var existingColumns = await QueryNamesAsync(context,
                    $"SELECT name FROM pragma_table_info('{table.Replace("'", "''")}')");

                // Column names have to be resolved against this specific table, not taken from the
                // property's default name. Owned entities (OwnsOne) are separate entity types that
                // share the owner's table via table splitting, and their columns carry the navigation
                // as a prefix — WorldBossDB.HP is stored as "WorldBossDB_HP". The parameterless
                // GetColumnName() returns the unprefixed default, which reads as a missing column and
                // would have had the reconciler try to ALTER TABLE ADD a column that already exists.
                var storeObject = StoreObjectIdentifier.Table(table, entity.GetSchema());

                foreach (var property in entity.GetProperties())
                {
                    var column = property.GetColumnName(storeObject);
                    if (string.IsNullOrEmpty(column) || existingColumns.Contains(column))
                        continue;

                    // SQLite refuses a NOT NULL column without a default on a table that may
                    // already hold rows, so required columns get a zero-value default.
                    var nullability = property.IsNullable
                        ? "NULL"
                        : $"NOT NULL DEFAULT {ZeroLiteralFor(property)}";

                    await context.Database.ExecuteSqlRawAsync(
                        $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {property.GetColumnType()} {nullability}");

                    applied.Add($"added column {table}.{column}");
                }
            }

            return applied;
        }

        private static IEnumerable<string> CreateStatementsFor(SchaleDataContext context, string table)
        {
            var script = context.Database.GenerateCreateScript();

            foreach (var statement in script.Split(';'))
            {
                var trimmed = statement.Trim();
                if (trimmed.Length == 0)
                    continue;

                // Match `CREATE TABLE "Foo" (` / `CREATE TABLE Foo (` for this table only, and skip
                // the index statements that follow it (they are recreated below by name match).
                if (trimmed.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase)
                    && NamesTable(trimmed, table))
                {
                    yield return trimmed;
                }
                else if (trimmed.StartsWith("CREATE", StringComparison.OrdinalIgnoreCase)
                    && trimmed.Contains("INDEX", StringComparison.OrdinalIgnoreCase)
                    && trimmed.Contains($"\"{table}\"", StringComparison.Ordinal))
                {
                    yield return trimmed;
                }
            }
        }

        private static bool NamesTable(string createStatement, string table)
        {
            var afterKeyword = createStatement["CREATE TABLE".Length..].TrimStart();
            var name = afterKeyword.StartsWith('"')
                ? afterKeyword[1..afterKeyword.IndexOf('"', 1)]
                : new string(afterKeyword.TakeWhile(c => !char.IsWhiteSpace(c) && c != '(').ToArray());

            return string.Equals(name, table, StringComparison.Ordinal);
        }

        private static string ZeroLiteralFor(IProperty property)
        {
            var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
                return "'0001-01-01 00:00:00'";
            if (type == typeof(string))
                return "''";
            if (type == typeof(byte[]))
                return "x''";
            if (type == typeof(Guid))
                return "'00000000-0000-0000-0000-000000000000'";
            if (type == typeof(bool) || type.IsEnum || type.IsPrimitive || type == typeof(decimal))
                return "0";

            // Collections and owned/JSON-converted values serialize to TEXT.
            return "''";
        }

        private static async Task<HashSet<string>> QueryNamesAsync(SchaleDataContext context, string sql)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            var wasClosed = command.Connection!.State != System.Data.ConnectionState.Open;
            if (wasClosed)
                await command.Connection.OpenAsync();

            try
            {
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    names.Add(reader.GetString(0));
            }
            finally
            {
                if (wasClosed)
                    await command.Connection.CloseAsync();
            }

            return names;
        }
    }
}
