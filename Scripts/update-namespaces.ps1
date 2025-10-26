<#
.SYNOPSIS
    Updates Plana namespace references to Schale in application code.

.DESCRIPTION
    This script replaces all instances of Plana namespaces with Schale namespaces
    in C# source files. Excludes plana/, packages/, bin/, and obj/ directories.

.PARAMETER DryRun
    When specified, shows what would be changed without modifying files.

.PARAMETER Exclude
    Additional file patterns to exclude (comma-separated).

.EXAMPLE
    .\update-namespaces.ps1 -DryRun
    Shows changes without modifying files.

.EXAMPLE
    .\update-namespaces.ps1
    Applies namespace changes to all application code.

.EXAMPLE
    .\update-namespaces.ps1 -Exclude "Legacy.cs,Old.cs"
    Excludes specific files from updates.
#>

param(
    [switch]$DryRun,
    [string]$Exclude = ""
)

$ErrorActionPreference = "Stop"

$excludePatterns = @(
    "plana",
    "Plana",
    "packages",
    "bin",
    "obj",
    "node_modules",
    ".git"
)

if ($Exclude) {
    $excludePatterns += $Exclude.Split(",")
}

function Test-ShouldExclude {
    param([string]$Path)
    
    foreach ($pattern in $excludePatterns) {
        if ($Path -like "*$pattern*") {
            return $true
        }
    }
    return $false
}

function Update-Namespaces {
    param(
        [string]$FilePath,
        [bool]$Apply
    )
    
    $content = Get-Content -Path $FilePath -Raw
    $originalContent = $content
    
    $content = $content -replace '\bPlana\.', 'Schale.'
    $content = $content -replace '\bPlana([A-Za-z0-9_]+)', 'Schale$1'
    
    if ($content -ne $originalContent) {
        Write-Host "Modified: $FilePath" -ForegroundColor Yellow
        
        if ($Apply) {
            Set-Content -Path $FilePath -Value $content -NoNewline
            Write-Host "  Applied changes" -ForegroundColor Green
        } else {
            $diff = Compare-Object -ReferenceObject $originalContent.Split("`n") `
                                   -DifferenceObject $content.Split("`n")
            if ($diff) {
                Write-Host "  Preview (first 5 changes):" -ForegroundColor Cyan
                $diff | Select-Object -First 5 | ForEach-Object {
                    if ($_.SideIndicator -eq "=>") {
                        Write-Host "    + $($_.InputObject)" -ForegroundColor Green
                    } else {
                        Write-Host "    - $($_.InputObject)" -ForegroundColor Red
                    }
                }
            }
        }
        return $true
    }
    return $false
}

Write-Host "Schale Namespace Migration Tool" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No files will be modified" -ForegroundColor Yellow
    Write-Host ""
}

$csFiles = Get-ChildItem -Path . -Filter *.cs -Recurse -File

$modifiedCount = 0
$scannedCount = 0

foreach ($file in $csFiles) {
    if (Test-ShouldExclude -Path $file.FullName) {
        continue
    }
    
    $scannedCount++
    $modified = Update-Namespaces -FilePath $file.FullName -Apply (-not $DryRun)
    
    if ($modified) {
        $modifiedCount++
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Files scanned: $scannedCount"
Write-Host "  Files modified: $modifiedCount" -ForegroundColor $(if ($modifiedCount -gt 0) { "Yellow" } else { "Green" })

if ($DryRun -and $modifiedCount -gt 0) {
    Write-Host ""
    Write-Host "Run without -DryRun to apply changes" -ForegroundColor Yellow
}

if (-not $DryRun -and $modifiedCount -gt 0) {
    Write-Host ""
    Write-Host "Changes applied successfully!" -ForegroundColor Green
}