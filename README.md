# Shittim Server

A functional Blue Archive private server implemented in C# on ASP.NET Core (.NET 10).

Official Discord — https://discord.gg/JNp6SUhrk2

## What it does

- Handles authentication and account management (Nexon / IAS / IMS login flow)
- Implements the core MX game protocols with packet encryption/decryption
- SQLite database for persistence
- HAR logging for traffic analysis

## Requirements

- .NET 10 SDK
- Blue Archive (Steam version)
- Python 3.8+ and [mitmproxy](https://mitmproxy.org/) (including `mitmweb`) installed and available on `PATH`

## Quick start

1. Install the mitmproxy root certificate — once per machine, see [One-time setup](#one-time-setup).
2. From the repository root, run the launcher:

   ```powershell
   .\autorun.ps1
   ```

3. Launch Blue Archive from Steam. It connects to Shittim Server instead of Nexon, and you can log in.

The rest of this document explains each step in detail.

## One-time setup

### Install the mitmproxy root certificate (Windows, via mitm.it)

You only need to do this once per machine.

1. Install mitmproxy from the official site and ensure `mitmweb` runs in a terminal.
2. Start mitmproxy:

   ```powershell
   mitmweb
   ```

   By default the proxy listens on `127.0.0.1:8080`.

3. Temporarily configure your Windows HTTP/HTTPS proxy to use mitmproxy:

   - Open **Settings → Network & Internet → Proxy**
   - Enable **Use a proxy server**
   - Address: `127.0.0.1`
     Port: `8080`

4. Open a browser on the same machine and visit:

   ```
   http://mitm.it
   ```

5. Click the **Windows** icon and download the certificate file.

6. Double-click the downloaded certificate to open the **Certificate Import Wizard**.

7. When asked _"Store Location"_, choose **Local Machine** (not _Current User_), then click **Next**.

8. Select **"Place all certificates in the following store"**, click **Browse…**, and choose:

   - **Trusted Root Certification Authorities**

9. Finish the wizard and confirm the security warning.

This installs the mitmproxy CA into the **machine** root store, which is what the Steam version of Blue Archive actually uses. You can now revert your system proxy settings; the certificate stays installed, and `autorun.ps1` hooks the game directly (it does not rely on the system proxy).


## Running the server

From the repository root:

```powershell
.\autorun.ps1
```

This launcher:

- Verifies the .NET SDK and `mitmweb` are installed (and exits with instructions if either is missing)
- Starts the ASP.NET Core game server at `http://localhost:5000`
- Starts `mitmweb` with the redirect addon, hooking `BlueArchive.exe` directly (web UI at `http://127.0.0.1:8081`)

## Launch Blue Archive

With the server and proxy running, start Blue Archive from Steam. If the certificate is installed and the server is up, the game connects to Shittim Server and you can log in to the private server.

## Disclaimer

For educational and research purposes only. Not affiliated with Nexon.
