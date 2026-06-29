# OneClickClose 市场驱动优化方案

## 概述

基于竞品分析（Winhance、火绒、Process Hacker、Windows 任务管理器）和用户痛点调研，为 OneClickClose 制定全面的功能和体验优化计划。

**核心用户痛点**：关机等待后台程序、不知道哪些进程能安全关闭、手动逐个结束太繁琐、误关关键进程的风险、Windows 遥测/AI 服务占用资源、开机自启项管理困难。

**竞品差异化定位**：专注"关机前一键清理"场景，比 Winhance 更轻量聚焦，比 Process Hacker 更友好，比任务管理器更智能。

---

## 一、当前架构分析

### Core 层（`src/OneClickClose.Core/`）
- `ProcessPlanner.cs` — 同步扫描，`Process.GetProcesses()` + WMI 全量枚举阻塞调用线程
- `ProcessCollector.cs` — WMI `GetParentProcessMap()` 延迟 300-800ms
- `CloseExecutor.cs` — 三阶段关闭管线（已较完善）
- `RiskCalculator.cs` — 风险评分
- `AppConfig.cs` — JSON 配置（目标/保护/强制三个名单）
- `UserPreferences.cs` — 用户偏好学习

### WinUI 层（`src/OneClickClose.WinUI/`）
- `AppState.cs` — 纯静态类，无变更通知，页面间靠手动刷新
- 已引用 `CommunityToolkit.Mvvm` 但完全未使用
- 6 个页面：总览 / 候选进程 / 保护进程 / 运行日志 / 学习建议 / 设置

### 关键技术债务
1. 扫描方法同步阻塞 UI 线程 500ms-2s
2. WMI 父进程查询慢
3. 静态状态无变更通知
4. CommunityToolkit.Mvvm 未利用

---

## 二、优化方案（按优先级）

### 第一梯队：高价值功能（核心差异化）

#### A1. 内存/CPU 实时监控面板

**痛点**：用户想看到哪些进程占资源最多

**Core 层**：
- 新增 `src/OneClickClose.Core/SystemMonitor.cs`
- 数据模型 `SystemSnapshot`（TotalMemoryMb、UsedMemoryMb、CpuUsagePercent、TopProcesses）
- CPU 使用率通过两次采样 `TotalProcessorTime` 差值计算
- 内存通过 `Process.GetProcesses().WorkingSet64` 汇总
- 不依赖 WMI，纯 `System.Diagnostics`

**WinUI 层**：
- 新增 `ViewModels/SystemMonitorViewModel.cs`（用 CommunityToolkit.Mvvm）
- 修改 `OverviewPage.xaml`：MetricsRow 上方新增资源监控卡片
  - CPU / 内存两个大数字指标 + `ProgressBar` 比例条
  - Top 5 资源占用进程列表
- Timer 每 2 秒刷新，页面加载/卸载启停

#### A2. 开机自启项扫描与管理

**痛点**：用户想管理开机启动项，对标 Winhance

**Core 层**：
- 新增 `src/OneClickClose.Core/StartupScanner.cs`
- 三合一扫描：注册表 Run 键（HKCU/HKLM）、启动文件夹、计划任务
- 数据模型 `StartupItem`（Name、Command、Location、Source、IsEnabled）
- 禁用方式：注册表值重命名 / 移动到 Disabled 文件夹 / `schtasks /change /disable`

**WinUI 层**：
- 新增 `Pages/StartupPage.xaml(.cs)` + `ViewModels/StartupItemViewModel.cs`
- 页面：扫描按钮 + ToggleSwitch 启用/禁用 + 来源筛选 + 右键菜单
- MainWindow.xaml 新增"自启管理"导航项（Glyph `&#xE7C3;`）

#### A3. Windows 遥测/AI 后台服务识别与关闭建议

**痛点**：用户想关遥测/AI 服务但不知道怎么关，对标 Winhance 程序清理

