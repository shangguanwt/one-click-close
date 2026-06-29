<#
.SYNOPSIS
    Build the WinUI 3 version of OneClickClose.

.DESCRIPTION
    Uses `dotnet publish` to produce a self-contained or framework-dependent
    release build of the WinUI 3 frontend.  Output goes to
    release/OneClickClose.WinUI-<Version>/ so it does not collide with
    the legacy WinForms build.

.PARAMETER Version
    Semantic version stamped into the output folder name.  Default "2.0.0".

.PARAMETER SelfContained
    If set, bundles the .NET runtime so the user does not need to install it.
    Produces a larger output (~80 MB).  Default is framework-dependent (~5 MB).

.PARAMETER Platform
    Target RID architecture: x64 or arm64.  Default: host architecture.

.EXAMPLE
    .\scripts\build-winui.ps1
    .\scripts\build-winui.ps1 -SelfContained
    .\scripts\build-winui.ps1 -Version 2.1.0 -SelfContained -Platform arm64
#>
param(
    [string]$Version = "2.0.0",
    [switch]$SelfContained,
    [ValidateSet("x64", "arm64")]
    [string]$Platform
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $root "src\OneClickClose.WinUI\OneClickClose.WinUI.csproj"
$release = Join-Path $root "release"

if (-not $Platform) {
    $Platform = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLower()
    if ($Platform -ne "x64" -and $Platform -ne "arm64") {
        $Platform = "x64"
    }
}

$rid = "win-$Platform"
$outputDir = Join-Path $release "OneClickClose.WinUI-$Version-$Platform"
$zipPath = Join-Path $release "OneClickClose.WinUI-$Version-$Platform.zip"

Write-Host "=== OneClickClose WinUI 3 Build ===" -ForegroundColor Cyan
Write-Host "  Project : $project"
Write-Host "  RID     : $rid"
Write-Host "  Version : $Version"
Write-Host "  Output  : $outputDir"
Write-Host "  Self-contained: $SelfContained"
Write-Host ""

# Ensure .NET SDK is available
try {
    $sdkVersion = (dotnet --version 2>&1)
    Write-Host "  .NET SDK: $sdkVersion" -ForegroundColor DarkGray
}
catch {
    throw "dotnet SDK not found. Install .NET 9 SDK from https://dot.net"
}

# Build arguments
$publishArgs = @(
    "publish",
    $project,
    "--configuration", "Release",
    "--runtime", $rid,
    "--self-contained", $SelfContained.IsPresent.ToString().ToLower(),
    "/p:Version=$Version",
    "--output", $outputDir
)

if ($SelfContained) {
    # Bundle the Windows App SDK runtime too, so the unzipped package runs
    # without the user manually installing the WindowsAppRuntime MSIX packages.
    $publishArgs += "/p:WindowsAppSDKSelfContained=true"
}

Write-Host "Running: dotnet $($publishArgs -join ' ')" -ForegroundColor DarkGray
Write-Host ""

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# Copy config if not already present
$configSrc = Join-Path $root "close-user-apps.config.json"
$configDst = Join-Path $outputDir "close-user-apps.config.json"
if ((Test-Path $configSrc) -and -not (Test-Path $configDst)) {
    Copy-Item -LiteralPath $configSrc -Destination $configDst
}

# Create zip
if (Test-Path $zipPath) {
    try {
        Remove-Item -LiteralPath $zipPath -Force
    }
    catch {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $zipPath = Join-Path $release "OneClickClose.WinUI-$Version-$Platform-$stamp.zip"
        Write-Warning "Could not overwrite zip. Writing $zipPath instead."
    }
}
Compress-Archive -Path (Join-Path $outputDir "*") -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "=== Build complete ===" -ForegroundColor Green
Write-Host "  Folder: $outputDir"
Write-Host "  Zip   : $zipPath"
Write-Host ""
Write-Host "To run:" -ForegroundColor DarkGray
Write-Host "  $outputDir\OneClickClose.WinUI.exe"
