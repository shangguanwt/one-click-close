using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using Microsoft.Win32;

namespace OneClickClose.Core;

/// <summary>
/// 遥测/AI 服务风险等级。
/// </summary>
public enum RiskLevel
{
    /// <summary>低风险，可安全关闭。</summary>
    Low,

    /// <summary>中风险，关闭可能影响部分功能。</summary>
    Medium,

    /// <summary>高风险，不建议关闭。</summary>
    High
}

/// <summary>
/// 对遥测服务建议的操作类型。
/// </summary>
public enum ServiceAction
{
    /// <summary>可以禁用该服务。</summary>
    CanDisable,

    /// <summary>只能停止，不建议禁用。</summary>
    CanStopOnly,

    /// <summary>只读，不建议操作。</summary>
    ReadOnly
}

/// <summary>
/// 遥测服务静态定义信息（不可变）。
/// </summary>
/// <param name="ServiceName">服务内部名称。</param>
/// <param name="DisplayName">服务显示名称。</param>
/// <param name="Description">服务描述。</param>
/// <param name="RiskLevel">风险等级。</param>
/// <param name="RecommendedAction">建议操作。</param>
public record TelemetryServiceInfo(
    string ServiceName,
    string DisplayName,
    string Description,
    RiskLevel RiskLevel,
    ServiceAction RecommendedAction);

/// <summary>
/// 遥测服务扫描结果（可变）。
/// </summary>
public sealed class TelemetryServiceItem
{
    /// <summary>服务内部名称。</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>服务显示名称。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>服务描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>风险等级。</summary>
    public RiskLevel RiskLevel { get; set; }

    /// <summary>建议操作。</summary>
    public ServiceAction RecommendedAction { get; set; }

    /// <summary>服务是否正在运行。</summary>
    public bool IsRunning { get; set; }

    /// <summary>启动类型：2=自动，3=手动，4=已禁用。</summary>
    public int StartType { get; set; } = 3;
}

/// <summary>
/// 已知遥测/冗余服务的静态定义库。
/// </summary>
public static class TelemetryServiceDefinitions
{
    /// <summary>
    /// 已知的遥测/冗余 Windows 服务列表。
    /// </summary>
    public static readonly TelemetryServiceInfo[] KnownServices =
    [
        new TelemetryServiceInfo("DiagTrack",
            "Connected User Experiences and Telemetry",
            "微软遥测数据收集服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("dmwappushservice",
            "WAP Push Message Routing",
            "与 DiagTrack 配套的消息推送服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("SysMain",
            "SysMain (原 SuperFetch)",
            "内存预读缓存服务，SSD 上可安全关闭",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("WSearch",
            "Windows Search",
            "搜索索引服务，关闭后开始菜单搜索不可用",
            RiskLevel.Medium, ServiceAction.CanDisable),

        new TelemetryServiceInfo("WaasMedicSvc",
            "Windows Update Medic Service",
            "Windows 更新修复服务",
            RiskLevel.Medium, ServiceAction.CanDisable),

        new TelemetryServiceInfo("wisvc",
            "Windows Insider Service",
            "Windows 预览体验计划服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("MessagingService",
            "Windows Messaging Service",
            "短信/消息服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("PimIndexMaintenance",
            "PimIndexMaintenance",
            "联系人索引维护",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("OneSyncSvc",
            "OneSync Service",
            "邮件/日历同步服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("UnistoreSvc",
            "User Data Storage",
            "应用数据存储服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("WMPNetworkSvc",
            "Windows Media Player Network Sharing",
            "媒体共享服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("TabletInputService",
            "Tablet Input Service",
            "平板电脑输入服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("RetailDemo",
            "Retail Demo Service",
            "零售演示服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("WerSvc",
            "Windows Error Reporting",
            "Windows 错误报告服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("MapsBroker",
            "Downloaded Maps Manager",
            "离线地图管理器",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("lfsvc",
            "Geolocation Service",
            "地理位置服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("WdiSystemHost",
            "WDI System Host",
            "诊断系统主机",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("edgeupdate",
            "Microsoft Edge Update",
            "Edge 后台更新服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("MicrosoftEdgeElevationService",
            "Microsoft Edge Elevation Service",
            "Edge 权限提升服务",
            RiskLevel.Low, ServiceAction.CanDisable),

        new TelemetryServiceInfo("GameInputSvc",
            "GameInput Service",
            "游戏输入服务",
            RiskLevel.Low, ServiceAction.CanDisable),
    ];
}

/// <summary>
/// 遥测/AI 服务扫描器，用于检测并管理 Windows 冗余服务。
/// </summary>
public static class TelemetryServiceScanner
{
    /// <summary>
    /// 扫描所有已知遥测服务，返回各服务的当前运行状态和启动类型。
    /// </summary>
    /// <returns>扫描结果列表。</returns>
    public static List<TelemetryServiceItem> Scan()
    {
        var results = new List<TelemetryServiceItem>();
        ServiceController[] runningServices;

        try
        {
            runningServices = ServiceController.GetServices();
        }
        catch
        {
            // 无法枚举服务时返回空列表
            return results;
        }

        var runningDict = runningServices
            .Where(s => s != null)
            .GroupBy(s => s.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var def in TelemetryServiceDefinitions.KnownServices)
        {
            var item = new TelemetryServiceItem
            {
                ServiceName = def.ServiceName,
                DisplayName = def.DisplayName,
                Description = def.Description,
                RiskLevel = def.RiskLevel,
                RecommendedAction = def.RecommendedAction,
            };

            // 检查服务是否正在运行
            if (runningDict.TryGetValue(def.ServiceName, out var controller))
            {
                item.IsRunning = controller.Status is ServiceControllerStatus.Running
                                 or ServiceControllerStatus.StartPending;
            }

            // 从注册表读取启动类型
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    $@"SYSTEM\CurrentControlSet\Services\{def.ServiceName}");
                if (key != null)
                {
                    var value = key.GetValue("Start");
                    item.StartType = value is int i ? i : 3;
                }
            }
            catch
            {
                item.StartType = 3; // 默认手动
            }

            results.Add(item);
        }

        return results;
    }

    /// <summary>
    /// 禁用指定服务（设为禁用并停止运行中的实例）。
    /// 需要管理员权限。
    /// </summary>
    /// <param name="serviceName">服务内部名称。</param>
    /// <returns>操作是否成功。</returns>
    public static bool DisableService(string serviceName)
    {
        try
        {
            // 先配置为禁用
            using var config = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"config \"{serviceName}\" start= disabled",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (config == null)
                return false;

            config.WaitForExit(15000);
            if (config.ExitCode != 0)
                return false;

            // 如果服务正在运行，尝试停止
            try
            {
                using var stop = Process.Start(new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"stop \"{serviceName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                });
                stop?.WaitForExit(30000);
            }
            catch
            {
                // 停止失败不影响禁用结果
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 启用指定服务（设为手动启动）。
    /// 需要管理员权限。
    /// </summary>
    /// <param name="serviceName">服务内部名称。</param>
    /// <returns>操作是否成功。</returns>
    public static bool EnableService(string serviceName)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "sc.exe",
                Arguments = $"config \"{serviceName}\" start= demand",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (process == null)
                return false;

            process.WaitForExit(15000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
