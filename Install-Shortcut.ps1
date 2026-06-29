param(
    [string]$ShortcutName = '一键关闭后台软件'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$appRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$mainScript = Join-Path $appRoot 'Close-UserApps.ps1'
if (-not (Test-Path -LiteralPath $mainScript)) {
    throw "找不到主脚本：$mainScript"
}

$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop ($ShortcutName + '.lnk')
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $powershell
$shortcut.Arguments = ('-NoProfile -STA -ExecutionPolicy Bypass -File "{0}"' -f $mainScript)
$shortcut.WorkingDirectory = $appRoot
$shortcut.WindowStyle = 1
$shortcut.Description = '预览并安全关闭常见用户后台软件'
$shortcut.IconLocation = "$env:SystemRoot\System32\shell32.dll,167"
$shortcut.Save()

Write-Host "已创建快捷方式：$shortcutPath"
