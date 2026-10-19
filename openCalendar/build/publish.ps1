<#
.SYNOPSIS
  Builds openCalendar and the Windows installer.

.EXAMPLE
  .\build\publish.ps1                    # version 1.0.0, self-contained x64 + installer
  .\build\publish.ps1 -Version 1.2.0
  .\build\publish.ps1 -SkipInstaller     # only the published app in artifacts\publish\win-x64

.NOTES
  Requires the .NET 8 SDK. The installer needs Inno Setup 6 (https://jrsoftware.org/isinfo.php),
  e.g. `winget install JRSoftware.InnoSetup`.
#>
param(
    [string] $Version = "1.0.0",
    [switch] $SkipInstaller,
    [switch] $SkipTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "artifacts\publish\win-x64"

if (-not $SkipTests) {
    Write-Host "▶ Tests" -ForegroundColor Magenta
    dotnet test (Join-Path $root "openCalendar.sln") -c Release
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
}

Write-Host "▶ Publishing openCalendar $Version" -ForegroundColor Magenta
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish (Join-Path $root "src\OpenCalendar\OpenCalendar.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:Version=$Version `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

if ($SkipInstaller) {
    Write-Host "✔ Published to $publishDir" -ForegroundColor Green
    return
}

$iscc = (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) { throw "Inno Setup 6 (ISCC.exe) not found. Install it: winget install JRSoftware.InnoSetup" }

Write-Host "▶ Building installer" -ForegroundColor Magenta
& $iscc "/DAppVersion=$Version" (Join-Path $root "installer\openCalendar.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

Write-Host "✔ Installer: $(Join-Path $root "artifacts\installer\openCalendar-Setup-$Version-x64.exe")" -ForegroundColor Green
