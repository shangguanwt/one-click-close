# 一键关闭 — 全面 UI 精细化重设计方案

## 概述

根据用户提供的高保真设计参考图和详细设计规范，对 OneClickClose 进行全面的视觉改造。目标是从"学生作品"提升到"付费软件"品质，融合 Apple 极简、Fluent Design、Linear Dashboard、Raycast 等设计风格。

**设计关键词**: Minimal, Premium, Glassmorphism, Soft UI, Rounded, Spacing First, Dashboard, High-end

---

## 一、当前状态分析

### 现有 UI 问题
1. **导航栏**: 宽 180px 文字展开式，视觉上过于拥挤、不够现代
2. **总览页**: 缺少 Hero 区域，没有视觉中心点，统计数字不够有冲击力
3. **色彩**: 灰色调偏暖（Apple 灰），需改为深蓝灰冷色调（参考图 #111315）
4. **圆角**: 12px 偏小，设计规范要求 12-20px（卡片 20px）
5. **按钮**: 缺少品牌色主按钮，"一键清理"使用铜色而非蓝色
6. **缺少**: 圆环资源图、Hero 区域、性能监控图表、Toast 组件

### 现有文件清单
- `Theme.xaml` — 全局主题资源
- `MainWindow.xaml(.cs)` — 导航壳 + 入场引导
- `Pages/OverviewPage.xaml(.cs)` — 总览仪表板
- `Pages/CandidateProcessesPage.xaml(.cs)` — 候选进程列表
- `Pages/ProtectedProcessesPage.xaml(.cs)` — 保护进程列表
- `Pages/LogsPage.xaml(.cs)` — 运行日志
- `Pages/LearningPage.xaml(.cs)` — 学习建议
- `Pages/SettingsPage.xaml(.cs)` — 设置页
- `Pages/StartupPage.xaml(.cs)` — 自启管理（新增）
- `Pages/SystemSlimPage.xaml(.cs)` — 系统瘦身（新增）
- `ViewModels/` — 7 个 ViewModel
- `AppState.cs` — 全局状态
- `ColorHelper.cs` — 颜色辅助
- `Helpers/AnimationHelper.cs` — 动画辅助

---

## 二、设计方案

### 2.1 色彩系统（全新，对标设计规范）

| 角色 | 色值 | 用途 |
|------|------|------|
| **背景底色** | `#111315` | 窗口/页面最深底色 |
| **内容区** | `#15171A` | 内容面板背景 |
| **卡片** | `#1A1D21` | 卡片/容器背景 |
| **悬浮** | `#23272F` | Hover 状态 |
| **品牌蓝 Primary** | `#3B82F6` | 主按钮、选中态、链接 |
| **品牌蓝浅 Accent** | `#60A5FA` | 辅助强调 |
| **成功绿** | `#22C55E` | 安全/成功状态 |
| **警告黄** | `#F59E0B` | 警告/中等风险 |
| **危险红** | `#EF4444` | 危险/高风险 |
| **文字主色** | `#FFFFFF` | 标题/主文字 |
| **文字辅助** | `#A1A1AA` | 正文/次要信息 |
| **文字提示** | `#71717A` | 占位符/提示文字 |
| **分隔线** | `#FFFFFF` Opacity 8% | 分隔线 |

### 2.2 字体规范

| 层级 | 字号 | 字重 | 用途 |
|------|------|------|------|
| 页面标题 | 28px | SemiBold | 页面顶部标题 |
| 模块标题 | 18px | SemiBold | 卡片内区域标题 |
| 大数字 | 36px | Bold | 统计卡片数字 |
| 正文 | 15px | Regular | 内容正文 |
| 辅助信息 | 13px | Regular | 次要信息 |
| 按钮 | 14px | SemiBold | 按钮文字 |
| 小标签 | 12px | Medium | 标签/徽章 |

### 2.3 圆角规范

- 卡片: `20px`
- 按钮: `16px`
- 弹窗/对话框: `24px`
- 标签/徽章: `8px`
- 进度条: `9999px`（全圆角）

### 2.4 间距规范（8pt Grid）

采用 8pt 基础网格：4, 8, 12, 16, 20, 24, 32, 40, 48, 64, 80, 96

