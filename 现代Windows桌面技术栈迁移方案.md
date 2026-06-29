# OneClickClose 现代 Windows 桌面技术栈迁移方案

> 目标：把 OneClickClose 从当前的轻量 WinForms 工具，升级为符合 2026 年 Windows 桌面应用主流方向的现代原生应用，同时保留当前版本可用、可发布、可回滚。

## 1. 当前状态判断

当前项目路线：

- UI：WinForms + 自绘控件
- 运行时：.NET Framework 4.x
- 构建：`scripts/build.ps1` 直接调用系统自带 `csc.exe`
- 发布：`release/OneClickClose.exe`、`OneClickCloseSetup.exe`、zip 包
- 优点：轻量、依赖少、构建简单、适合系统工具类 exe
- 短板：不是现代 .NET SDK 项目；UI 技术不是微软当前主推的 Windows 11 原生 UI 栈；打包、更新、可维护性、设计系统扩展能力有限

结论：当前项目不是“过时到必须重写”，但如果目标是“最现代 Windows 桌面技术栈”，应进入 WinUI 3 / Windows App SDK 路线。

## 2. 推荐目标技术栈

### 首选路线

| 层级 | 推荐方案 | 原因 |
| --- | --- | --- |
| UI 框架 | WinUI 3 | 微软当前现代 Windows 原生 UI 框架，贴近 Fluent Design 和 Windows 11 视觉语言 |
| 平台 SDK | Windows App SDK Stable | 给桌面应用提供现代 Windows API、窗口、生命周期、部署、通知等能力 |
| 运行时 | .NET 10 LTS | 2026 年更合适的长期支持 .NET 版本，生命周期更长 |
| 语言 | C# 14 | 跟随 .NET 10，保持现代 C# 语法和工具链 |
| 项目格式 | SDK-style `.csproj` | 使用 `dotnet build/publish/test`，方便 CI、依赖管理和发布 |
| UI 架构 | MVVM | 把界面状态、业务逻辑、进程扫描/关闭逻辑拆开，降低 MainForm 式巨型文件风险 |
| MVVM 工具 | CommunityToolkit.Mvvm | 轻量、成熟，适合 WinUI/WPF/桌面应用 |
| 打包 | MSIX + portable zip 双路线 | MSIX 负责现代安装/卸载/升级，zip 保留便携分发 |
| 发布 | GitHub Actions + GitHub Releases + winget 可选 | 更符合现代开源/桌面工具发布方式 |

### 不推荐作为最终路线

| 方案 | 不推荐原因 |
| --- | --- |
| 继续 .NET Framework WinForms | 可继续维护，但不是“最现代” |
| WPF on .NET 10 | 比 WinForms 现代，XAML 能力强，但微软当前 Windows 11 原生方向更偏 WinUI 3 |
| MAUI | 更适合跨平台；OneClickClose 是强 Windows 系统工具，不需要跨平台抽象 |
| Electron / WebView 外壳 | 对系统工具偏重，启动和资源占用不划算，也不符合“原生 Windows”目标 |

## 3. 为什么是 WinUI 3 + Windows App SDK

微软官方定位中，Windows App SDK 提供构建现代 Windows 应用的统一 API 和工具，可以用于 WinUI 3，也可以给现有 WinForms/WPF/Win32 应用增量接入现代能力。

WinUI 3 是微软现代原生 Windows UI 框架，提供 Fluent Design 风格、高性能渲染、XAML 编程模型，并运行在 Windows 10 1809 及以上系统。

这与 OneClickClose 的目标非常匹配：

- 这是 Windows 专属系统工具，不需要跨平台
- 需要轻快、可靠、原生外观
- 需要比 WinForms 更自然的布局、动画、主题、可访问性和高 DPI 体验
- 后续可以接入现代通知、窗口管理、应用生命周期、MSIX 部署等能力

## 4. 推荐迁移策略

不要直接在现有 WinForms 项目上硬改。建议采用“双轨迁移”：

1. 保留当前 WinForms 版本作为稳定版。
2. 新建 `src/OneClickClose.WinUI` 作为现代 UI 壳。
3. 把进程扫描、关闭计划、配置读写、学习建议等逻辑抽到共享库。
4. WinUI 新壳复用共享业务逻辑。
5. 功能等价后，再切换默认发布物。

推荐目录结构：

```text
one-click-close/
  src/
    OneClickClose.LegacyWinForms/      # 当前 WinForms 版本，保留或逐步迁移
    OneClickClose.WinUI/               # 新 WinUI 3 桌面应用
    OneClickClose.Core/                # 进程扫描、关闭策略、配置、日志等业务逻辑
    OneClickClose.Tests/               # 单元测试
  packaging/
    msix/
    portable/
  scripts/
    build.ps1
    build-winui.ps1
    publish-msix.ps1
  docs/
    architecture.md
    migration.md
```

## 5. 架构拆分建议

### OneClickClose.Core

放所有不依赖 WinForms/WinUI 的逻辑：

- `ProcessPlanner`
- `AppConfig`
- `UserPreferences`
- 关闭进程策略
- 保护列表规则
- 日志事件模型
- 学习建议模型

目标：业务逻辑不认识任何 UI 控件。