**Core 层**：
- 新增 `src/OneClickClose.Core/TelemetryServiceScanner.cs`
- 预定义知识库（DiagTrack、SysMain、WSearch、CopilotUtilities、Recall 等十余项）
- 每项含：ServiceName、DisplayName、Description、RiskLevel、RecommendedAction
- 用 `ServiceController` 检查运行状态，注册表检查启动类型

**WinUI 层**：
- 新增 `Pages/SystemSlimPage.xaml(.cs)` + `ViewModels/TelemetryServiceViewModel.cs`
- 页面：扫描摘要 + ToggleSwitch 列表 + "一键禁用安全项"按钮 + 安全提示 InfoBar
- MainWindow.xaml 新增"系统瘦身"导航项

---

### 第二梯队：体验提升

#### A4. 进程树可视化

**痛点**：用户不理解进程间关系

**Core 层**：
- 新增 `src/OneClickClose.Core/ProcessTreeBuilder.cs`
- 数据模型 `ProcessTreeNode`（Id、ProcessName、Children）
- 复用父进程 Map 构建树，以 explorer/services/svchost 为根

**WinUI 层**：
- 修改 `CandidateProcessesPage.xaml`：筛选栏新增"列表/树"视图切换
- 树视图用 WinUI 3 原生 `TreeView` 控件
- 新增 `ViewModels/ProcessTreeViewModel.cs`

#### A5. 定时/关机前自动清理触发器

**痛点**：关机等待，希望自动清理

**Core 层**：
- 新增 `src/OneClickClose.Core/AutoCleanupTrigger.cs`
- 三种触发方式：关机前（WM_QUERYENDSESSION）、定时、空闲检测
- AppConfig 扩展：`enableShutdownCleanup`、`enableIdleCleanup`、`idleMinutesThreshold`、`scheduledCleanupTime`

**WinUI 层**：
- 修改 `SettingsPage.xaml`：新增"自动清理"配置卡片
  - ToggleSwitch 关机前/空闲自动清理 + NumberBox + TimePicker
- NativeMethods 新增 CreateWindowEx P/Invoke（隐藏窗口接收系统消息）

#### A6. 批量规则导入/导出

**痛点**：方便分享配置

**Core 层**：
- AppConfig.cs 新增 `ExportConfig()` 和 `ImportConfig()` 方法
- 支持合并/替换两种导入模式

**WinUI 层**：
- SettingsPage.xaml 新增"导出配置"/"导入配置"按钮（FileSavePicker / FileOpenPicker）

---

### 第三梯队：技术基础改进

#### C1. 异步扫描

- `ProcessPlanner.cs` 新增 `GetClosePlanAsync()` → `Task.Run`
- `AppState.cs` 新增 `ScanAsync()`
- 所有 Page 调用改为 async

#### C2. WMI 查询优化

- `NativeMethods.cs` 新增 `NtQueryInformationProcess` P/Invoke
- `ProcessCollector.cs` 新增 `GetParentProcessIdFast()` 和 `GetParentProcessMapFast()`
- 回退到 WMI 保证兼容，预期从 500-800ms 降到 50-100ms

#### C3. 应用状态管理改进

- `AppState.cs` 重构为 `ObservableObject` 单例
- 使用 `[ObservableProperty]`、`[RelayCommand]` 源生成器
- 保留静态属性转发向后兼容，逐步迁移

#### C4. UI 反馈动画

- 扫描/执行状态切换添加淡入淡出动画（200ms）
- 新增 `Helpers/AnimationHelper.cs`
- 使用 WinUI 3 `ConnectedAnimationService` 页面过渡

---

## 三、实施顺序与依赖

```
C1 (异步扫描) ─┐
C2 (WMI优化)  ─┼─→ A1 (实时监控)
C3 (状态管理) ─┘       │
                        ↓
           A2 (自启管理)    A3 (遥测服务)
                        │
           A4 (进程树) ←──┘
                        │
           A5 (自动清理) ←── A6 (导入导出)
                        │
           B1-B6 (UI 优化随对应 A 项同步)
```