---

## 三、实施步骤（按文件分组）

### 阶段 1：主题基础设施

#### 步骤 1.1 — 重写 `Theme.xaml`
**文件**: `src/OneClickClose.WinUI/Styles/Theme.xaml`

**改动要点**:
- 所有颜色按 2.1 色彩系统替换
- 新增 brush: `PrimaryBrush` (#3B82F6), `AccentLightBrush` (#60A5FA), `CardBrush` (#1A1D21), `HoverBrush` (#23272F), `DividerBrush`（白色 8% 透明）
- 保留旧 brush key 作为别名（`SafeBrush` → 映射到 `#22C55E`，`AccentBrush` → 映射到 `#3B82F6`）
- 更新所有 Style: CardBorderStyle CornerRadius 20, PrimaryButtonStyle 改为蓝色品牌按钮 220×60 规格, MetricNumberStyle 36px Bold
- 移除 CharacterSpacing 负值（避免中文重叠）
- 新增 Style: `HeroTitleStyle`, `StatCardStyle`, `ProcessRowStyle`, `StatusTagStyle`, `GlassBorderStyle`
- 新增 LinearGradientBrush: `PrimaryGradientBrush`（蓝→紫渐变，用于圆环和主按钮）

#### 步骤 1.2 — 新增 `AppStyles.xaml`
**文件**: `src/OneClickClose.WinUI/Styles/AppStyles.xaml`（新建）

独立的复杂样式资源字典，包含：
- `ContentDialog` 样式覆盖（圆角 24px，背景 #1A1D21）
- `InfoBar` 样式覆盖（圆角 12px，匹配设计语言）
- `ProgressBar` 样式覆盖（圆角全圆，品牌蓝色）
- `ToggleSwitch` 样式覆盖
- `NavigationViewItem` 样式微调

### 阶段 2：导航壳

#### 步骤 2.1 — 重写 `MainWindow.xaml`
**文件**: `src/OneClickClose.WinUI/MainWindow.xaml`

**改动要点**:
- 窗口默认尺寸改为 `1280x800`
- `MicaBackdrop Kind="Base"`（非 BaseAlt，让 Mica 效果更明显）
- NavigationView 改为 `PaneDisplayMode="LeftCompact"`，`OpenPaneLength="96"`，`CompactPaneLength="48"`
  - 默认只显示图标，Hover 展开文字标签（Raycast 风格）
  - 选中项使用品牌蓝左指示条（而非圆点）
  - Pane 背景透明，让 Mica 透出
- 重新组织导航项（对齐设计稿）：
  - 总览 → 一键关闭 → 后台进程 → 白名单 → 性能监控 → 清理记录 → 设置
  - 移除"学习建议"（功能弱化，可合并到其他页面）
  - "自启管理"和"系统瘦身"合并到设置页面的子功能
  - 底部：版本号 v1.0.0
- ContentFrame Padding 改为 `32,24,32,32`
- 移除 Onboarding overlay（改为首次打开时在 OverviewPage 内显示引导卡片）
- 顶部 TitleBar: Logo + "一键关闭" + Slogan + 右侧实时 CPU/内存小指标

#### 步骤 2.2 — 更新 `MainWindow.xaml.cs`
**文件**: `src/OneClickClose.WinUI/MainWindow.xaml.cs`

- 导航路由更新（移除 learning/startup/systemslim，增加 performance/history）
- TitleBar 区域增加实时性能数据绑定
- 移除 Onboarding 逻辑（迁移到 OverviewPage）

### 阶段 3：总览仪表板（核心页面，最大改动）

#### 步骤 3.1 — 重写 `OverviewPage.xaml`
**文件**: `src/OneClickClose.WinUI/Pages/OverviewPage.xaml`

**完整重构布局**（从上到下）：

**1. Hero 区域**（新增）:
```
┌─────────────────────────────────────────────────┐
│ [标题] 释放后台进程，让电脑恢复最佳状态          │
│ [描述] 已检测到 112 个可优化项目，预计释放 4.6 GB │
│ [主按钮] 立即优化 ⚡  (220×60, 品牌蓝渐变)      │
│                                                  │
│            ┌──── 圆环图 ────┐                     │
│            │  Memory        │                     │
│            │  18.0 / 32 GB   │                     │
│            └────────────────┘                     │
│     CPU 23%    GPU 41%    Disk 18%    Network 3.2M│
└─────────────────────────────────────────────────┘
```
- 左侧：标题 + 描述 + 主按钮
- 右侧：大圆环进度图（`Canvas` + `Ellipse` + `ArcSegment` 实现，渐变蓝→紫）
- 下方四个小指标横排（CPU/GPU/Disk/Network 各带迷你进度条）

**2. 四张统计卡片**（替代 MetricsRow）:
```
┌──待关闭──┐ ─ ┌──预计释放──┐ ─ ┌──后台软件──┐ ─ ┌──启动项──┐
│  ●  112  │   │  ● 4.6 GB │   │  ●  37     │   │  ●  18   │
│  ↓12%较上次│   │  ↑16%较上次│   │  ↓5%较上次 │   │  ↓8%较上次│
└──────────┘   └────────────┘   └───────────┘   └─────────┘
```
- 每张卡片: 20px 圆角，图标 + 大数字（36px Bold）+ 标题 + 趋势箭头
- 背景 #1A1D21，Hover 时 #23272F + 轻微 elevation
- 用 `GridView` 或 4 列 `Grid`

**3. 进程列表**（替代双栏布局的左半部分）:
- 标题"后台进程"
- 现代列表样式（非 Table），每项：
  ```
  [Icon] Chrome.exe        850 MB    CPU 2.3%    ● 运行中    [关闭]
  ```
- 无分割线，利用留白 + Hover 背景 #23272F 区分
- 右侧"关闭"按钮：品牌蓝色小按钮
- 风险提示：黄色 Pill 标签"系统关键进程 · 建议保留"

**4. 移除**：双栏布局、日志面板（日志移到独立的"清理记录"页面）

#### 步骤 3.2 — 更新 `OverviewPage.xaml.cs`
**文件**: `src/OneClickClose.WinUI/Pages/OverviewPage.xaml.cs`

- 新增圆环进度控件数据绑定（自定义 UserControl 或 Canvas 绑定）
- 统计卡片数据填充逻辑
- 进程列表改为扁平列表（不再分两栏）
- 保留扫描/执行/取消逻辑
- 移除日志相关代码

#### 步骤 3.3 — 新增圆环控件
**文件**: `src/OneClickClose.WinUI/Controls/MemoryRingControl.xaml(.cs)`（新建）

使用 `Canvas` + `Path`（`ArcSegment`）实现 Fluent Ring 风格的内存占用圆环：
- 底圈灰色弧线 + 上圈蓝紫渐变弧线
- 中心文字：内存使用量 + 总量
- `DependencyProperty`: `Value`（0-100），`UsedText`，`TotalText`
- 动画：值变化时弧线平滑过渡

### 阶段 4：其余页面适配

#### 步骤 4.1 — 候选进程页 `CandidateProcessesPage.xaml`
- 应用新色彩和卡片样式
- 列表项改为现代扁平列表（与 Hero 区域进程列表样式统一）
- 移除表格样式，改为进程行样式
- 筛选栏改为 pill 按钮组
- 搜索框样式更新

#### 步骤 4.2 — 保护进程页 `ProtectedProcessesPage.xaml`
- 白名单改为 Tag/Chip 展示方式
- 每项: 图标 + 名称 + 来源 + ToggleSwitch
- 搜索功能保留

#### 步骤 4.3 — 设置页 `SettingsPage.xaml`
- 合并"自启管理"和"系统瘦身"为设置页的子 section
- 分组: 通用 / 自动清理 / 白名单管理 / 自启管理 / 系统瘦身 / 外观 / 关于
- 采用 Preference Pane 风格（Notion 设置页风格）
- 每项: 标题 + 描述 + 右侧控件（ToggleSwitch/TimePicker/NumberBox/按钮）
- 导入导出按钮组

#### 步骤 4.4 — 移除/合并页面
- `LearningPage` → 移除导航入口（功能弱，学习建议改为 OverviewPage 内的提示卡片）
- `StartupPage` → 合并到 SettingsPage 内的 section
- `SystemSlimPage` → 合并到 SettingsPage 内的 section

#### 步骤 4.5 — 日志页 → 改为"清理记录"页 `LogsPage.xaml`
- 应用新色彩
- 每条记录显示：时间、释放内存、关闭数量、详情
- 顶部摘要统计卡片

#### 步骤 4.6 — 新增"性能监控"页 `PerformancePage.xaml(.cs)`（新建）
- 四个实时图表区域：CPU / Memory / Disk / Network
- 使用 Win2D `CanvasAnimatedControl` 绘制平滑曲线
- 或者用 `ItemsControl` + `Polyline` 实现简单折线图
- 顶部统计摘要

### 阶段 5：组件和动效

#### 步骤 5.1 — 更新 `AnimationHelper.cs`
- 新增 `CountUpAnimation(TextBlock, target, duration)` — 数字递增动画
- 新增 `RingAnimation(Path, targetValue)` — 圆环进度动画
- 优化现有动画的缓动函数

#### 步骤 5.2 — 更新 `ColorHelper.cs`
- 新增 `GetStatusTagColor(status)` → 返回绿色/黄色/红色/灰色
- 新增 `GetTrendArrow(isUp)` → 返回 "↑"/"↓" + 颜色
- 调整 `GetActionBackground` 以匹配新色彩

### 阶段 6：清理和验证

#### 步骤 6.1 — 清理未使用的资源
- 移除旧 LearningPage 的导航入口
- 确认所有 brush key 都有使用

#### 步骤 6.2 — 编译验证
- `dotnet build OneClickClose.sln` 确保 0 error
- 启动运行验证所有页面

---

## 四、新增文件清单

| 文件 | 说明 |
|------|------|
| `Styles/AppStyles.xaml` | 复杂控件样式覆盖 |
| `Controls/MemoryRingControl.xaml(.cs)` | 圆环内存占用控件 |
| `Pages/PerformancePage.xaml(.cs)` | 性能监控页面 |

## 五、修改文件清单

| 文件 | 改动 |
|------|------|
| `Styles/Theme.xaml` | 全部色彩/样式重写 |
| `MainWindow.xaml(.cs)` | 导航重构（图标优先/紧凑模式） |
| `Pages/OverviewPage.xaml(.cs)` | Hero + 统计卡片 + 圆环 + 进程列表 |
| `Pages/CandidateProcessesPage.xaml` | 现代列表样式 |
| `Pages/ProtectedProcessesPage.xaml` | Tag/Chip 白名单样式 |
| `Pages/SettingsPage.xaml(.cs)` | Preference Pane + 合并自启/瘦身 |
| `Pages/LogsPage.xaml` | 清理记录样式 |
| `AppState.cs` | 新增性能数据属性 |
| `Helpers/AnimationHelper.cs` | 新增数字/圆环动画 |
| `Helpers/ColorHelper.cs` | 新增状态色/趋势箭头方法 |
| `ViewModels/SystemMonitorViewModel.cs` | 扩展 GPU/Disk/Network 数据 |

## 六、假设与决策

1. **WinUI 3 NavigationView LeftCompact 模式**: 支持 Hover 展开。如果不支持完美的 Raycast 风格，退而求其次使用 Left 模式但保持 96px 图标优先宽度。
2. **圆环控件**: 用 `Path` + `ArcSegment` 实现，不引入第三方图表库。
3. **性能图表**: 用 Win2D Canvas 绘制平滑曲线。如果 Win2D 集成复杂度过高，改用 `Polyline` + `ItemsControl` 模拟。
4. **GPU 监控**: .NET 没有原生 GPU 使用率 API。初始版本显示 0% 或用 `PerformanceCounter` 尝试。
5. **移除学习建议**: 该功能实际价值低，在导航中移除入口但保留代码文件（不删除）。

## 七、验证方式

1. 编译: `dotnet build OneClickClose.sln` 0 error
2. 启动: 验证导航切换、页面加载
3. 视觉: 对比设计参考图，确认色彩/布局/圆角/间距一致
4. 功能: 扫描 → 执行 → 结果链路完整
5. 性能: 实时监控不卡顿（Timer 间隔 ≥ 2s）
