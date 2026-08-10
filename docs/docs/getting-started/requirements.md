---
id: requirements
title: Requirements
---

## What you need

- **Windows.** The server itself builds and runs anywhere .NET 10 does, but the client patchers, the Control Center's installers and the offline hosts handling are all written against Windows, and the game is a Windows build.
- **Blue Archive installed from Steam.** The server patches the retail install in place. It finds the install by asking Steam's own library folders first, then the conventional locations. If you have two installs, or the game is somewhere Steam does not know about, point `ClientInstallDirectory` at the right one from the Configuration page.
- **Steam running.** Even in Steam's own offline mode, the client reads the SDK version back before it will boot at all.
- **Administrator rights.** The proxy writes a CA certificate into the machine store, and offline mode edits the hosts file.
- **About 3 GB free** for the server, its resource downloads and the .NET SDK if you do not already have it.

## What the Control Center installs for you

The Overview page runs an environment check and offers to fix anything it finds missing. Nothing on this list needs to be installed by hand.

| Check | What it is |
| --- | --- |
| .NET SDK | .NET 10, needed to build and run the server |
| Server build | the compiled server, or the source waiting to be built |
| Game database | `shittim.sqlite3`, created on the first server run |
| mitmproxy | the redirecting proxy |
| CA certificate | mitmproxy's root certificate, trusted in the machine store |
| Gateway keys | the RSA pair used for the login handshake, shipped with the server |
| Redirect script | `Scripts/redirect_server_mitmproxy/redirect_server.py` |

The gateway keys are shipped rather than generated. They are copied next to the server executable by the project file, so a clean rebuild that wipes `bin/Config` takes the private key with it. Without it the login handshake cannot be decrypted, and the client shows that as a hang on "Unpacking game resources" rather than as anything to do with keys. If the Overview page reports the gateway keys missing, restore `Shittim-Server/config/*.pem`.

## Accounts

You do not need a Nexon account and you never touch the official servers. The server answers whatever publisher identity the client presents, and you can create as many accounts as you like from the Accounts page and pick which one the game logs into.
