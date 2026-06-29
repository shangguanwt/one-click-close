using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace OneClickClose.Core;

/// <summary>
/// 表示一个启动项。
/// </summary>
public sealed class StartupItem
{
    /// <summary>
    /// 启动项名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 启动项命令（可执行路径及参数）。
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// 启动项所在位置的完整路径（注册表路径 / 文件夹路径 / 计划任务名称）。
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// 启动项来源：<c>"注册表"</c>、<c>"启动文件夹"</c> 或 <c>"计划任务"</c>。
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 该启动项当前是否处于启用状态。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 该启动项是否属于当前用户。
    /// </summary>
    public bool IsCurrentUser { get; set; }
}

/// <summary>
/// 启动项扫描器，从注册表、启动文件夹和计划任务中收集 Windows 启动项，
/// 并支持启用 / 禁用操作。
/// </summary>
public static class StartupScanner
{
    // ── 注册表 Run / RunOnce 键路径 ─────────────────────────────────────
    private static readonly (string Path, bool IsCurrentUser)[] RegistryKeys =
    [
        (@"Software\Microsoft\Windows\CurrentVersion\Run",          true),
        (@"Software\Microsoft\Windows\CurrentVersion\Run",          false),
        (@"Software\Microsoft\Windows\CurrentVersion\RunOnce",       true),
        (@"Software\Microsoft\Windows\CurrentVersion\RunOnce",       false),
    ];

