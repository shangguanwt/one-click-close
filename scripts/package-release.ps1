param(
    [string]$Version = "1.0.0",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Parent,
        [Parameter(Mandatory = $true)][string]$Child
    )

    $parentFull = [System.IO.Path]::GetFullPath($Parent).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $childFull = [System.IO.Path]::GetFullPath($Child).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)

    if (!$childFull.StartsWith($parentFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean path outside repository: $childFull"
    }
}

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    Write-Host "dotnet $($Arguments -join ' ')"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE"
    }
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Value, $utf8NoBom)
}

function Get-RelativePackagePath {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootUri = [System.Uri]([System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar)
    $pathUri = [System.Uri]([System.IO.Path]::GetFullPath($Path))
    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($pathUri).ToString()).Replace('/', '\')
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solution = Join-Path $repoRoot "OneClickClose.sln"
$winUiProject = Join-Path $repoRoot "src\OneClickClose.WinUI\OneClickClose.WinUI.csproj"
$launcherProject = Join-Path $repoRoot "src\OneClickClose.Launcher\OneClickClose.Launcher.csproj"
$releaseRoot = Join-Path $repoRoot "release"
$stagingRoot = Join-Path $releaseRoot "staging"
$packageName = "OneClickClose-v$Version-$Runtime"
$packageRoot = Join-Path $stagingRoot $packageName
$appDir = Join-Path $packageRoot "app"
$docsDir = Join-Path $packageRoot "docs"
$checksumsDir = Join-Path $packageRoot "checksums"
$launcherPublishDir = Join-Path $releaseRoot "launcher-publish"
$zipPath = Join-Path $releaseRoot "$packageName.zip"
$zipShaPath = "$zipPath.sha256"

Assert-ChildPath -Parent $repoRoot -Child $releaseRoot
Assert-ChildPath -Parent $repoRoot -Child $stagingRoot
Assert-ChildPath -Parent $repoRoot -Child $launcherPublishDir

Write-Host "Stopping running OneClickClose processes from this workspace..."
$runningProcesses = Get-Process -Name "OneClickClose", "OneClickClose.WinUI" -ErrorAction SilentlyContinue |
    Where-Object {
        try {
            $_.Path -and ([System.IO.Path]::GetFullPath($_.Path).StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase))
        }
        catch {
            $false
        }
    }

if ($runningProcesses) {
    $runningProcesses | Stop-Process -Force
    foreach ($process in $runningProcesses) {
        try {
            Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
        }
        catch {
            Write-Host "Process $($process.Id) already exited."
        }
    }
}

if (Test-Path $releaseRoot) {
    Get-ChildItem -LiteralPath $releaseRoot -Force |
        Remove-Item -Recurse -Force
}

New-Item -ItemType Directory -Force $appDir, $docsDir, $checksumsDir, $launcherPublishDir | Out-Null

Push-Location $repoRoot
try {
    Invoke-DotNet -Arguments @("restore", $solution, "-p:RuntimeIdentifier=$Runtime")

    if (-not $SkipTests) {
        Invoke-DotNet -Arguments @("test", $solution, "-c", $Configuration, "--no-restore", "-p:RuntimeIdentifier=$Runtime")
    }

    Invoke-DotNet -Arguments @(
        "publish", $winUiProject,
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained", "false",
        "--no-restore",
        "-o", $appDir,
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    )

    Invoke-DotNet -Arguments @(
        "publish", $launcherProject,
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained", "false",
        "--no-restore",
        "-o", $launcherPublishDir,
        "-p:PublishSingleFile=true",
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    )
}
finally {
    Pop-Location
}

$launcherExe = Join-Path $launcherPublishDir "OneClickClose.exe"
if (!(Test-Path $launcherExe)) {
    throw "Launcher publish did not produce OneClickClose.exe"
}

Copy-Item -LiteralPath $launcherExe -Destination (Join-Path $packageRoot "OneClickClose.exe") -Force
Remove-Item -LiteralPath $launcherPublishDir -Recurse -Force

$noisePatterns = @("*.pdb", "*.log", "*.tmp", "trend.json", "history.json", "user-prefs.json", "README_RELEASE.txt")
foreach ($pattern in $noisePatterns) {
    Get-ChildItem -Path $packageRoot -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue |
        Remove-Item -Force
}

$readme = @(
    "OneClickClose 一键关闭 v$Version",
    "",
    "运行 / Run",
    "1. 解压整个文件夹。",
    "2. 双击根目录的 OneClickClose.exe。",
    "3. 请不要单独移动 app 文件夹，真实 WinUI 主程序位于 app\OneClickClose.WinUI.exe。",
    "",
    "说明 / Notes",
    "- 这是未打包 WinUI 3 便携版，依赖文件保留在 app 文件夹内以保证启动稳定。",
    "- 配置、习惯记录、清理历史和趋势数据写入 %LocalAppData%\OneClickClose，不会写回 Release 解压目录。",
    "- 温度读取依赖传感器、驱动和权限；无法读取时应用会显示原因。",
    "",
    "English",
    "Unzip the whole folder and run OneClickClose.exe from the root. Keep the app folder next to the launcher.",
    ""
) -join [Environment]::NewLine
Write-Utf8NoBom -Path (Join-Path $packageRoot "README.txt") -Value $readme

$releaseDate = Get-Date -Format "yyyy-MM-dd"
$releaseNotes = @(
    "# OneClickClose v$Version Release Notes",
    "",
    "发布日期 / Date: $releaseDate",
    "",
    "## 中文",
    "",
    "- 发布包改为整洁便携结构：根目录只保留 OneClickClose.exe、README.txt、app/、docs/、checksums/。",
    "- 新增根目录启动器，自动转发到 app/OneClickClose.WinUI.exe，缺少主程序时显示中文错误提示。",
    "- WinUI 和 Windows App SDK 依赖完整保留在 app/，避免移动 DLL 导致启动失败。",
    "- 清理 .pdb、运行时状态文件、日志和临时文件，减少压缩包噪音。",
    "- 趋势数据改写到 %LocalAppData%\OneClickClose\trend.json，Release 文件夹不会被运行数据污染。",
    "",
    "## English",
    "",
    "- The release zip now uses a clean portable layout with the launcher at the root and WinUI files under app/.",
    "- The root launcher starts app/OneClickClose.WinUI.exe and shows a clear Chinese error if the app payload is missing.",
    "- Required WinUI and Windows App SDK dependencies stay inside app/ for stable unpackaged startup.",
    "- PDBs, runtime state files, logs, and temp files are removed from the package.",
    "- Trend data is stored in %LocalAppData%\OneClickClose\trend.json instead of the release folder.",
    "",
    "## 已知限制 / Known Limits",
    "",
    "- 硬件温度取决于传感器、驱动和权限。",
    "- 当前是便携 zip，不是 MSIX 或安装器。",
    "- 本工具定位为后台应用治理，不是杀毒或完整电脑管家。",
    ""
) -join [Environment]::NewLine
Write-Utf8NoBom -Path (Join-Path $docsDir "RELEASE_NOTES.md") -Value $releaseNotes

$sourceDocsDir = Join-Path $repoRoot "docs"
if (Test-Path $sourceDocsDir) {
    foreach ($docName in @("USAGE.md", "NOTES.md", "FAQ.md", "DEVELOPMENT.md", "GITHUB_RELEASE_CHECKLIST.md")) {
        $sourceDoc = Join-Path $sourceDocsDir $docName
        if (Test-Path $sourceDoc) {
            Copy-Item -LiteralPath $sourceDoc -Destination (Join-Path $docsDir $docName) -Force
        }
    }

    $sourceAssetsDir = Join-Path $sourceDocsDir "assets"
    if (Test-Path $sourceAssetsDir) {
        Copy-Item -LiteralPath $sourceAssetsDir -Destination (Join-Path $docsDir "assets") -Recurse -Force
    }
}

$licenseSource = Join-Path $repoRoot "LICENSE"
if (Test-Path $licenseSource) {
    Copy-Item -LiteralPath $licenseSource -Destination (Join-Path $docsDir "LICENSE.txt") -Force
}
else {
    Write-Utf8NoBom -Path (Join-Path $docsDir "LICENSE.txt") -Value "License file was not found in the repository."
}

$checksumReadme = @(
    "校验说明 / Checksum Notes",
    "",
    "- FILES.sha256：包内文件逐项校验。",
    "- $($packageName).zip.sha256：外部 zip 校验文件，会生成在 release 目录并应随 GitHub Release 一起上传。",
    "",
    "说明：zip 文件无法把自身最终 SHA256 可靠地放在 zip 内部，因为写入该校验文件会改变 zip 本身的哈希。",
    ""
) -join [Environment]::NewLine
Write-Utf8NoBom -Path (Join-Path $checksumsDir "README.txt") -Value $checksumReadme

$filesHashPath = Join-Path $checksumsDir "FILES.sha256"
$fileHashLines = Get-ChildItem -Path $packageRoot -Recurse -File |
    Where-Object { $_.FullName -ne $filesHashPath } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = Get-RelativePackagePath -Root $packageRoot -Path $_.FullName
        $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        "$hash  $relative"
    }
Write-Utf8NoBom -Path $filesHashPath -Value (($fileHashLines -join [Environment]::NewLine) + [Environment]::NewLine)

Compress-Archive -Path $packageRoot -DestinationPath $zipPath -Force

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
Set-Content -LiteralPath $zipShaPath -Value "$zipHash  $(Split-Path -Leaf $zipPath)" -Encoding ASCII

$packageSizeMb = [Math]::Round((Get-Item $zipPath).Length / 1MB, 2)
$appFileCount = (Get-ChildItem -Path $appDir -Recurse -File).Count

Write-Host ""
Write-Host "Release package ready:"
Write-Host "  Folder: $packageRoot"
Write-Host "  Zip:    $zipPath"
Write-Host "  SHA256: $zipHash"
Write-Host "  Size:   $packageSizeMb MB"
Write-Host "  app files: $appFileCount"
