---
id: installation
title: Installation
---

Grab Shittim Control Center from the [releases page](https://github.com/Neoexm/Shittim-Server/releases). There are two builds: a portable exe that runs from wherever you put it, and an installer. Both self-update from GitHub Releases, but only the installed build can apply an update in place - the portable one tells you to download the new exe.

Run it as administrator. The certificate step and offline mode both need it.

## First launch

The Control Center needs a copy of the server source before it can do anything. On first launch it offers to download one into your Documents folder, or to point at a folder you already have. That folder becomes the project root, and everything else is derived from it:

| Path | Where it lands |
| --- | --- |
| Server project | `<root>/Shittim-Server` |
| Built executable | `<root>/Shittim-Server/bin/Debug/net10.0/Shittim-Server.exe` |
| Configuration | `<root>/Shittim-Server/bin/Debug/net10.0/Config/Config.json` |
| Database | `<root>/Shittim-Server/shittim.sqlite3` |
| Gacha overrides | `<root>/Shittim-Server/bin/gacha_config.json` |
| Proxy scripts | `<root>/Scripts/redirect_server_mitmproxy` |

A Release build is used instead if there is no Debug one. If you move or rename the project folder later, the Control Center keeps pointing at the old path and flags it as missing rather than quietly falling back somewhere else - a drive that is not plugged in should not look like a fresh install.

## Environment setup

Open the Overview page. The readiness card lists every prerequisite with a state and a path. Anything not ready gets an Install button, and **Install missing** does the whole list in one go:

- **.NET SDK** downloads and runs the official installer.
- **mitmproxy** downloads and runs its installer, then finds `mitmweb.exe` under the install root or on PATH. A pip install under `Scripts/` and a Program Files install are both normal and are not the same program, so the check reports which one it found.
- **CA certificate** runs mitmproxy once to generate the root certificate and trusts it in the machine store.

Re-check after each step. When the card is clean you are ready to start.

## Starting

The power button in the status bar starts the server and the proxy together. Wait for the console dock to report that the server is listening, then launch Blue Archive from Steam as normal.

The server does the rest on its own: it resolves the current data version, downloads the Excel tables if they have changed, patches the client, and starts listening on the API port (5000 by default) and the gateway port (5100).

## Offline mode

The Overview page also has an offline start. It brings the server and the proxy up with their offline switches on and points every hostname the client contacts at loopback, so nothing it asks for needs a name server or a route out. Stopping the server puts the hosts file back, and there is a **Restore hosts file** button if something goes wrong and it does not.

Steam still has to be running.
