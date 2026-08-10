---
id: admin-api
title: Admin API
---

Everything under `/api/admin` on the API port. This is what the Control Center drives, and it is a plain JSON HTTP API you can use directly.

## Authentication

`[AdminAuth]` gates the whole surface:

- a correct `X-Admin-Key` header is accepted from any address
- **loopback is accepted with no header at all** - the Control Center connects to `http://127.0.0.1` and does not send one
- everything else gets 403

`AdminApiKey` is empty by default, which means loopback-only. Set it (or `SHITTIM_ADMIN_API_KEY`) and send it as `X-Admin-Key` to administer a server over the network. The comparison is fixed-time.

`app.UseAuthorization()` on its own does nothing here - no scheme is registered and no action carries `[Authorize]` - so this attribute is the only thing standing between the mail, currency, console-command and account-delete endpoints and anything that can reach the port.

## Status

| Method | Route | Notes |
| --- | --- | --- |
| GET | `/health` | not under `/api/admin` and not gated. Answers as soon as the port is bound |
| GET | `/api/admin/status` | hits the database, so a 200 means the server can actually serve |

The Control Center probes both, in that order, and only calls itself online when the second answers.

## Accounts

| Method | Route |
| --- | --- |
| GET | `/api/admin/accounts` |
| GET | `/api/admin/account/{serverId}/detail` |
| POST | `/api/admin/account/create` |
| POST | `/api/admin/account/update` |
| POST | `/api/admin/account/delete` |
| GET | `/api/admin/account/selected` |
| POST | `/api/admin/account/select` |
| GET | `/api/admin/account/{serverId}/currencies` |
| POST | `/api/admin/currency/set` |

`account/select` writes `SelectedAccountId` in the config, which forces every login to that account.

## Inventory

| Method | Route |
| --- | --- |
| GET | `/api/admin/account/{serverId}/items` |
| POST | `/api/admin/items/give` |
| POST | `/api/admin/items/remove` |
| GET | `/api/admin/account/{serverId}/characters` |

## Mail

| Method | Route |
| --- | --- |
| GET | `/api/admin/account/{serverId}/mails` |
| POST | `/api/admin/mail/send` |
| POST | `/api/admin/mail/delete` |

## Console commands

| Method | Route |
| --- | --- |
| POST | `/api/admin/command` |

Body is `{ uid, command }`. The command string is the same one the console takes, without the leading `!`. See [Console commands](commands.md).

## Static game data

Read-only lookups over the Excel tables, used to populate the pickers.

| Method | Route |
| --- | --- |
| GET | `/api/admin/static/items?limit=&search=` |
| GET | `/api/admin/static/characters?limit=&search=` |
| GET | `/api/admin/static/equipment?limit=&search=` |
| GET | `/api/admin/static/currencies` |
| GET | `/api/admin/meta/parceltypes` |

## Gacha

| Method | Route |
| --- | --- |
| GET | `/api/admin/gacha/config` |
| POST | `/api/admin/gacha/config` |
| GET | `/api/admin/gacha/banners` |

Banners are read-only. The config write lands in `gacha_config.json` and is picked up without a restart.

## Events and notices

| Method | Route |
| --- | --- |
| GET | `/api/admin/events/schedule` |
| POST | `/api/admin/events/schedule` |
| GET | `/api/admin/events/seasons?uid=` |
| GET | `/api/admin/events/{eventContentId}/unlocks?uid=` |
| POST | `/api/admin/events/unlock` |
| GET | `/api/admin/notice` |
| POST | `/api/admin/notice` |

## Mods

| Method | Route | Body |
| --- | --- | --- |
| GET | `/api/admin/mods/characters` | |
| POST | `/api/admin/mods/characters/inspect` | `{ zipPath }` |
| POST | `/api/admin/mods/characters/import` | `{ zipPath, donorId, id, name, overrides }` |
| GET | `/api/admin/mods/characters/{id}` | |
| POST | `/api/admin/mods/characters/{id}/update` | `{ name, character, profile, stat }` |
| POST | `/api/admin/mods/characters/{id}/remove` | |

`zipPath` is a path on the server's filesystem, which is fine because the server is on 127.0.0.1. Import rewrites three 300 MB databases and takes a backup of each the first time, so give it a long timeout - the Control Center allows five minutes.

Errors from this controller come back as `{ error: "..." }` with a 400.
