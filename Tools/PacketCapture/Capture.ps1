<#
.SYNOPSIS
  Runs the Blue Archive decrypted-response capture.

.DESCRIPTION
  Wraps frida so you do not have to remember flags. Two useful shapes:

    Attach  - hooks a client that is already running. Fast, but anything that
              happened before you attached (the login handshake, usually) is
              already gone.

    Spawn   - starts the client under frida and installs hooks before its first
              network call, so the login and gateway handshake are captured.
              This is the one you want when chasing the session-key exchange.

  Discovery mode installs no hooks at all. It walks the live IL2CPP metadata
  and writes every networking/crypto/serialization method it can see, so you
  can pick precise targets instead of guessing.

.EXAMPLE
  .\Capture.ps1 -Discover
  .\Capture.ps1 -Spawn
  .\Capture.ps1
#>
[CmdletBinding()]
param(
  [switch]$Spawn,
  [switch]$Discover,
  [string]$GamePath = 'F:\SteamLibrary\steamapps\common\BlueArchive\BlueArchive.exe',
  [string]$Python   = 'F:\Python313\python.exe'
)

$ErrorActionPreference = 'Stop'
$script  = Join-Path $PSScriptRoot 'ba-capture.js'
$capDir  = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'captures'

if (-not (Test-Path $script)) { throw "missing $script" }
New-Item -ItemType Directory -Force -Path $capDir | Out-Null

if ($Discover) { $env:BA_MODE = 'discover' } else { $env:BA_MODE = 'capture' }

Write-Host "mode     : $env:BA_MODE"
Write-Host "script   : $script"
Write-Host "captures : $capDir"
Write-Host ''

# frida needs to inject into the game, which means matching elevation.
$admin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
         ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) {
  Write-Warning 'Not elevated. If injection fails with "permission denied", rerun this in an admin shell.'
}

if ($Spawn) {
  if (-not (Test-Path $GamePath)) { throw "game not found: $GamePath" }
  Write-Host "spawning $GamePath" -ForegroundColor Cyan
  Write-Host 'hooks install before the first network call; %resume once you see the hook list.' -ForegroundColor DarkGray
  & $Python -m frida -f $GamePath -l $script --runtime=v8
}
else {
  $proc = Get-Process BlueArchive -ErrorAction SilentlyContinue
  if (-not $proc) {
    Write-Warning 'BlueArchive.exe is not running.'
    Write-Host   'Start the game first, or use -Spawn to catch the login handshake.'
    return
  }
  Write-Host "attaching to BlueArchive.exe (PID $($proc.Id))" -ForegroundColor Cyan
  & $Python -m frida -n BlueArchive.exe -l $script --runtime=v8
}
