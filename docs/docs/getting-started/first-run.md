---
id: first-run
title: First run
---

## What happens when the server starts

In order:

1. `Config.json` is loaded, along with the saved server notice, the event schedule override and the cached world raid manifest.
2. The version resolver asks the CDN which data version is current, unless you have set an override.
3. Console commands are registered.
4. The resource service checks the Excel tables and downloads them if the version moved.
5. The ExcelDB SQLCipher key is validated. The key rotates between some game updates, and failing here with a clear message beats failing later with a corrupt-table error.
6. Kestrel binds the API port, the gateway port, and 443 if a certificate is present for the SDK endpoints.
7. The database is created or migrated, and a schema reconciler adds any tables and columns the EF model has grown since.
8. The client patchers run as hosted services: metadata, IAS in three native modules, the inface config, grap64, recruitment banners, the region label and the store URL.
9. Any event schedule override is re-applied, because a client update replaces `ExcelDB.db` and takes the forced dates with it.

The first run takes noticeably longer than the ones after it - the Excel tables are a few hundred megabytes and get downloaded and dumped.

## First launch of the game

Launch Blue Archive from Steam. You should land on the title screen with the region label the server set, then straight into the lobby - there is no Nexon login to get through, because the server answers the SDK endpoints itself.

If the client hangs on "Unpacking game resources", the gateway private key is missing from `bin/Debug/net10.0/Config`. See [Requirements](requirements.md).

If you get a popup saying a request cannot be processed, that is the client's rendering of an unhandled server exception. The server log will have the stack trace, and `logs/wire-<date>.txt` has the request and response bytes if wire dumping is on.

## Making an account

Open the Accounts page in the Control Center. **Create** makes a new account; the dropdown at the top picks which account the game logs into from its next launch, regardless of which Steam identity connects. Leaving it unset makes the game follow the Steam account again.

Nothing about the account is tied to Steam, so you can run as many as you like from one install.

## Giving yourself things

The Inventory page has bulk grants (all items, all equipment, all characters, max all characters) and per-item grants with a searchable picker. The Mail page can send anything as an attachment instead, which is the closer match to how the game normally hands things out.

Everything on both pages runs against the account selected in the top bar, not necessarily the one the game is logged into.
