@echo off
cd /d "%~dp0"
echo [1/2] Publishing solution (all projects except Uno)...
dotnet publish -c Release
if errorlevel 1 exit /b 1
echo [2/2] Publishing Uno separately...
dotnet publish src\minesweeperuno -c Release -p:Uno=true
if errorlevel 1 exit /b 1
echo Done. See dist folders.
