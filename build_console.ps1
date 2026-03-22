$ErrorActionPreference = "Stop"

$baseDir = $PSScriptRoot
Set-Location $baseDir

Write-Host "Building Shittim Console..." -ForegroundColor Cyan

python -m pip install --upgrade pyinstaller
if ($LASTEXITCODE -ne 0) {
    throw "Failed to install or upgrade PyInstaller"
}

python -m PyInstaller --noconfirm --clean --onefile --windowed --name "Shittim Console" .\shittim_console.py
if ($LASTEXITCODE -ne 0) {
    throw "Build failed"
}

Write-Host "Build completed. Portable executable available at .\dist\Shittim Console.exe" -ForegroundColor Green
