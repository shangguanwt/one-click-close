# Changelog

本项目所有重要变更记录于此。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [2.0.0] - 2026-06-30

里程碑版本：从 WinForms 单体迁移到 WinUI 3 + .NET 9 三项目架构，并新增系统监控与一键发布流程。

### 新增

- **WinUI 3 主程序**（`OneClickClose.WinUI`，Windows App SDK 2.2 / .NET 9）：Mica 材质、NavigationView 导航、Fluent 卡片与徽章的现代深色界面。
- **系统监控总览页**：CPU / 内存 / 温度 / 电源实时指标、内存占用环图、性能曲线。
- **启动项管理页** 与 **磁盘瘦身页**。
- **共享核心库** `OneClickClose.Core`：进程扫描、关闭逻辑、配置、用户偏好统一抽取，WinUI 与 WinForms 双前端复用。
- **标签触发的自动发布**：push `v*` tag → GitHub Actions 构建自包含 WinUI 3 包并上传 Release（见 `.github/workflows/release.yml`）。

### 优化

- 双版本配色统一，整体质感提升。
- WinForms 旧版重设计为「状态指挥中心」布局（墨绿铜色主题）。
- 自包含发布同时打包 .NET 与 Windows App SDK 运行时（`WindowsAppSDKSelfContained=true`），下载解压即用，无需手动安装 MSIX。
- 性能与内存优化。

### 修复

- 清理后自动重新扫描。
- 事件订阅泄漏。
- 构建脚本输出命名区分（WinUI 与 Legacy 产物不再冲突）。

### 重构

- 拆分 `ProcessPlanner` 巨型类为 5 个职责单一的类。
- 提取共享代码、消除重复模式、拆分巨型方法。

## [1.0.0] - 2026-06-08

- 首个发布版本（WinForms）。

[2.0.0]: https://github.com/shangguanwt/one-click-close/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/shangguanwt/one-click-close/releases/tag/v1.0.0
