---
id: configuration
title: Configuration reference
---

`Config.json` sits in `Config/` next to the server executable and is generated on the first run. The Control Center's Configuration page edits the same file.

The file has three sections. `ServerConfiguration` is the one that matters; `Irc` and `DataFetcher` are small and rarely touched.

## ServerConfiguration

### Version

| Key | Default | Meaning |
| --- | --- | --- |
| `GameVersion` | `1.90.439170` | client version string |
| `AuthCurrentVersion` | `446690` | data build number reported as `Account_Auth.CurrentVersion`. The official server answers its latest build regardless of the client's |
| `OverrideVersionId` | null | pin the data version instead of resolving it |
| `OverrideCdnBaseUrl` | null | pin the CDN |
| `ServerInfoUrl` | a CloudFront URL | where the version resolver looks |
| `AutoCheckVersion` | true | resolve the current version on boot |
| `AutoUpdateVersion` | true | follow it when it moves |
| `AutoUpdateResources` | true | re-download Excel and HexaMap data when the version changes |

### Networking

| Key | Default | Meaning |
| --- | --- | --- |
| `HostAddress` | `127.0.0.1` | |
| `HostPort` | `5000` | admin API and SDK |
| `GatewayPort` | `5100` | protocol traffic |
| `EnableGateway` | true | |
| `GatewayRsaPrivateKeyPem` / `Path` | empty | the login handshake key; normally loaded from `Config/GatewayPrivateKey.pem` |
| `GatewayRsaPublicKeyPem` / `Path` | empty | the public half, patched into the client's metadata |

### Client

| Key | Default | Meaning |
| --- | --- | --- |
| `ClientInstallDirectory` | empty | the install everything else is derived from. Blank means find it |
| `AutoPatchClientMetadata` | true | gateway public key and region label in `global-metadata.dat` |
| `AutoPatchClientSteamOffline` | **false** | lets the client hand out an external ticket with Steam offline or not running; rewrites `GameAssembly.dll`, which Steam restores on a file verify |
| `AutoPatchClientGamescaleIas` | true | `gamescale.core.dll` |
| `AutoPatchClientInfaceConfig` | true | the inface config file |
| `AutoManageGrap64` | true | grap64 plugin |
| `AutoPatchClientBanners` | true | recruitment banners into the client's `ExcelDB.db` |
| `AutoPatchClientStoreUrl` | true | sends the client's Steam store lookup to this server, so the shop currency check still answers with no route out |
| `RegionDisplayText` | empty | title-screen region label; blank restores the stock name |
| `Client*Path` | empty | per-file overrides, only needed when a file is not where the install directory implies |

### Data and database

| Key | Default | Meaning |
| --- | --- | --- |
| `SQLProvider` | `SQLite3` | |
| `SQLConnectionString` | `Data Source=shittim.sqlite3` | |
| `UseCustomExcel` | false | read Excel tables from a local override folder |
| `ExcelDbSqlCipherKey` | a 64-hex key | decrypts `ExcelDB.db`. Rotates between some game updates; overridable with `SHITTIM_EXCELDB_SQLCIPHER_KEY` |
| `ExcelDbSqlCipherLicense` | a base64 string | SQLCipher Commercial Edition licence string shipped in the client. The Community build handles the same ciphers, so nothing here uses it |

### Behaviour

| Key | Default | Meaning |
| --- | --- | --- |
| `UseEncryption` | false | packet encryption |
| `BypassAuthentication` | false | |
| `SelectedAccountId` | 0 | when nonzero, every login is answered with this account regardless of which publisher identity connects. 0 disables |
| `KoyukiIncident` | false | fills every cafe with Koyuki and swaps the lobby banner list for a single webview banner |
| `WorldRaidCoordinatorUrl` | `https://raid.shittem-server.com` | shared world raid coordinator. Empty means the raid runs off the cached manifest with a purely local HP pool |
| `AdminApiKey` | empty | shared secret for `/api/admin`, sent as `X-Admin-Key`. Empty restricts the admin surface to loopback, which is enough for the Control Center. Overridable with `SHITTIM_ADMIN_API_KEY` |

### PacketLogging

| Key | Default |
| --- | --- |
| `RequestPacket` | true |
| `ResponsePacket` | false |
| `ErrorPacket` | false |
| `WireDump` | true |

Wire dumping writes request and response bytes to `logs/wire-<date>.txt` in the same format as a packet capture, with the session key length and AES state alongside. It costs one buffered append per request and is the only way to compare a fault against a capture, which is why it defaults on.

## Environment variables

| Variable | Overrides |
| --- | --- |
| `SHITTIM_ADMIN_API_KEY` | `AdminApiKey` |
| `SHITTIM_EXCELDB_SQLCIPHER_KEY` | `ExcelDbSqlCipherKey` |
| `SHITTIM_CLIENT_EXCELDB_PATH` | the client's `ExcelDB.db` location |

## Other files

| File | Where | What |
| --- | --- | --- |
| `gacha_config.json` | one level above the build directory | rate overrides and the guaranteed pickup, hot-reloaded |
| the server notice | next to the build | notification flags and the login gate |
| the event schedule override | next to the build | which events are forced open |
| `worldraid_manifest.json` | next to the build | the cached world raid schedule |
| `Data/Mods/characters.json` | next to the build | the custom character registry |
