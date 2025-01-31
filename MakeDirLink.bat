@echo off
echo CurrentDir: %~dp0

mklink /d "%~dp0/Library/PackageCache/com.unity.netcode.gameobjects" "%~dp0/backup/com.unity.netcode.gameobjects"
mklink /d "%~dp0/outPath/Proj/Library/PackageCache/com.unity.netcode.gameobjects" "%~dp0/backup/com.unity.netcode.gameobjects"

pause