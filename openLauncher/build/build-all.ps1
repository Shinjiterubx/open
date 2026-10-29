<#
.SYNOPSIS
  Builds the whole suite: openNotes, openCalendar, openKanban and the openLauncher installer that bundles them.

.DESCRIPTION
  Expects the four projects side by side (the layout of the repository root):
      <repo>\openNotes
      <repo>\openCalendar
      <repo>\openKanban
      <repo>\openLauncher   (this one)
  1. runs each app's build\publish.ps1 (tests + publish + its own setup),
  2. copies the setups to installer\payload\<App>-Setup.exe,
  3. publishes openLauncher and compiles installer\openLauncher.iss
     → artifacts\installer\openSuite-Setup-<version>-x64.exe

.EXAMPLE
  .\build\build-all.ps1
  .\build\build-all.ps1 -Version 1.1.0  # default: the VERSION file in the repo root
  .\build\build-all.ps1 -SkipApps        # reuse the setups already in installer\payload
  .\build\build-all.ps1 -SkipTests

.NOTES
  Requires the .NET 8 SDK and Inno Setup 6 (winget install JRSoftware.InnoSetup).
#>
param(
    [string] $Version = "",   # empty = read from the VERSION file in the repository root
    [switch] $SkipApps,
    [switch] $SkipTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$suiteRoot = Split-Path -Parent $root
$payloadDir = Join-Path $root "installer\payload"
$publishDir = Join-Path $root "artifacts\publish\win-x64"
$apps = @("openNotes", "openCalendar", "openKanban")

if (-not $Version) {
    $versionFile = Join-Path $suiteRoot "VERSION"
    $Version = if (Test-Path $versionFile) { (Get-Content $versionFile -Raw).Trim() } else { "1.0.0" }
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid version '$Version' (expected e.g. 1.2.0)." }
Write-Host "Version: $Version" -ForegroundColor DarkYellow

New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null

if (-not $SkipApps) {
    foreach ($app in $apps) {
        $appRoot = Join-Path $suiteRoot $app
        $script = Join-Path $appRoot "build\publish.ps1"
        if (-not (Test-Path $script)) { throw "$app not found at $appRoot (the projects must be side by side)." }

        Write-Host "▶ $app" -ForegroundColor DarkYellow
        $arguments = @{ Version = $Version }
        if ($SkipTests) { $arguments.SkipTests = $true }
        & $script @arguments
        if (-not $?) { throw "$app build failed." }

        $setup = Join-Path $appRoot "artifacts\installer\$app-Setup-$Version-x64.exe"
        if (-not (Test-Path $setup)) { throw "Setup not found: $setup" }
        Copy-Item $setup (Join-Path $payloadDir "$app-Setup.exe") -Force
    }
}

foreach ($app in $apps) {
    if (-not (Test-Path (Join-Path $payloadDir "$app-Setup.exe"))) { throw "Missing payload: installer\payload\$app-Setup.exe" }
}

if (-not $SkipTests) {
    Write-Host "▶ openLauncher tests" -ForegroundColor DarkYellow
    dotnet test (Join-Path $root "openLauncher.sln") -c Release
    if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
}

Write-Host "▶ Publishing openLauncher $Version" -ForegroundColor DarkYellow
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root "src\OpenLauncher\OpenLauncher.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:Version=$Version `
    -p:PublishReadyToRun=true `
    -p:DebugType=none `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

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

Write-Host "▶ Building suite installer" -ForegroundColor DarkYellow
& $iscc "/DAppVersion=$Version" (Join-Path $root "installer\openLauncher.iss")
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

Write-Host "✔ Installer: $(Join-Path $root "artifacts\installer\openSuite-Setup-$Version-x64.exe")" -ForegroundColor Green
