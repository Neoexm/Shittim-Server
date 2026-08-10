---
id: configuration
title: Configuration
---

Reads and writes the server's `Config.json` directly. If the file does not exist yet the page says so and offers to open the folder - it is generated the first time the server runs.

Most changes need a server restart. The page says so when you save.

## Networking

| Field | Default | Notes |
| --- | --- | --- |
| API port | 5000 | admin API, SDK endpoints, health |
| Gateway port | 5100 | protocol traffic from the client |
| Enable gateway | on | |

## Behaviour

| Field | Notes |
| --- | --- |
| Packet encryption | off |
| Bypass authentication | off |
| Custom Excel tables | reads tables from a local override folder instead of the downloaded set |
| Koyuki incident | fills every cafe with Koyuki and replaces the lobby banner list with a single webview banner |
| Auto-check version | resolve the latest data version on boot |
| Auto-update version | follow it when it moves |
| Auto-update resources | re-download the Excel and HexaMap data when the version changes |

## Database

Provider and connection string. The default is SQLite with `Data Source=shittim.sqlite3`, resolved relative to the working directory the server runs from.

## Version and data sources

Override the version id and the CDN base URL to pin the server to a specific data build, and set the server info URL it resolves from. Blank means resolve automatically.

## Client auto-patching

**Game install directory** is the install everything else is derived from. Blank means find it - Steam's own libraries first, then the conventional locations. Set it when there are two installs, or when the game is somewhere Steam does not know about. The per-file overrides underneath are only needed when one file lives somewhere else.

| Switch | What it patches |
| --- | --- |
| Patch metadata | the gateway public key and the region label in `global-metadata.dat` |
| Patch GameAssembly IAS | off by default; rewrites `GameAssembly.dll`, which Steam restores on a file verify |
| Patch gamescale.core IAS | the login endpoint in `gamescale.core.dll` |
| Patch Nexon platform IAS | the same in the Nexon platform modules |
| Patch inface IAS | the same in inface |
| Patch inface config | the inface configuration file |
| Manage grap64 | grap64 plugin management |
| Patch recruitment banners | writes banners into the client's `ExcelDB.db` |
| Region label | the title-screen region name, blank for the stock one |

There is also a Steam offline patch, off by default and not exposed on this page, that lets the client hand out an external ticket with Steam offline or not running.

## Packet logging

Requests, responses and errors, each independently. There is a fourth switch, wire dumping, that is on by default and not on this page: it writes the raw request and response bytes to `logs/wire-<date>.txt`. The client reports protocol faults as opaque popups, and without the response bytes there is nothing to compare against a capture.

## Reset

**Reset** puts the editable fields back to their defaults. It leaves the game version, the gateway keys, the plugin directory and the IRC and data-fetcher sections of `Config.json` alone.
