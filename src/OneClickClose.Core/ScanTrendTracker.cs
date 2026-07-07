using System;
using System.IO;
using System.Text.Json;

namespace OneClickClose.Core;

/// <summary>
/// 扫描趋势追踪器——存储上一次扫描的指标，计算百分比变化。
/// 持久化到本地用户数据目录，避免污染 Release 解压目录。
/// </summary>
public static class ScanTrendTracker
{
    private const string AppName = "OneClickClose";

    private static readonly string _dataDirectory = GetDataDirectory();
    private static readonly string _filePath = Path.Combine(_dataDirectory, "trend.json");

    /// <summary>趋势数据键名。</summary>
    public const string KeyCandidates = "candidates";
    public const string KeyMemoryEstimate = "memoryEstimate";
    public const string KeyBackgroundSoftware = "backgroundSoftware";
    public const string KeyStartupItems = "startupItems";

    /// <summary>
    /// 记录当前扫描的指标，同时将上一次的值覆盖保存。
    /// </summary>
    public static void RecordScan(int candidates, long memoryEstimate, int backgroundSoftware, int startupItems)
    {
        var data = new TrendData
        {
            Candidates = candidates,
            MemoryEstimate = memoryEstimate,
            BackgroundSoftware = backgroundSoftware,
            StartupItems = startupItems,
            Timestamp = DateTime.UtcNow
        };
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch { /* 持久化失败不影响功能 */ }
    }

    /// <summary>
    /// 获取指定指标的百分比变化。正数=增加，负数=减少。首次扫描返回 null。
    /// </summary>
    public static double? GetTrend(string key, int currentValue)
    {
        try
        {
            if (!File.Exists(_filePath)) return null;
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<TrendData>(json);
            if (data == null) return null;

            int previous = key switch
            {
                KeyCandidates => data.Candidates,
                KeyMemoryEstimate => (int)data.MemoryEstimate,
                KeyBackgroundSoftware => data.BackgroundSoftware,
                KeyStartupItems => data.StartupItems,
                _ => -1
            };

            if (previous == 0) return null; // 避免除零
            return Math.Round((double)(currentValue - previous) / previous * 100, 1);
        }
        catch
        {
            return null;
        }
    }

    private class TrendData
    {
        public int Candidates { get; set; }
        public long MemoryEstimate { get; set; }
        public int BackgroundSoftware { get; set; }
        public int StartupItems { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private static string GetDataDirectory()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.GetTempPath();
        }

        return Path.Combine(localAppData, AppName);
    }
}
