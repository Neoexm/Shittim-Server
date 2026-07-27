@echo off
REM ==========================================================================
REM  Blue Archive Network Logger - one-click patcher
REM  Logs every request/response to  <GameDir>\DiagnosticLogs\NetworkLog.txt
REM
REM  BEFORE running: in Steam, Verify integrity of game files, then CLOSE the game.
REM ==========================================================================
cd /d "%~dp0"

tasklist /FI "IMAGENAME eq BlueArchive.exe" 2>NUL | find /I "BlueArchive.exe" >NUL
if not errorlevel 1 (
    echo [!] Blue Archive is running. Close the game completely, then run this again.
    echo.
    pause
    exit /b 1
)

where py >NUL 2>&1
if not errorlevel 1 (
    py apply_netlog.py %*
) else (
    python apply_netlog.py %*
)

echo.
pause
