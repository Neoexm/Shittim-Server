$baseDir = $PSScriptRoot

$dotnetVersions = dotnet --list-sdks | Select-String "^[6-9]|^1[0-9]" 2>&1
$hasDotnet = -not [System.String]::IsNullOrEmpty($dotnetVersions)
if ($LASTEXITCODE -ne 0 -or -not $hasDotnet) {
    Write-Host "Error: .NET SDK 6+ is not installed or not found in PATH" -ForegroundColor Red
    Write-Host "Please install .NET SDK from https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    exit 1
}

$mitmwebVersion = mitmweb --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: mitmweb is not installed or not found in PATH" -ForegroundColor Red
    Write-Host "Please install mitmproxy tools from https://mitmproxy.org" -ForegroundColor Yellow
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    exit 1
}

Write-Host "Starting Shittim Server..." -ForegroundColor Cyan
Start-Sleep -Seconds 1

Write-Host "Starting game server..." -ForegroundColor Green
Start-Process cmd -ArgumentList "/k", "cd $baseDir\Shittim-Server && dotnet run --project Shittim-Server.csproj" -WindowStyle Normal

Start-Sleep -Seconds 2

Write-Host "Starting mitmproxy..." -ForegroundColor Green
Start-Process cmd -ArgumentList "/k", "cd $baseDir\Scripts\redirect_server_mitmproxy && mitmweb -m wireguard --no-http2 -s redirect_server.py --set termlog_verbosity=warn --mode local:BlueArchive.exe" -WindowStyle Normal

Write-Host ""
Write-Host "Server started successfully!" -ForegroundColor Green
Write-Host "- Game server will be available at http://localhost:5000" -ForegroundColor Yellow
Write-Host "- Mitmproxy web interface at http://127.0.0.1:8081" -ForegroundColor Yellow
Write-Host ""
Write-Host "Launch Blue Archive to connect." -ForegroundColor Cyan