@echo off
rem Runs build\build-all.ps1 without changing the system execution policy.
rem Usage: build-all.cmd [-Version 1.0.0] [-SkipApps] [-SkipTests]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build\build-all.ps1" %*
exit /b %ERRORLEVEL%
