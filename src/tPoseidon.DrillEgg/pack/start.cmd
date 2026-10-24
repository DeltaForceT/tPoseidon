@echo off
cd /d "%~dp0"
"%~dp0app\drill-egg.exe" %*
if errorlevel 1 pause