    // ── 启动文件夹 ──────────────────────────────────────────────────────
    private static readonly (string Folder, bool IsCurrentUser)[] StartupFolders =
    [
        (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup"),      true),
        (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Microsoft\Windows\Start Menu\Programs\Startup"),      false),
    ];

    // ───────────────────────────────────────────────────────────────────
    //  公共 API
    // ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// 扫描全部来源（注册表、启动文件夹、计划任务），返回合并后的启动项列表。
    /// </summary>
    /// <returns>所有发现的启动项。</returns>
    public static List<StartupItem> ScanAll()
    {
        var items = new List<StartupItem>();
        items.AddRange(ScanRegistry());
        items.AddRange(ScanStartupFolders());
        items.AddRange(ScanScheduledTasks());
        return items;
    }

    /// <summary>
    /// 仅扫描注册表 Run / RunOnce 键中的启动项。
    /// </summary>
    /// <returns>注册表启动项列表。</returns>
    public static List<StartupItem> ScanRegistry()
    {
        var items = new List<StartupItem>();

        foreach (var (subKeyPath, isCurrentUser) in RegistryKeys)
        {
            try
            {
                using var baseKey = isCurrentUser
                    ? Registry.CurrentUser
                    : Registry.LocalMachine;

                using var subKey = baseKey.OpenSubKey(subKeyPath);
                if (subKey is null) continue;

                foreach (var valueName in subKey.GetValueNames())
                {
                    // 已禁用项的名称以 "_" 前缀标记
                    bool disabled = valueName.StartsWith('_');
                    string originalName = disabled ? valueName.Substring(1) : valueName;

                    var value = subKey.GetValue(valueName) as string;
                    if (value is null) continue;

                    string hiveName = isCurrentUser ? "HKCU" : "HKLM";
                    items.Add(new StartupItem
                    {
                        Name = originalName,
                        Command = value,
                        Location = $@"{hiveName}\{subKeyPath}\{valueName}",
                        Source = "注册表",
                        IsEnabled = !disabled,
                        IsCurrentUser = isCurrentUser,
                    });
                }
            }
            catch
            {
                // 忽略无权限或键不存在的异常
            }
        }

        return items;
    }

    /// <summary>
    /// 扫描启动文件夹中的快捷方式文件。
    /// </summary>
    /// <returns>启动文件夹中的启动项列表。</returns>
    public static List<StartupItem> ScanStartupFolders()
    {
        var items = new List<StartupItem>();

        foreach (var (folder, isCurrentUser) in StartupFolders)
        {
            try
            {
                if (!Directory.Exists(folder)) continue;

                // 同时检查 Disabled 子文件夹中已禁用的快捷方式
                var enabledDir = folder;
                var disabledDir = Path.Combine(folder, "Disabled");
                ScanFolderDir(enabledDir, disabledDir, isCurrentUser, items);

                // 如果 Disabled 子文件夹不存在则跳过
                if (Directory.Exists(disabledDir))
                {
                    ScanFolderDir(disabledDir, disabledDir, isCurrentUser, items, disabled: true);
                }
            }
            catch
            {
                // 忽略无权限等异常
            }
        }

        return items;
    }

    /// <summary>
    /// 扫描计划任务中包含"登录时"触发器的任务。
    /// </summary>
    /// <returns>计划任务启动项列表。</returns>
    public static List<StartupItem> ScanScheduledTasks()
    {
        var items = new List<StartupItem>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = "/query /fo CSV /v",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return items;

            using var reader = proc.StandardOutput;
            bool headerSkipped = false;

            while (reader.ReadLine() is { } line)
            {
                if (!headerSkipped)
                {
                    headerSkipped = true;
                    continue; // 跳过 CSV 表头
                }

                // schtasks CSV 字段用逗号分隔，可能带双引号
                var fields = ParseCsvLine(line);
                if (fields.Length < 9) continue;

                string taskName = Unquote(fields[1].Trim());
                string trigger = Unquote(fields[7].Trim());
                string status = Unquote(fields[8].Trim());
                string nextRun = fields.Length > 11 ? Unquote(fields[11].Trim()) : "";

                // 只保留包含"登录"触发器的任务
                if (!trigger.Contains("At log on") && !trigger.Contains("At logon")) continue;

                bool disabled = status.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

                items.Add(new StartupItem
                {
                    Name = taskName,
                    Command = nextRun,          // 计划任务的"下次运行时间"字段，无命令则留空
                    Location = taskName,
                    Source = "计划任务",
                    IsEnabled = !disabled,
                    IsCurrentUser = false,       // 计划任务暂不区分当前用户
                });
            }

            proc.WaitForExit();
        }
        catch
        {
            // 忽略 schtasks 不可用等异常
        }

        return items;
    }

    /// <summary>
    /// 禁用指定的启动项。
    /// <list type="bullet">
    ///   <item>注册表项：在值名称前添加 "_" 前缀。</item>
    ///   <item>启动文件夹：将快捷方式移动到 Disabled 子文件夹。</item>
    ///   <item>计划任务：执行 schtasks /change /disable。</item>
    /// </list>
    /// </summary>
    /// <param name="item">要禁用的启动项。</param>
    /// <returns>操作是否成功。</returns>
    public static bool DisableItem(StartupItem item)
    {
        try
        {
            switch (item.Source)
            {
                case "注册表":
                    return DisableRegistryItem(item);
                case "启动文件夹":
                    return DisableFolderItem(item);
                case "计划任务":
                    return DisableScheduledTask(item);
            }
        }
        catch
        {
            // 忽略异常，返回失败
        }

        return false;
    }

    /// <summary>
    /// 启用指定的启动项（禁用操作的逆操作）。
    /// <list type="bullet">
    ///   <item>注册表项：移除值名称前的 "_" 前缀。</item>
    ///   <item>启动文件夹：将快捷方式从 Disabled 子文件夹移回原目录。</item>
    ///   <item>计划任务：执行 schtasks /change /enable。</item>
    /// </list>
    /// </summary>
    /// <param name="item">要启用的启动项。</param>
    /// <returns>操作是否成功。</returns>
    public static bool EnableItem(StartupItem item)
    {
        try
        {
            switch (item.Source)
            {
                case "注册表":
                    return EnableRegistryItem(item);
                case "启动文件夹":
                    return EnableFolderItem(item);
                case "计划任务":
                    return EnableScheduledTask(item);
            }
        }
        catch
        {
            // 忽略异常，返回失败
        }

        return false;
    }

    // ───────────────────────────────────────────────────────────────────
    //  内部辅助方法
    // ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// 扫描单个目录中的快捷方式文件。
    /// </summary>
    private static void ScanFolderDir(string dir, string disabledDir, bool isCurrentUser,
        List<StartupItem> items, bool disabled = false)
    {
        foreach (var file in Directory.GetFiles(dir, "*.lnk"))
        {
            var fileInfo = new FileInfo(file);
            string parentFolder = Path.GetDirectoryName(dir)!;

            items.Add(new StartupItem
            {
                Name = fileInfo.Name,
                Command = fileInfo.FullName,
                Location = disabled ? disabledDir : parentFolder,
                Source = "启动文件夹",
                IsEnabled = !disabled,
                IsCurrentUser = isCurrentUser,
            });
        }
    }

    /// <summary>
    /// 解析带引号的 CSV 行（简单实现，适用于 schtasks 输出）。
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    /// <summary>
    /// 去除字段两端的英文双引号。
    /// </summary>
    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1];
        return value;
    }

    // ── 注册表禁用 / 启用 ────────────────────────────────────────────────

    private static (RegistryKey? BaseKey, string SubKeyPath, string ValueName) ParseRegistryLocation(
        StartupItem item)
    {
        string location = item.Location;

        bool isHkcu = location.StartsWith("HKCU\\");
        bool isHklm = location.StartsWith("HKLM\\");
        if (!isHkcu && !isHklm) return (null, string.Empty, string.Empty);

        string hivePart = isHkcu ? "HKCU\\" : "HKLM\\";
        string rest = location[hivePart.Length..];

        // rest 格式: Software\Microsoft\...\RunOnce\ValueName
        int lastBackslash = rest.LastIndexOf('\\');
        if (lastBackslash < 0) return (null, string.Empty, string.Empty);

        string subKeyPath = rest[..lastBackslash];
        string valueName = rest[(lastBackslash + 1)..];

        var baseKey = isHkcu ? Registry.CurrentUser : Registry.LocalMachine;
        return (baseKey, subKeyPath, valueName);
    }

    private static bool DisableRegistryItem(StartupItem item)
    {
        var (baseKey, subKeyPath, valueName) = ParseRegistryLocation(item);
        if (baseKey is null) return false;

        using var subKey = baseKey.OpenSubKey(subKeyPath, writable: true);
        if (subKey is null) return false;

        string value = subKey.GetValue(valueName) as string ?? string.Empty;
        if (string.IsNullOrEmpty(value)) return false;

        // 写入带 "_" 前缀的新值
        subKey.SetValue("_" + valueName, value);

        // 删除原始值
        subKey.DeleteValue(valueName);

        // 更新 Location 以反映新名称
        string hiveName = item.Location.StartsWith("HKCU") ? "HKCU" : "HKLM";
        item.Location = $@"{hiveName}\{subKeyPath}\_{valueName}";
        item.IsEnabled = false;

        return true;
    }

    private static bool EnableRegistryItem(StartupItem item)
    {
        var (baseKey, subKeyPath, valueName) = ParseRegistryLocation(item);
        if (baseKey is null) return false;

        // 值名应以 "_" 前缀开头（被禁用状态）
        if (!valueName.StartsWith('_')) return false;

        using var subKey = baseKey.OpenSubKey(subKeyPath, writable: true);
        if (subKey is null) return false;

        string value = subKey.GetValue(valueName) as string ?? string.Empty;
        if (string.IsNullOrEmpty(value)) return false;

        string originalName = valueName[1..];

        // 恢复原始值名
        subKey.SetValue(originalName, value);
        subKey.DeleteValue(valueName);

        // 更新 Location
        string hiveName = item.Location.StartsWith("HKCU") ? "HKCU" : "HKLM";
        item.Location = $@"{hiveName}\{subKeyPath}\{originalName}";
        item.IsEnabled = true;

        return true;
    }

    // ── 启动文件夹禁用 / 启用 ──────────────────────────────────────────

    private static string GetParentStartupFolder(string filePath)
    {
        // filePath 是 .lnk 文件的完整路径
        // 向上找到 Startup 文件夹
        var dir = Path.GetDirectoryName(filePath);
        if (dir is null) return filePath;

        // 如果已经在 Disabled 子文件夹中，再上一级就是 Startup 文件夹
        string dirName = new DirectoryInfo(dir).Name;
        if (dirName.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
            dir = Path.GetDirectoryName(dir);

        return dir ?? filePath;
    }

    private static bool DisableFolderItem(StartupItem item)
    {
        string filePath = item.Command;
        if (!File.Exists(filePath)) return false;

        string startupFolder = GetParentStartupFolder(filePath);
        string disabledDir = Path.Combine(startupFolder, "Disabled");

        Directory.CreateDirectory(disabledDir);

        string fileName = Path.GetFileName(filePath);
        string destPath = Path.Combine(disabledDir, fileName);

        // 如果目标已存在则不覆盖
        if (File.Exists(destPath)) return false;

        File.Move(filePath, destPath);

        item.Command = destPath;
        item.Location = disabledDir;
        item.IsEnabled = false;

        return true;
    }

    private static bool EnableFolderItem(StartupItem item)
    {
        string filePath = item.Command;
        if (!File.Exists(filePath)) return false;

        // 确认文件在 Disabled 子文件夹中
        string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        string dirName = new DirectoryInfo(dir).Name;

        if (!dirName.Equals("Disabled", StringComparison.OrdinalIgnoreCase)) return false;

        string startupFolder = Path.GetDirectoryName(dir) ?? dir;
        string fileName = Path.GetFileName(filePath);
        string destPath = Path.Combine(startupFolder, fileName);

        // 如果目标已存在则不覆盖
        if (File.Exists(destPath)) return false;

        File.Move(filePath, destPath);

        item.Command = destPath;
        item.Location = startupFolder;
        item.IsEnabled = true;

        return true;
    }

    // ── 计划任务禁用 / 启用 ────────────────────────────────────────────

    private static bool DisableScheduledTask(StartupItem item)
    {
        return RunSchtasksChange(item.Name, "/disable");
    }

    private static bool EnableScheduledTask(StartupItem item)
    {
        return RunSchtasksChange(item.Name, "/enable");
    }

    private static bool RunSchtasksChange(string taskName, string flag)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks",
                Arguments = $"/change /tn \"{taskName}\" {flag}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return false;

            proc.WaitForExit(10000);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
