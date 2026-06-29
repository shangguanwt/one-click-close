# OneClickClose UI 优化方案

> 顶层设计文档，供后续模型/开发按图施工。所有改动集中在两个文件：
> - `src/OneClickClose/MainForm.cs`
> - `src/OneClickClose/ModernControls.cs`
>
> 每条改动都标注了**位置**、**原代码**、**改后代码**、**原因**。

---

## 一、背景

当前 UI 已经是深色主题 + 自绘控件，结构没问题，但"不够高级"的根因有三类：

1. **遗留 Bug**：上一轮修改有 2 处漏改，导致部分文字不可见、侧边栏强调色仍是旧青色。
2. **配色不统一**：侧边栏/导航项用的是一套"纯灰"配色 `(26,26,26)/(45,45,45)/(96,205,255)`，而主体用的是"蓝灰"配色 `(26,29,35)/(91,138,247)`。两套色系并存，产生廉价、拼接的观感。
3. **缺少质感层次**：卡片是纯平填充，没有渐变、没有高光、没有阴影，色阶之间对比太弱，整体"发糊"。

本方案目标：**统一为一套蓝灰色系**，**补足层次质感**，让界面看起来像一个完整设计的产品，而不是控件堆叠。

---

## 二、🔴 必修 Bug（2 处漏改）

### Bug 1 — "最多显示 90 条"文字不可见

- **位置**：`MainForm.cs` 第 614 行（`BuildProtectedPage` 内）
- **现状**：颜色 `(70,70,70)` 是接近黑的灰，在深背景上几乎看不见（上一轮其它同类提示已改，这里漏了）。

```csharp
// 原
truncHint.ForeColor = Color.FromArgb(70, 70, 70);
// 改
truncHint.ForeColor = muted;   // (107,114,128)
```

### Bug 2 — 侧边栏导航强调色仍是青色

- **位置**：`MainForm.cs` 第 117–128 行（`InitializeComponent` 内）
- **现状**：`sidebar.AccentColor = primary;` 写在 `AddNavItem(...)` **之后**。但 `AddNavItem` 在创建每个 `NavItem` 时就把当时的 `AccentColor`（默认青色 `96,205,255`）拷给了 item。结果导航项的图标、激活竖条、文字高亮**仍是青色**，与主题蓝不一致。

```csharp
// 原顺序
sidebar = new SidebarPanel();
sidebar.AddNavItem("overview", "概览", "");
... // 其它 AddNavItem
sidebar.NavigationRequested += OnNavigationRequested;
sidebar.SidebarBackground = background;
sidebar.AccentColor = primary;     // ← 太晚了，item 已经拿到旧色
sidebar.BackColor = background;
sidebar.Dock = DockStyle.Fill;

// 改：先设主题色，再加导航项
sidebar = new SidebarPanel();
sidebar.SidebarBackground = background;
sidebar.BackColor = background;
sidebar.AccentColor = primary;      // ← 提前到 AddNavItem 之前
sidebar.AddNavItem("overview", "概览", "");
sidebar.AddNavItem("candidate", "候选进程", "");
sidebar.AddNavItem("protected", "保护列表", "");
sidebar.AddNavItem("log", "运行日志", "");
sidebar.AddNavItem("config", "配置", "");
sidebar.AddNavItem("learning", "学习建议", "");
sidebar.NavigationRequested += OnNavigationRequested;
sidebar.Dock = DockStyle.Fill;
```

---

## 三、🎨 配色统一（消除"两套色系"）

核心：把 `NavItem`、`SidebarPanel`、`TabBar` 三个控件的默认色，从"纯灰 + 青色"统一到主体的"蓝灰 + 主题蓝"。

### 3.1 NavItem 默认配色

- **位置**：`ModernControls.cs` 第 549–556 行（`NavItem` 构造函数）
- **原因**：当前悬停/激活是纯灰 `(45,45,45)/(50,50,50)`，与蓝灰主题割裂；文字色 `(180,180,180)` 偏暖灰。

```csharp
// 原
IdleBackground = Color.Transparent;
HoverBackground = Color.FromArgb(45, 45, 45);
ActiveBackground = Color.FromArgb(50, 50, 50);
PressedBackground = Color.FromArgb(56, 56, 56);
TextColor = Color.FromArgb(180, 180, 180);
ActiveTextColor = Color.White;
AccentColor = Color.FromArgb(96, 205, 255);
IconColor = Color.FromArgb(140, 140, 140);

// 改（蓝灰系）
IdleBackground = Color.Transparent;
HoverBackground = Color.FromArgb(40, 44, 56);
ActiveBackground = Color.FromArgb(46, 51, 66);
PressedBackground = Color.FromArgb(52, 57, 74);
TextColor = Color.FromArgb(160, 166, 178);
ActiveTextColor = Color.FromArgb(234, 237, 243);
AccentColor = Color.FromArgb(91, 138, 247);
IconColor = Color.FromArgb(120, 127, 140);
```

