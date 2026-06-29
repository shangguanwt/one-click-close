# Release Checklist

发布 OneClickClose（WinUI 3 主力版本）到 GitHub Releases 的清单。

## 自动发布（推荐）

仓库带 `.github/workflows/release.yml`：**push 一个 `vX.Y.Z` tag 即自动构建并上传 Release**。

```powershell
# 1. 确认 main 已是要发布的代码且已 push
git push origin main

# 2. 打 tag 并 push（版本号会从 tag 注入构建）
git tag v2.0.0
git push origin v2.0.0
```

workflow 会在 `windows-latest` 上：

1. `dotnet` 9.0.x 还原 + `scripts/build-winui.ps1 -SelfContained -Version <tag> -Platform x64`
   构建**自包含**包（含 .NET 运行时 **和** Windows App SDK 运行时，用户解压即用）。
2. 用 `softprops/action-gh-release` 创建 Release `vX.Y.Z`，自动生成 release notes，
   附件上传 `release/OneClickClose.WinUI-<版本>-x64.zip`（及 `ui-preview.png` 若存在）。

发布后到仓库 **Releases** 页确认 tag、附件 zip、release notes 都在。

## 手动发布（备选）

需要本地构建并手动上传时：

```powershell
# 1. 自包含构建（含 WinApp SDK 运行时，下载即用）
.\scripts\build-winui.ps1 -SelfContained -Version 2.0.0 -Platform x64

# 2. 冒烟测试
.\release\OneClickClose.WinUI-2.0.0-x64\OneClickClose.WinUI.exe

# 3. 用 gh CLI 创建 Release 并上传产物
gh release create v2.0.0 `
  release\OneClickClose.WinUI-2.0.0-x64.zip `
  --title "v2.0.0" --generate-notes
```

## WinForms 旧版（可选）

旧版仅作兼容保留，正式发布不再附带。如需单独分发：

```powershell
.\scripts\build.ps1
# 产物：release\OneClickClose.Legacy.exe、release\OneClickCloseSetup.exe、release\OneClickClose.Legacy-<版本>.zip
```

注意：不要上传 `release\package\` 或 `release\*.TMP`，这些是被忽略的中间文件。
