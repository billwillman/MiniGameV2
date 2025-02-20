@echo off
echo CurrentDir: %~dp0
mklink /d "C:/cygwin64/home/zengyi/MiniGameV2/Server" "%~dp0/Server"
pause