### 3.2 SidebarPanel 默认配色 + 分割线

- **位置**：`ModernControls.cs` 第 641–644 行（构造函数）

```csharp
// 原
SidebarBackground = Color.FromArgb(26, 26, 26);
TitleColor = Color.White;
VersionColor = Color.FromArgb(80, 80, 80);
AccentColor = Color.FromArgb(96, 205, 255);

// 改
SidebarBackground = Color.FromArgb(22, 25, 31);   // 比内容区(26,29,35)略深，制造纵深
TitleColor = Color.FromArgb(234, 237, 243);
VersionColor = Color.FromArgb(107, 114, 128);     // = muted，副标题/版本不再消失
AccentColor = Color.FromArgb(91, 138, 247);
```

- **位置**：`ModernControls.cs` 第 716–719 行（`SidebarPanel.OnPaint` 内的分割线）
- **原因**：分割线 `(40,40,40)` 是纯黑灰，和主题 border `(51,56,68)` 不统一。

```csharp
// 原
using (Pen divider = new Pen(Color.FromArgb(40, 40, 40)))
{
    e.Graphics.DrawLine(divider, 12, 72, Width - 12, 72);
}

// 改
using (Pen divider = new Pen(Color.FromArgb(45, 50, 62)))
{
    e.Graphics.DrawLine(divider, 16, 72, Width - 16, 72);
}
```

### 3.3 侧边栏右边缘加 1px 分隔线（区分侧栏与内容区）

- **位置**：`ModernControls.cs` `SidebarPanel.OnPaint` 末尾（约第 725 行 `v1.0` 绘制之后）
- **原因**：现在侧栏和内容区只靠背景色差区分，边界模糊；加一条细竖线立刻有"面板"感。

```csharp
// 在 OnPaint 方法 return 前追加
using (Pen edge = new Pen(Color.FromArgb(45, 50, 62)))
{
    e.Graphics.DrawLine(edge, Width - 1, 0, Width - 1, Height);
}
```

### 3.4 TabBar 默认配色

- **位置**：`ModernControls.cs` 第 760–766 行（`TabBar` 构造函数）
- **原因**：AccentColor 青色。注意 `MainForm.cs:806` 已经手动设了 `configTabBar.AccentColor = primary`，但把默认值也改对，避免别处复用时再踩坑。

```csharp
// 原
AccentColor = Color.FromArgb(96, 205, 255);
// 改
AccentColor = Color.FromArgb(91, 138, 247);
```

---

## 四、✨ 质感提升（让界面"高级"起来）

### 4.1 MiniMetric 指标卡：微渐变 + 顶部高光

- **位置**：`MainForm.cs` `MiniMetric` 方法，第 341–347 行
- **原因**：现在 4 张卡是纯平填充，毫无层次。加一个从上到下的细微渐变 + 顶部 1px 高光，立刻有玻璃/卡片质感。`RoundedPanel` 已支持 `UseGradient`、`DrawHighlight`、`FillColor2`，直接用。

```csharp
// 原
panel.FillColor = card;
panel.FillColor2 = card;
panel.BorderColor = border;
panel.Radius = 8;

// 改
panel.UseGradient = true;
panel.GradientMode = LinearGradientMode.Vertical;
panel.FillColor = Color.FromArgb(48, 53, 64);    // 上：略亮
panel.FillColor2 = Color.FromArgb(40, 44, 54);   // 下：略暗
panel.BorderColor = border;
panel.DrawHighlight = true;
panel.Radius = 10;
```

### 4.2 日志卡 / 通用卡：开启顶部高光

- **位置**：`MainForm.cs` `BuildLogSummary` 第 400–405 行 与 `MakeCard` 第 1339–1349 行
- **原因**：统一卡片质感。

`BuildLogSummary`：
```csharp
// 在 logCard 设置块里追加
logCard.DrawHighlight = true;
logCard.Radius = 10;
```

`MakeCard`：
```csharp
// 在 panel 设置块里追加
panel.DrawHighlight = true;
panel.Radius = 10;
```

