# Shittim Server

A functional Blue Archive private server implementation in C# with ASP.NET Core 6.0.

## What it does

- Handles authentication and account management
- Supports core game protocols with MX packet encryption/decryption
- SQLite database for persistence
- HAR logging for traffic analysis

## Requirements

- .NET 6.0 SDK or later
- Blue Archive (Steam version)
- Python 3.8+ and [mitmproxy](https://mitmproxy.org/) (including `mitmweb`) installed and available on `PATH`

## How to run

## Currently, the server is not working with the latest version, pleae follow the stpes below

1. Download [Steam Depot Downloader](https://github.com/SteamRE/DepotDownloader)
2. extract the zip and, in command prompt navigate to the folder and run `-app 3557620 -depot 3557621 -manifest 368323032969658707`
3. Make a backup of your current blue archive install, then copy the files from the depot and paste them where your existing blue archive install was
4. replace grap64.dll with the one in /scripts
5. run the server and run the game


### 1. Patch Blue Archive with the custom DLL

1. Close Blue Archive and Steam.
2. Find your Blue Archive install folder, e.g.  
   `C:\Program Files (x86)\Steam\steamapps\common\Blue Archive`
3. Go to:  
   `BlueArchive_Data\Plugins\x86_64`
4. **Back up** the original `grap64.dll` somewhere safe.
5. Copy the patched `grap64.dll` from this repository’s `Scripts` folder into  
   `BlueArchive_Data\Plugins\x86_64`, **overwriting** the original file.

### 2. Install the mitmproxy root certificate (Windows, via mitm.it)

You only need to do this once per machine.

1. Install mitmproxy from the official site and ensure `mitmweb` runs in a terminal.
2. Start mitmproxy:

```powershell
mitmweb
```

By default it listens on `127.0.0.1:8080`.

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

7. When asked _“Store Location”_, choose **Local Machine** (not _Current User_), then click **Next**.

8. Select **“Place all certificates in the following store”**, click **Browse…**, and choose:

   - **Trusted Root Certification Authorities**

9. Finish the wizard and confirm the security warning.

This ensures the mitmproxy CA is installed into the **machine** root store, which is what the Steam version of Blue Archive will actually use.

You can now revert your system proxy settings if you want; the certificate stays installed.

### 3. Start the server and mitmproxy wrapper

From a PowerShell window in the repo folder:

```powershell
cd Shittim-Server
.\autorun.ps1
```

This script will:

- Start the ASP.NET Core game server (`http://localhost:5000`)
- Start `mitmweb` in the correct mode to hook `BlueArchive.exe`

### 4. Launch Blue Archive

Start Blue Archive from Steam.
If the DLL patch and certificate install were done correctly, the game should connect to Shittim Server instead of Nexon.

## Disclaimer

For educational and research purposes only. Not affiliated with Nexon.
