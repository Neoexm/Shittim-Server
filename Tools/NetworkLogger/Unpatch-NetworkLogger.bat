@echo off
REM Restore the clean GameAssembly.dll (remove the network logger).
cd /d "%~dp0"
where py >NUL 2>&1
if not errorlevel 1 ( py apply_netlog.py revert ) else ( python apply_netlog.py revert )
echo.
pause
