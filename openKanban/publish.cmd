@echo off
rem Runs build\publish.ps1 without changing the system execution policy.
rem Usage: publish.cmd [-Version 1.0.0] [-SkipInstaller] [-SkipTests]
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build\publish.ps1" %*
exit /b %ERRORLEVEL%
