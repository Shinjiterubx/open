@echo off
rem Builds every app and the suite installer (openLauncher\build\build-all.ps1).
rem Usage: build.cmd [-Version 1.0.0] [-SkipApps] [-SkipTests]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0openLauncher\build\build-all.ps1" %*
exit /b %ERRORLEVEL%
