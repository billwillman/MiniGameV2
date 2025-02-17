@echo off
echo CurrentDir: %~dp0
cd "%~dp0/outPath/DS/"
Server.exe '{"dsData":{"ip":"127.0.0.1","port":7777, "scene": "MultiScene"},"GsData":{"ip":"127.0.0.1","port":1991}, "isLocalDS":true}'