### 4.3 RoundedPanel 高光更细腻

- **位置**：`ModernControls.cs` 第 92 行（`OnPaint` 内 `DrawHighlight` 分支）
- **原因**：当前高光 alpha=24 偏强，降一点更自然。

```csharp
// 原
using (Pen highlight = new Pen(Color.FromArgb(24, Color.White)))
// 改
using (Pen highlight = new Pen(Color.FromArgb(16, Color.White)))
```

### 4.4 NavItem 激活竖条加宽 + 圆角

- **位置**：`ModernControls.cs` 第 590–597 行（`NavItem.OnPaint` 的 active 竖条）
- **原因**：3px 直角竖条略单薄，加宽到 4px 并画成圆角小药丸更精致。

```csharp
// 原
if (active)
{
    Rectangle bar = new Rectangle(2, 8, 3, Height - 17);
    using (SolidBrush brush = new SolidBrush(AccentColor))
    {
        e.Graphics.FillRectangle(brush, bar);
    }
}

// 改
if (active)
{
    Rectangle bar = new Rectangle(2, 10, 4, Height - 20);
    using (GraphicsPath barPath = RoundedPanel.RoundedPath(bar, 2))
    using (SolidBrush brush = new SolidBrush(AccentColor))
    {
        e.Graphics.FillPath(brush, barPath);
    }
}
```

### 4.5 Action Badge 改半透明填充（减少刺眼、更精致）

- **位置**：`ModernControls.cs` `ProcessGridView.OnCellPainting` 第 386–396 行
- **原因**：当前徽章是 100% 实色 + 白字，在深色行里非常跳。改成"半透明同色填充 + 同色描边 + 亮色文字"的 tag 风格，更现代、更克制。

```csharp
// 原
string action = Convert.ToString(e.Value);
Color fill = ResolveActionColor(action);
Color fore = Color.White;
Rectangle badge = GetCenteredBadge(e.CellBounds, action);
using (GraphicsPath path = RoundedPanel.RoundedPath(badge, 11))
using (SolidBrush brush = new SolidBrush(fill))
{
    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
    e.Graphics.FillPath(brush, path);
}
TextRenderer.DrawText(e.Graphics, action, e.CellStyle.Font, badge, fore, ...);

// 改
string action = Convert.ToString(e.Value);
Color accent = ResolveActionColor(action);
Color fill = Color.FromArgb(38, accent);          // 半透明填充
Color fore = ControlPaint.Light(accent, 0.3f);    // 亮一点的同色文字
Rectangle badge = GetCenteredBadge(e.CellBounds, action);
using (GraphicsPath path = RoundedPanel.RoundedPath(badge, 11))
using (SolidBrush brush = new SolidBrush(fill))
using (Pen pen = new Pen(Color.FromArgb(90, accent)))
{
    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
    e.Graphics.FillPath(brush, path);
    e.Graphics.DrawPath(pen, path);
}
TextRenderer.DrawText(e.Graphics, action, e.CellStyle.Font, badge, fore, ...);
```

### 4.6 侧边栏标题前加品牌小圆点

- **位置**：`ModernControls.cs` `SidebarPanel.OnPaint`，第 704–708 行（标题绘制处）
- **原因**：纯文字标题缺少品牌感。在标题左侧画一个 AccentColor 实心圆点，是低成本高回报的"产品感"细节。注意标题文字 X 坐标要相应右移。

```csharp
// 原
using (SolidBrush titleBrush = new SolidBrush(TitleColor))
using (Font titleFont = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold))
{
    e.Graphics.DrawString(Program.DisplayName, titleFont, titleBrush, 16, 24);
}

// 改：先画圆点，标题右移到 30
using (SolidBrush dotBrush = new SolidBrush(AccentColor))
{
    e.Graphics.FillEllipse(dotBrush, 16, 30, 8, 8);
}
using (SolidBrush titleBrush = new SolidBrush(TitleColor))
using (Font titleFont = new Font("Microsoft YaHei UI", 12.5F, FontStyle.Bold))
{
    e.Graphics.DrawString(Program.DisplayName, titleFont, titleBrush, 30, 24);
}
// 副标题 X 也从 16 → 30（第 713 行）
e.Graphics.DrawString("后台进程管理工具", subFont, subBrush, 30, 50);
```

### 4.7 配置页输入框统一为圆角包裹

