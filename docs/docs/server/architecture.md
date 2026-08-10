---
id: architecture
title: Architecture
---

The server is a single ASP.NET Core application on .NET 10. Everything - the game protocol, the SDK stand-in, the admin API, the client patchers - runs in one process.

## Ports

| Port | What listens |
| --- | --- |
| 5000 | admin API, dev and GM controllers, SDK endpoints, `/health` |
| 5100 | the gateway the client sends protocol traffic to |
| 443 | SDK endpoints over HTTPS, only when a certificate is present in `certs/` |

Both HTTP ports are configurable. If the gateway port is set to the same value as the API port, only one listener is bound.

## Layers

**Controllers** (`Shittim-Server/Controllers`) are ordinary MVC controllers.

- root: the admin surface (`AdminController`, `ManagementController`, `ModsController`), the banner asset route and the admin auth attribute
- `Api/`: the gateway itself and the server info endpoint
- `SDK/`: everything that stands in for Nexon's login stack - IAS, inface, the toy SDK, stamps, config, prohibited words
- `GM/` and `Dev/`: ad-hoc endpoints used during development

**Protocol handlers** (`Shittim-Server/Core/NetworkProtocol/Handlers`) are 65 classes covering the game's own protocols. See [Protocol handling](protocol.md).

**Services** (`Shittim-Server/Services`) are where the game logic lives: parcels, gacha, cafe, missions, raids, campaign, shop, and the eight client patchers that run as hosted services.

**Schale** is a separate project holding the game models, the FlatBuffers-generated data types, the crypto, and the AutoMapper profiles that map between database entities and wire models.

## Startup order

1. Console hardening (disables QuickEdit on Windows and replaces `Console.Out` with a non-blocking writer, so a saturated pipe buffer can never block a request thread).
2. `Config.Load()`, then the saved server notice, the event schedule override and the cached world raid manifest.
3. The version resolver decides the data version and CDN base URL.
4. Console commands are discovered by reflection.
5. Excel resources are checked and downloaded if the version moved.
6. The ExcelDB SQLCipher key is validated against the current `ExcelDB.db`.
7. DI registration, Kestrel binding, and the app is built.
8. Protocol handlers are discovered and registered.
9. The database is created or migrated, and the schema reconciler adds any tables and columns the EF model has grown. `EnsureCreated` only builds the schema for a brand-new database, and the project has no migrations, so the reconciler derives both tables and columns from the model - adding a property to an entity is enough.
10. Account initialization, then the event schedule is re-applied.
11. `app.Run()`.

## Storage

Progress lives in SQLite (`shittim.sqlite3` by default) through EF Core. Game data - characters, stages, items, events, everything static - lives in `ExcelDB.db`, a SQLCipher-encrypted database shipped with the client. See [Excel data](excel-data.md).

A handful of things live in JSON files next to the build rather than in either database: the gacha rate override, the server notice, the event schedule override, the world raid manifest, and the custom character registry.

## Error handling

There is deliberately almost no try/catch in the handler layer. An unhandled exception in a handler surfaces to the client as error code 500, which the game renders as "A request that cannot be processed" - that is the intended behaviour, and the popup means "look at the server log", not "one specific bug".
