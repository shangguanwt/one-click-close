# OneClickClose 注意事项 / Important Notes

> 中文为主 / English follows: 这份文档列出使用、发布和排查时最容易忽略的边界。  
> English: This document lists the boundaries that are easy to miss when using, publishing, or troubleshooting OneClickClose.

![上线体检 / Launch readiness](assets/readme-launch-readiness.png)

## 使用前请先确认 / Before You Use

| 注意事项 | English |
| --- | --- |
| OneClickClose 会读取本机进程信息，并可能在你确认后关闭用户软件。 | OneClickClose reads local process information and may close user apps after confirmation. |
| 请先把正在编辑、同步、会议、录屏、编译或下载的软件加入白名单。 | Add editors, sync tools, meetings, screen recorders, build tools, or downloaders to the allowlist first. |
| 不确定的软件先展开查看 PID、窗口标题和路径，不要只看进程名。 | Expand uncertain apps and check PID, window title, and path instead of only the process name. |
| 高风险、系统路径、白名单和只提示项不应被直接关闭。 | High-risk, system-path, allowlisted, and prompt-only items should not be closed directly. |

## 安全边界 / Safety Boundaries

- 清理动作必须保留确认弹窗。
- 本地习惯学习只影响建议和排序，不绕过确认。
- 白名单优先级高于自动检测、目标名单和关机清障规则。
- OneClickClose 自身、系统进程、Codex 工具进程和保护名单默认跳过。
- 如果某个软件被误判，请先加入白名单，再反馈具体进程名、路径和场景。

English: Confirmation, allowlist priority, and explainable risk labels are the main safety boundaries.

## 配置与数据位置 / Config And Data

运行时配置默认位于：

```text
%LocalAppData%\OneClickClose\close-user-apps.config.json
```

说明：

- Release 输出目录里的 `close-user-apps.config.json` 是模板。
- 首次运行会把模板复制到用户目录。
- 已存在的用户配置不会被覆盖。
- 清理历史、趋势数据和本地习惯也应保存在用户目录，避免污染 Release 解压目录。

English: The release config is a template. Runtime config lives under `%LocalAppData%\OneClickClose`.

## Release 包注意事项 / Release Package Notes

不要手动移动这些文件：

```text
OneClickClose.exe
app/
docs/
checksums/
```

原因：

- 根目录 `OneClickClose.exe` 是启动器。
- 真实 WinUI 主程序位于 `app/OneClickClose.WinUI.exe`。
- WinUI、Windows App SDK 和 .NET 依赖对相对路径敏感。
- `checksums/FILES.sha256` 可用于核对包内文件。

English: Keep the launcher, app folder, docs, and checksums together after extraction.

## 温度读取限制 / Temperature Limits

温度信息取决于硬件、驱动、权限和传感器暴露方式。

常见情况：

- 没有传感器：显示不可用原因，不影响进程清理。
- 普通权限不可读：可尝试管理员身份运行，但不强制要求。
- 部分机器只有 CPU 或 GPU 温度。
- WMI 回退结果可能为空或较慢。

## GitHub 公开前 / Before Publishing To GitHub

请确认不要提交：

- `work/`
- `output/`
- `outputs/`
- `release/`
- `test-results/`
- `.playwright-cli/`
- `bin/`、`obj/`
- 本机私有配置、token、日志、真实用户路径截图。

建议命令：

```powershell
git status --short
rg -n --hidden --glob '!work/**' --glob '!output/**' --glob '!outputs/**' --glob '!release/**' --glob '!**/bin/**' --glob '!**/obj/**' "(?i)(token|secret|password|api[_-]?key|bearer)"
```

## 出问题时怎么反馈 / How To Report Issues

请尽量提供：

- OneClickClose 版本或 commit。
- Windows 版本和 CPU 架构。
- 是否管理员身份运行。
- 相关进程名、风险原因和脱敏后的路径。
- 截图时请遮挡私人软件名、用户目录和聊天内容。

English: Include version, Windows build, privilege level, process name, reason label, and redacted screenshots.
