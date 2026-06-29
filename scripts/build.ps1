param(
    [string]$Version = "1.0.0"
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$release = Join-Path $root "release"
$buildDir = Join-Path ([System.IO.Path]::GetTempPath()) ("OneClickClose-build-" + [guid]::NewGuid().ToString("N"))
$packageDir = Join-Path ([System.IO.Path]::GetTempPath()) ("OneClickClose-package-" + [guid]::NewGuid().ToString("N"))
$appExe = Join-Path $release "OneClickClose.Legacy.exe"
$setupExe = Join-Path $release "OneClickCloseSetup.exe"
$appExeBuild = Join-Path $buildDir "OneClickClose.exe"
$setupExeBuild = Join-Path $buildDir "OneClickCloseSetup.exe"
$config = Join-Path $root "close-user-apps.config.json"
$zipPath = Join-Path $release ("OneClickClose.Legacy-" + $Version + ".zip")

function Find-CSharpCompiler {
    $candidates = @(
        "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    throw "Could not find .NET Framework csc.exe."
}

function Clear-FileForOverwrite {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        try {
            $item = Get-Item -LiteralPath $Path -Force
            $item.IsReadOnly = $false
        }
        catch {
        }
    }
}

New-Item -ItemType Directory -Path $release -Force | Out-Null
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

$csc = Find-CSharpCompiler
Write-Host "Using compiler: $csc"

$framework = Split-Path -Parent $csc
$refs = @(
    (Join-Path $framework "System.dll"),
    (Join-Path $framework "System.Core.dll"),
    (Join-Path $framework "System.Drawing.dll"),
    (Join-Path $framework "System.Management.dll"),
    (Join-Path $framework "System.Web.Extensions.dll"),
    (Join-Path $framework "System.Windows.Forms.dll"),
    (Join-Path $framework "Microsoft.CSharp.dll")
)

$appSources = Get-ChildItem -LiteralPath (Join-Path $root "src\OneClickClose") -Filter "*.cs" | ForEach-Object { $_.FullName }
$appArgs = @("/nologo", "/target:winexe", "/platform:anycpu", "/optimize+", "/utf8output", "/out:$appExeBuild")
foreach ($ref in $refs) {
    if (Test-Path -LiteralPath $ref) {
        $appArgs += "/reference:$ref"
    }
}
$appArgs += $appSources
& $csc @appArgs

$setupSources = Get-ChildItem -LiteralPath (Join-Path $root "src\OneClickClose.Setup") -Filter "*.cs" | ForEach-Object { $_.FullName }
$setupArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/utf8output",
    "/out:$setupExeBuild",
    "/resource:$appExeBuild,OneClickClose.exe",
    "/resource:$config,close-user-apps.config.json"
)
foreach ($ref in $refs) {
    if (Test-Path -LiteralPath $ref) {
        $setupArgs += "/reference:$ref"
    }
}
$setupArgs += $setupSources
& $csc @setupArgs

Clear-FileForOverwrite -Path $appExe
Clear-FileForOverwrite -Path $setupExe
Clear-FileForOverwrite -Path (Join-Path $release "close-user-apps.config.json")
Copy-Item -LiteralPath $appExeBuild -Destination $appExe -Force
Copy-Item -LiteralPath $setupExeBuild -Destination $setupExe -Force
Copy-Item -LiteralPath $config -Destination (Join-Path $release "close-user-apps.config.json") -Force
Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination (Join-Path $packageDir "README.md") -Force
Copy-Item -LiteralPath $setupExeBuild -Destination (Join-Path $packageDir "OneClickCloseSetup.exe") -Force
Copy-Item -LiteralPath $appExeBuild -Destination (Join-Path $packageDir "OneClickClose.Legacy.exe") -Force
Copy-Item -LiteralPath $config -Destination (Join-Path $packageDir "close-user-apps.config.json") -Force

if (Test-Path -LiteralPath $zipPath) {
    Clear-FileForOverwrite -Path $zipPath
    try {
        Remove-Item -LiteralPath $zipPath -Force
    }
    catch {
        $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $zipPath = Join-Path $release ("OneClickClose.Legacy-" + $Version + "-" + $stamp + ".zip")
        Write-Warning "Could not overwrite the existing zip. Writing $zipPath instead."
    }
}
Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force
Remove-Item -LiteralPath $packageDir -Recurse -Force
Remove-Item -LiteralPath $buildDir -Recurse -Force

Write-Host "Built:"
Write-Host "  $appExe"
Write-Host "  $setupExe"
Write-Host "  $zipPath"
