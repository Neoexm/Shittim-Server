---
id: excel-data
title: Excel data and FlatData
---

Everything static in Blue Archive - students, stages, items, skills, events, shops, dialogue - lives in `ExcelDB.db`, a SQLCipher-encrypted SQLite database shipped inside the client. It is about 300 MB and has around 900 tables.

## Opening it

The key is a 64-character hex string in `ExcelDbSqlCipherKey`, overridable with `SHITTIM_EXCELDB_SQLCIPHER_KEY`. It rotates between some game updates, which is why the server validates it at startup rather than letting a wrong key surface as a corrupt table later.

```csharp
SqliteProvider.EnsureInitialized();
TableEncryptionService.UseEncryption = false;

var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
conn.Open();

using var cmd = conn.CreateCommand();
cmd.CommandText = $"PRAGMA key = \"x'{key}'\";";
cmd.ExecuteNonQuery();
```

`TableEncryptionService.UseEncryption` must be off for `ExcelDB.db`. Strings inside these rows are plaintext; SQLCipher is the only layer of encryption. It is on for other table sources, which is why it is set explicitly rather than left alone.

## Table shape

Every table is named `<Something>DBSchema` and has the same shape: some denormalized indexed key columns, then a `Bytes` blob holding one FlatBuffers row.

There is **no primary key and no unique constraint** on any of them, only plain indexes. An insert that collides with an existing key does not replace it - it duplicates it, and both rows come back from a lookup. Anything writing to these tables has to check for itself.

## FlatData

`Schale/FlatData` is flatc-generated with the object API. For every table there is a type:

| Thing | Example |
| --- | --- |
| The table type | `CharacterExcel` |
| Its unpacked object form | `CharacterExcelT` |
| Reading a row | `CharacterExcel.GetRootAsCharacterExcel(new ByteBuffer(bytes)).UnPack()` |
| Writing one | `CharacterExcel.Pack(builder, obj)`, then `builder.Finish(offset.Value)` |

Type name to table name is mechanical: `CharacterExcel` becomes `CharacterDBSchema`, `LocalizeEtcExcel` becomes `LocalizeEtcDBSchema`.

A repacked row is rarely byte-identical to the original even when nothing changed, because vtable layout and string deduplication differ between the client's writer and flatc's. That is expected and harmless.

The generated files are excluded from the repo's style rules; do not hand-edit them.

## Regenerating

The FlatData models are rebuildable from any installed client: dump the IL2CPP metadata, derive the `.fbs`, run flatc, and post-process the crypto attributes. That reproduces the checked-in files essentially byte for byte.

`BlueArchive.fbs` in the tree is stale and must not be used as the source for a regeneration.

## Schema drift

Field order in the client's schema moves between versions, and a table read at the wrong offsets decodes into plausible-looking nonsense rather than failing. The server handles this by reading the installed client's `global-metadata.dat` to find where each field actually sits now and re-reading drifted tables at those offsets.

The client's reader properties are not all schema fields, so aligning on property names alone invents drift where there is none. Gate on row-byte evidence, never on the client's property order.

## The three copies

There are normally three copies of `ExcelDB.db` on a machine, and they have to agree:

| Copy | Role |
| --- | --- |
| `Resources/Dumped/ExcelDB.db` | what the server reads |
| `Resources/Downloaded/ExcelDB.db` | what the resource loader restores from |
| `BlueArchive_Data/StreamingAssets/PUB/Resource/Preload/TableBundles/ExcelDB.db` | what the client reads |

A change written to fewer than all three drifts back the next time one is copied over another. The banner patcher, the event schedule and the custom character importer all write to every copy they can find.

The client's copy is locked while the game is running, so anything writing to it needs Blue Archive closed.