- **位置**：`MainForm.cs` `BuildListEditor` 第 908–913 行（三个名单的 `input` TextBox），以及 `BuildAdvancedTab` 里的 NumericUpDown
- **原因**：搜索框已经用 `MakeSearchBoxWrapped` 做了圆角包裹，但名单编辑区的输入框还是系统 `FixedSingle` 灰边框，风格不统一。复用已有的包裹思路。
- **做法**：把 `BuildListEditor` 里裸的 `input` 用一个 `RoundedPanel`（`FillColor=secondaryPanel, BorderColor=border, Radius=6`）包起来再加进 layout，`input.BorderStyle` 改 `None`、`Dock=Fill`。可直接抽一个 `WrapInput(TextBox)` 辅助方法复用。

```csharp
// 辅助方法（新增到 MainForm）
private RoundedPanel WrapInput(TextBox box)
{
    RoundedPanel wrap = new RoundedPanel();
    wrap.FillColor = secondaryPanel;
    wrap.BorderColor = border;
    wrap.Radius = 6;
    wrap.Dock = DockStyle.Fill;
    box.BorderStyle = BorderStyle.None;
    box.BackColor = secondaryPanel;
    box.Dock = DockStyle.Fill;
    box.Margin = new Padding(8, 6, 8, 6);
    wrap.Controls.Add(box);
    return wrap;
}
// 然后 BuildListEditor 里：
// layout.Controls.Add(input, 0, 3);  →  layout.Controls.Add(WrapInput(input), 0, 3);
```

---

## 五、可选增强（锦上添花，非必须）

| 项 | 位置 | 说明 |
|----|------|------|
| 窗口主背景微渐变 | `MainForm.cs` `InitializeComponent` | 给 `contentArea` 套一层从 `(28,31,38)` 到 `(24,27,33)` 的竖向渐变 Panel，避免大片纯色发闷 |
| 一键清理按钮做主色强调 | `MakeButton` 调用处 | 当前是红色 danger，可考虑改为"红色描边 + 危险图标"或在按钮上加轻微渐变，弱化"报错感" |
| 网格行高从 36 → 40 | `ApplyRowTone` / `RowTemplate.Height` | 行距更舒展，呼吸感更好 |
| 标题字体改用 `Segoe UI Semibold` 混排 | 各 18F Bold 标题 | 中文用雅黑、数字/英文用 Segoe UI，层次更专业（指标数值已用 Segoe UI） |

---

## 六、配色速查表（统一后的最终色板）

| 用途 | ARGB | 备注 |
|------|------|------|
| 窗口背景 | `26,29,35` | background |
| 侧栏背景 | `22,25,31` | 比内容区略深 |
| 二级面板 | `34,38,47` | secondaryPanel |
| 卡片 | `42,46,56` | card |
| 卡片渐变上 / 下 | `48,53,64` / `40,44,54` | MiniMetric 用 |
| 边框 | `51,56,68` | border |
| 分割线（侧栏） | `45,50,62` | |
| 行悬停 | `47,52,68` | rowHover |
| 主题蓝 | `91,138,247` | primary / Accent（**全局统一**） |
| 主题蓝亮 | `111,153,255` | primaryHover |
| 正文 | `200,205,216` | text |
| 次要文字 | `107,114,128` | muted（替换所有 `70,70,70`/`80,80,80`） |
| 标题文字 | `234,237,243` | titleText |
| 成功绿 | `61,184,122` | protect |
| 警告橙 | `240,145,58` | force |
| 危险红 | `224,85,85` | danger |
| 紫 | `155,127,232` | purple |

> 关键原则：**全局只有一个强调色（主题蓝 `91,138,247`），所有青色 `96,205,255` 一律删除；所有 `(70,70,70)`/`(80,80,80)` 暗灰文字一律换成 `muted`。**

---

## 七、验证清单

改完后按以下步骤自检（用 `scripts/build.ps1` 编译，运行 `OneClickClose.exe`）：

1. **侧边栏**：导航项图标、激活竖条、选中文字是否都是**蓝色**（不再有青色）。
2. **副标题/版本号/"最多显示90条"**：是否都清晰可见（不再隐没在背景里）。
3. **概览页 4 张指标卡**：是否有从上到下的细微渐变和顶部高光。
4. **候选进程动作徽章**：是否是半透明 tag 风格（不再是刺眼实色块）。
5. **侧栏与内容区之间**：是否有一条细分隔线。
6. **配置页输入框**：圆角风格是否和搜索框一致。
7. 整体扫一眼：**有没有还残留青色**、有没有还看不清的灰字。
