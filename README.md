<div align="center">

# OneClickClose / 一键关闭后台软件

**关机前一键清理后台软件 —— 先预览，再温和关闭，默认保护关键工具。**

[![Release](https://img.shields.io/github/v/release/shangguanwt/one-click-close?style=flat-square&color=2ea043)](https://github.com/shangguanwt/one-click-close/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/shangguanwt/one-click-close/total?style=flat-square&color=blue)](https://github.com/shangguanwt/one-click-close/releases)
[![License](https://img.shields.io/github/license/shangguanwt/one-click-close?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/Windows-10%2F11-0078d6?style=flat-square&logo=windows)](https://github.com/shangguanwt/one-click-close/releases)
[![.NET](https://img.shields.io/badge/.NET-9.0-512bd4?style=flat-square&logo=dotnet)](https://dot.net)

</div>

一个面向 Windows 的本地小工具，用来在关机前一键清理常见用户软件后台。它会先预览，再温和关闭窗口程序；默认保护密码管理器、同步盘、代理、远控、Tailscale、Syncthing、系统服务和驱动进程。

## 项目结构

仓库包含三个项目，共享同一个解决方案 `OneClickClose.sln`：

```
src/
  OneClickClose.Core/        共享核心库（进程扫描、关闭逻辑、配置、用户偏好）
  OneClickClose.WinUI/       WinUI 3 主程序（Windows App SDK 2.2 / .NET 9）
  OneClickClose/             WinForms 旧版主程序（兼容 .NET Framework 环境）
```

WinUI 3 版本是主力发展方向，WinForms 版本作为旧版兼容保留。

## 下载发布版

到 [GitHub Releases](../../releases) 下载最新版 `OneClickClose.WinUI-<版本>-x64.zip`（当前 **v2.0.0**），解压后运行 `OneClickClose.WinUI.exe` 即可。发布包为自包含，已内置 .NET 与 Windows App SDK 运行时，**无需**额外安装任何运行时。

## 功能

- 现代 WinUI 3 深色界面（Mica 材质、NavigationView 导航、Fluent 风格卡片和徽章）。
- 智能扫描分类：识别可见窗口、父进程、用户软件路径、系统路径、内存占用，并按 0-100 风险评分排序。
- 三阶段安全关闭：先发送 `WM_CLOSE`，再发送 `WM_QUERYENDSESSION`，最后只对白名单后台辅助进程强制结束。
- 可视化配置：在软件内维护关闭名单、保护名单、强制清理名单，并查看学习记录建议。
- 搜索过滤：候选进程和保护进程页面支持实时搜索和操作类型过滤。
- 右键菜单：候选进程行可直接添加到保护名单、强制清理名单或从目标名单移除。
- 颜色编码日志：执行日志按成功/错误/警告/取消/操作分色显示。
- 键盘快捷键：F5 扫描、Ctrl+Enter 关闭、Esc 取消、Ctrl+F 搜索。
- 用户偏好学习：清理历史写入 `history.json`，偏好建议写入 `user-prefs.json`，不会覆盖默认配置。
- JSON 配置：可以维护默认关闭名单、保护名单、强制清理名单和关闭等待时间。

## 使用说明

> 首次使用建议先**扫描预览**，确认无误后再清理。本工具默认只温和关闭有窗口的程序，不会动系统服务和保护名单。

1. **启动**：运行 `OneClickClose.WinUI.exe`（WinUI 3 版）。
2. **扫描**：进入「总览」或「一键关闭」页，按 `F5` 或点「重新扫描」。工具会列出后台进程，按 0-100 风险评分排序。
3. **确认预览**：在「后台进程 / 候选进程」页查看每个进程的动作（**温和关闭** / **强制清理** / **只提示**）、风险分、内存占用和说明。
   - 觉得某进程不该关：右键 →「加入白名单」。
   - 想强制结束某后台辅助进程：右键 →「加入强制清理名单」。
4. **执行清理**：点「一键清理」（或 `Ctrl+Enter`）。工具按三阶段安全关闭：
   1. 发 `WM_CLOSE`（请求关窗）→ 2. 发 `WM_QUERYENDSESSION`（模拟关机询问）→ 3. 仅对白名单内的后台辅助进程强制结束。
5. **查看结果**：「运行日志」按 成功 / 错误 / 警告 / 取消 / 操作 分色显示。清理记录写入 `history.json`。
6. **随时取消**：清理过程中按 `Esc` 中止。

### 界面导航（WinUI 3）

左侧 NavigationView 分页：总览（CPU / 内存 / 温度 / 电源实时监控 + 性能曲线）、一键关闭、后台进程、白名单、性能监控、清理记录、设置。

### 快捷键

| 按键 | 作用 |
|---|---|
| `F5` | 扫描 |
| `Ctrl+Enter` | 一键清理 |
| `Esc` | 取消 |
| `Ctrl+F` | 搜索过滤 |

### 安全说明

- 默认**保护**密码管理器、同步盘、代理、远控、系统服务和驱动进程，不会关闭。
- 只有显式加入「强制清理名单」的进程才会被强杀；其余一律温和关闭。
- 偏好数据只存本机（`%LOCALAPPDATA%\OneClickClose\`），不上传、不修改仓库默认配置。

## 环境要求

### WinUI 3 版本（推荐）

| 依赖 | 最低版本 |
|---|---|
| Windows | 10 1809 (17763) 及以上，推荐 Windows 11 |
| .NET SDK | 9.0.x（需 `net9.0-windows10.0.26100.0` 目标框架） |
| Windows App SDK | 2.2.0（NuGet 自动还原，运行时需安装 MSIX） |

> **从 [GitHub Releases](../../releases) 下载自包含 zip 的用户无需以下步骤** —— 发布包已内置 .NET 和 Windows App SDK 运行时，解压即用。

仅当从源码做**非自包含**构建时，运行时才需要安装 4 个 MSIX 包（从 NuGet 缓存中获取）：

```powershell
# 在 NuGet 包缓存目录中找到这些文件，通常在：
# %USERPROFILE%\.nuget\packages\microsoft.windowsappsdk\2.2.0\tools\MSIX\win10-x64\
Add-AppxPackage Microsoft.WindowsAppRuntime.Singleton-x64.msix
Add-AppxPackage Microsoft.WindowsAppRuntime.Main-x64.msix
Add-AppxPackage Microsoft.WindowsAppRuntime.DDLM-x64.msix
Add-AppxPackage Microsoft.WindowsAppRuntime-x64.msix
```

### WinForms 旧版

可以用 .NET 9 SDK 的 SDK-style csproj 构建，也可以用 `scripts/build.ps1` 通过系统自带的 .NET Framework csc.exe 构建（无需安装 SDK）。

## 从源码构建

### WinUI 3 版本（推荐）

```powershell
# 确保已安装 .NET 9 SDK (https://dot.net)

# 快速开发运行
dotnet run --project src/OneClickClose.WinUI

# 发布构建（输出到 release/ 目录）
.\scripts\build-winui.ps1

# 自包含发布（内置 .NET 和 Windows App SDK 运行时，解压即用，约 80MB）
.\scripts\build-winui.ps1 -SelfContained
```

发布输出位于 `release\OneClickClose.WinUI-<版本>-<平台>\`，可分发 zip 为 `release\OneClickClose.WinUI-<版本>-<平台>.zip`。

### WinForms 旧版

```powershell
# 方式一：.NET SDK
dotnet build OneClickClose.sln

# 方式二：无需 SDK，使用系统自带 csc.exe
.\scripts\build.ps1
```

输出文件：`release\OneClickClose.Legacy.exe`

## 快速使用

从 `release` 下载或构建后：

- **WinUI 3 版**：运行 `release\OneClickClose.WinUI-<版本>-<平台>\OneClickClose.WinUI.exe`
- **WinForms 旧版**：运行 `release\OneClickClose.Legacy.exe`，或双击 `OneClickCloseSetup.exe` 安装器

WinForms 安装器会创建：

- 桌面快捷方式：`一键关闭后台软件`
- 开始菜单文件夹：`OneClickClose`
- 安装目录：`%LOCALAPPDATA%\OneClickClose`

## 配置

配置文件名为 `close-user-apps.config.json`。

- `targetNames`：纳入关闭列表的进程名。
- `protectedNames`：永远保护，不关闭。
- `forceAllowedNames`：允许强制结束的后台辅助进程。
- `gracefulTimeoutSeconds`：阶段 1 温和关闭后的等待秒数，默认 5。
- `queryTimeoutSeconds`：阶段 2 关机会话提示后的等待秒数，默认 3。
- `waitSeconds`：旧版兼容字段，新配置会保留但优先使用上面两个字段。

默认保护名单覆盖系统关键进程（`csrss`、`wininit`、`winlogon`、`services`、`lsass`、`dwm`、`explorer` 等）、常见输入法 / 音频 / 显卡服务，以及密码管理器、同步盘、代理、远控这类不应被误杀的辅助工具类别。可在软件内或 `close-user-apps.config.json` 里按自己的环境增删。

用户偏好数据默认保存在：

```text
%LOCALAPPDATA%\OneClickClose\history.json
%LOCALAPPDATA%\OneClickClose\user-prefs.json
```

这些文件只记录本机使用习惯，不会修改仓库里的默认配置。

## 旧版脚本

仓库根目录里仍保留了早期 PowerShell GUI 脚本：

- `Close-UserApps.ps1`
- `Install-Shortcut.ps1`

正式发布建议使用 WinUI 3 版本。

## 许可证

MIT