### OneClickClose.WinUI

只负责现代界面：

- `MainWindow.xaml`
- `OverviewPage.xaml`
- `CandidateProcessesPage.xaml`
- `ProtectedProcessesPage.xaml`
- `LogsPage.xaml`
- `SettingsPage.xaml`
- `LearningPage.xaml`
- `ViewModels/`
- `Styles/Theme.xaml`

目标：界面绑定 ViewModel，不在窗口代码里堆业务逻辑。

### ViewModel 划分

```text
ShellViewModel
OverviewViewModel
CandidateProcessesViewModel
ProtectedProcessesViewModel
LogViewModel
SettingsViewModel
LearningViewModel
```

## 6. UI 风格方向

目标视觉：现代 Windows 11 工具应用，而不是网页后台或传统 WinForms。

建议风格：

- 主题：深色优先，支持系统主题跟随
- 色彩：蓝灰中性色 + 一个主蓝色 + 风险红色
- 布局：左侧导航 + 顶部状态区 + 内容页
- 控件：WinUI 原生 `NavigationView`、`InfoBar`、`CommandBar`、`DataGrid` 或列表控件
- 反馈：扫描中、关闭中、部分失败、需确认等状态必须清楚
- 可访问性：键盘可操作、控件名称、对比度、缩放适配
- DPI：125%、150%、200% 下不重叠

## 7. 发布与安装方案

### 推荐发布物

1. `OneClickClose.msix`
   - 现代安装、卸载、升级体验
   - 更适合长期维护

2. `OneClickClose-portable.zip`
   - 保留便携版
   - 适合不想安装的用户

3. `OneClickCloseSetup.exe`
   - 可暂时保留，作为旧路线兼容
   - 后续可降级为备用发布物

### GitHub Actions

建议 CI 做这些事：

- `dotnet restore`
- `dotnet build -c Release`
- `dotnet test`
- `dotnet publish`
- 生成 MSIX/zip
- 上传 artifact
- 打 tag 时创建 GitHub Release

## 8. 分阶段计划

### Phase 0：保留稳定版

目标：当前 WinForms 版本继续可构建、可发布。

验收：

- `scripts/build.ps1` 仍能生成现有 release 文件
- 当前 exe 不被破坏
- 当前安装包仍可用

### Phase 1：抽离 Core

目标：把非 UI 逻辑从 WinForms 中拆出来。

任务：

- 新建 `OneClickClose.Core`
- 迁移配置、进程扫描、进程关闭、日志模型
- WinForms 项目改为调用 Core

验收：

- WinForms 功能不变
- Core 有基本单元测试

### Phase 2：建立 WinUI 3 壳

目标：新建可运行的 WinUI 3 桌面应用。

任务：

- 新建 WinUI 3 项目
- 建立 Shell、导航、主题、基础页面
- 接入 Core 的只读扫描功能

验收：

- WinUI app 可启动
- 能显示候选进程和保护列表
- UI 风格初步成型

### Phase 3：功能等价

目标：WinUI 版本达到现有 WinForms 功能水平。

任务：

- 预览关闭计划
- 执行一键关闭
- 实时日志
- 设置页
- 学习建议
- 错误与权限提示

验收：

- WinUI 版本能完成完整工作流
- 与 WinForms 版本结果一致

### Phase 4：现代发布

目标：建立 MSIX + zip 发布。

任务：

- MSIX 打包
- portable zip
- GitHub Actions
- Release 说明

验收：

- 新 release 可安装、可卸载、可升级
- 便携版可直接运行

### Phase 5：切换主线

目标：WinUI 版本成为默认下载。

任务：

- README 改为介绍 WinUI 版本
- WinForms 标记为 Legacy
- 保留回滚入口

验收：

- 用户默认获取 WinUI 版本
- 旧版本仍可在 release 中下载

## 9. 风险与应对

| 风险 | 应对 |
| --- | --- |
| WinUI 迁移导致进度变慢 | 先抽 Core，再做新壳，不直接破坏旧版 |
| MSIX 对用户环境有要求 | 同时提供 portable zip |
| 权限/进程关闭逻辑在打包后行为变化 | 迁移后做真实机器 smoke test |
| UI 变漂亮但工具效率下降 | 保留一键操作、预览、日志、设置的短路径 |
| 依赖 Windows App SDK Runtime | 发布时明确 runtime 策略，必要时自包含或引导安装 |

## 10. 最终建议

如果目标是“最现代 Windows 桌面技术栈”，推荐路线是：

```text
WinUI 3 + Windows App SDK Stable + .NET 10 LTS + C# 14 + MVVM + MSIX/portable zip
```

但不要一次性重写。OneClickClose 已经是可用工具，正确做法是：

```text
保留当前 WinForms 稳定版
→ 抽离 OneClickClose.Core
→ 新建 WinUI 3 壳
→ 功能等价
→ 建立现代发布
→ WinUI 成为主线
```

这条路线既符合现代 Windows 应用方向，也能控制风险。

## 参考资料

- Windows App SDK: https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/
- WinUI 3: https://learn.microsoft.com/en-us/windows/apps/winui/winui3/
- .NET 10: https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview
- MSIX: https://learn.microsoft.com/en-us/windows/msix/overview