1. C1、C2、C3 最先实施（技术基础）
2. A1 依赖 C3（数据绑定）
3. A4 依赖 C2（快速父进程查询）
4. A5 依赖 NativeMethods 扩展
5. 每个 UI 优化随对应 Core 功能同步实施

---

## 四、新增文件清单

| 文件 | 说明 |
|------|------|
| `src/OneClickClose.Core/SystemMonitor.cs` | CPU/内存监控快照 |
| `src/OneClickClose.Core/StartupScanner.cs` | 自启项扫描与管理 |
| `src/OneClickClose.Core/TelemetryServiceScanner.cs` | 遥测/AI 服务知识库 |
| `src/OneClickClose.Core/ProcessTreeBuilder.cs` | 进程树构建 |
| `src/OneClickClose.Core/AutoCleanupTrigger.cs` | 自动清理触发器 |
| `src/OneClickClose.WinUI/Pages/StartupPage.xaml(.cs)` | 自启管理页面 |
| `src/OneClickClose.WinUI/Pages/SystemSlimPage.xaml(.cs)` | 系统瘦身页面 |
| `src/OneClickClose.WinUI/ViewModels/SystemMonitorViewModel.cs` | 资源监控 VM |
| `src/OneClickClose.WinUI/ViewModels/StartupItemViewModel.cs` | 自启项 VM |
| `src/OneClickClose.WinUI/ViewModels/TelemetryServiceViewModel.cs` | 遥测服务 VM |
| `src/OneClickClose.WinUI/ViewModels/ProcessTreeViewModel.cs` | 进程树 VM |
| `src/OneClickClose.WinUI/Helpers/AnimationHelper.cs` | 动画辅助 |

## 五、修改文件清单

| 文件 | 改动 |
|------|------|
| `Core/Models.cs` | 新增 SystemSnapshot、StartupItem、TelemetryServiceItem 等模型 |
| `Core/ProcessCollector.cs` | 新增 GetParentProcessIdFast()、GetParentProcessMapFast() |
| `Core/ProcessPlanner.cs` | 新增 GetClosePlanAsync()，替换 WMI 调用 |
| `Core/NativeMethods.cs` | 新增 NtQueryInformationProcess、CreateWindowEx P/Invoke |
| `Core/AppConfig.cs` | 新增自动清理配置字段、Import/Export 方法 |
| `WinUI/AppState.cs` | 重构为 ObservableObject 单例 |
| `WinUI/MainWindow.xaml(.cs)` | 新增两个导航项 + 路由 |
| `WinUI/Pages/OverviewPage.xaml(.cs)` | 新增资源监控卡片 + 动画 |
| `WinUI/Pages/CandidateProcessesPage.xaml(.cs)` | 新增树视图切换 |
| `WinUI/Pages/SettingsPage.xaml(.cs)` | 新增自动清理配置、导入导出 |
| `WinUI/Styles/Theme.xaml` | ProgressBar、TreeView 主题覆盖 |

---

## 六、风险与注意事项

1. **管理员权限**：自启管理（HKLM）和遥测服务禁用需要管理员权限，确认 `app.manifest` 的 `requestedExecutionLevel`
2. **服务禁用风险**：WSearch 禁用影响开始菜单搜索，UI 中必须明确提示
3. **关机前触发**：`WM_QUERYENDSESSION` 在 Fast Startup 下可能不可靠，需备选 EventLog 监听
4. **NtQueryInformationProcess**：未文档化 NtAPI，回退 WMI 确保安全
5. **CommunityToolkit.Mvvm**：需确保 partial class 和 Nullable 正确配置

---

## 七、验证方式

每个优化项完成后：
- 功能测试：手动触发对应操作验证结果
- 性能测试：C2 优化后对比扫描耗时
- UI 测试：在不同 DPI（125%/150%/200%）下验证布局
- 兼容测试：Windows 10 1809+ 和 Windows 11
