using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.Pages;
using OneClickClose.WinUI.Services;

namespace OneClickClose.WinUI.ViewModels;

public sealed class CleanupHistorySessionViewModel
{
    public DateTime Timestamp { get; }
    public string TimestampText { get; }
    public string DetailTimestampText { get; }
    public string RelativeText { get; }
    public int ClosedCount { get; }
    public int FailedCount { get; }
    public int SkippedCount { get; }
    public long ReleasedMemoryMb { get; }
    public string ClosedCountText { get; }
    public string FailedCountText { get; }
    public string MemoryText { get; }
    public string ResultText { get; }
    public SolidColorBrush ResultForeground { get; }
    public SolidColorBrush ResultBackground { get; }
    public SolidColorBrush FailedForeground { get; }
    public List<CleanupHistoryItemViewModel> Items { get; }
    public List<LogLineViewModel> LogLines { get; }
    public string SectionHeader { get; set; }
    public Visibility SectionHeaderVisibility { get; set; } = Visibility.Collapsed;

    public CleanupHistorySessionViewModel(IEnumerable<CleanupHistoryRecord> records, DateTime timestamp)
    {
        Timestamp = timestamp == DateTime.MinValue ? DateTime.Now : timestamp;
        TimestampText = Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
        DetailTimestampText = TimestampText;
        RelativeText = BuildRelativeText(Timestamp);

        Items = records
            .Where(record => record != null)
            .Select(record => new CleanupHistoryItemViewModel(record))
            .OrderByDescending(item => item.SortPriority)
            .ThenByDescending(item => item.MemoryMb)
            .ThenBy(item => item.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ClosedCount = Items.Count(item => item.IsSuccessfulClose);
        FailedCount = Items.Count(item => item.IsFailure);
        SkippedCount = Items.Count(item => item.IsSkipped);
        ReleasedMemoryMb = Items
            .Where(item => item.IsSuccessfulClose)
            .Sum(item => item.MemoryMb);

        ClosedCountText = ClosedCount.ToString(CultureInfo.InvariantCulture);
        FailedCountText = FailedCount.ToString(CultureInfo.InvariantCulture);
        MemoryText = LogsPage.FormatMemory(ReleasedMemoryMb);

        ResultText = BuildResultText();
        ResultForeground = BuildResultForeground();
        ResultBackground = BuildResultBackground();
        FailedForeground = FailedCount > 0 ? ColorHelper.Danger : ColorHelper.BodyText;
        LogLines = BuildLogLines();
    }

    private string BuildResultText()
    {
        if (FailedCount > 0 && ClosedCount > 0)
        {
            return "部分失败";
        }

        if (FailedCount > 0)
        {
            return "失败";
        }

        if (SkippedCount > 0 && ClosedCount == 0)
        {
            return "已跳过";
        }

        return "成功";
    }

    private SolidColorBrush BuildResultForeground()
    {
        if (FailedCount > 0)
        {
            return ColorHelper.Danger;
        }

        if (SkippedCount > 0 && ClosedCount == 0)
        {
            return ColorHelper.Copper;
        }

        return ColorHelper.Safe;
    }

    private SolidColorBrush BuildResultBackground()
    {
        if (FailedCount > 0)
        {
            return ColorHelper.DangerSoft;
        }

        if (SkippedCount > 0 && ClosedCount == 0)
        {
            return ColorHelper.CopperSoft;
        }

        return ColorHelper.SafeGreenSoft;
    }

    private List<LogLineViewModel> BuildLogLines()
    {
        var lines = new List<string>
        {
            "[" + Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "] 开始执行一键关闭",
            "[" + Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "] 找到 " + Items.Count + " 个可处理项目，预计释放 " + MemoryText
        };

        foreach (CleanupHistoryItemViewModel item in Items)
        {
            string prefix = item.IsFailure
                ? "[失败]"
                : item.IsSkipped ? "[跳过]" : "[成功]";
            lines.Add(prefix + " " + item.ProcessName + " (" + item.ProcessId + ") ... " + item.ResultText);
        }

        lines.Add("[" + Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture) + "] 执行完成，释放内存 " + MemoryText);
        return lines.Select(line => new LogLineViewModel(line)).ToList();
    }

    private static string BuildRelativeText(DateTime timestamp)
    {
        DateTime today = DateTime.Today;
        if (timestamp.Date == today)
        {
            return "今天 " + timestamp.ToString("HH:mm", CultureInfo.CurrentCulture);
        }

        if (timestamp.Date == today.AddDays(-1))
        {
            return "昨天 " + timestamp.ToString("HH:mm", CultureInfo.CurrentCulture);
        }

        int days = (today - timestamp.Date).Days;
        return days > 0 && days < 7 ? days + " 天前" : timestamp.ToString("MM-dd", CultureInfo.CurrentCulture);
    }
}

public sealed class CleanupHistoryItemViewModel
{
    public string ProcessName { get; }
    public int ProcessId { get; }
    public string PidText { get; }
    public string PathText { get; }
    public long MemoryMb { get; }
    public string MemoryText { get; }
    public string ActionText { get; }
    public string ProtectionText { get; }
    public string ResultText { get; }
    public SolidColorBrush ResultForeground { get; }
    public SolidColorBrush IconBackground { get; }
    public SolidColorBrush IconForeground { get; }
    public string IconGlyph { get; }
    public ImageSource IconSource { get; }
    public Visibility IconImageVisibility => IconSource is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility FallbackIconVisibility => IconSource is null ? Visibility.Visible : Visibility.Collapsed;
    public bool IsSuccessfulClose { get; }
    public bool IsFailure { get; }
    public bool IsSkipped { get; }
    public int SortPriority { get; }

    public CleanupHistoryItemViewModel(CleanupHistoryRecord record)
    {
        ProcessName = string.IsNullOrWhiteSpace(record.processName) ? "未知应用" : record.processName;
        ProcessId = record.processId;
        PidText = ProcessId > 0 ? "PID " + ProcessId : "PID -";
        PathText = string.IsNullOrWhiteSpace(record.path) ? "-" : record.path;
        MemoryMb = Math.Max(0, record.memoryMb);
        MemoryText = LogsPage.FormatMemory(MemoryMb);

        string decision = record.decision ?? string.Empty;
        IsSuccessfulClose = decision.Equals("closed", StringComparison.OrdinalIgnoreCase)
            || decision.Equals("forced", StringComparison.OrdinalIgnoreCase);
        IsFailure = decision.Equals("remaining", StringComparison.OrdinalIgnoreCase);
        IsSkipped = decision.Equals("report", StringComparison.OrdinalIgnoreCase)
            || decision.Equals("cancel", StringComparison.OrdinalIgnoreCase);

        ActionText = BuildActionText(record);
        ProtectionText = "否";
        ResultText = BuildResultText(decision);
        ResultForeground = BuildResultForeground();
        SortPriority = IsFailure ? 3 : IsSuccessfulClose ? 2 : 1;

        var processRecord = new ProcessRecord
        {
            ProcessName = ProcessName,
            Id = ProcessId,
            Path = record.path,
            Action = record.action
        };

        IconSource = ProcessIconProvider.GetIconSource(processRecord);
        IconGlyph = GetFallbackGlyph(record);
        IconForeground = ResultForeground;
        IconBackground = BuildIconBackground();
    }

    private static string BuildActionText(CleanupHistoryRecord record)
    {
        string decision = record.decision ?? string.Empty;
        if (decision.Equals("forced", StringComparison.OrdinalIgnoreCase))
        {
            return "强制关闭";
        }

        if (decision.Equals("closed", StringComparison.OrdinalIgnoreCase))
        {
            return "正常关闭";
        }

        if (decision.Equals("remaining", StringComparison.OrdinalIgnoreCase))
        {
            return "仍在运行";
        }

        if (decision.Equals("report", StringComparison.OrdinalIgnoreCase))
        {
            return "仅报告";
        }

        if (decision.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            return "用户取消";
        }

        return string.IsNullOrWhiteSpace(record.action) ? "-" : record.action;
    }

    private static string BuildResultText(string decision)
    {
        if (decision.Equals("closed", StringComparison.OrdinalIgnoreCase)
            || decision.Equals("forced", StringComparison.OrdinalIgnoreCase))
        {
            return "成功";
        }

        if (decision.Equals("remaining", StringComparison.OrdinalIgnoreCase))
        {
            return "失败";
        }

        if (decision.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            return "取消";
        }

        if (decision.Equals("report", StringComparison.OrdinalIgnoreCase))
        {
            return "跳过";
        }

        return "-";
    }

    private SolidColorBrush BuildResultForeground()
    {
        if (IsFailure)
        {
            return ColorHelper.Danger;
        }

        if (IsSkipped)
        {
            return ColorHelper.Copper;
        }

        return ColorHelper.Safe;
    }

    private SolidColorBrush BuildIconBackground()
    {
        if (IsFailure)
        {
            return ColorHelper.DangerSoft;
        }

        if (IsSkipped)
        {
            return ColorHelper.CopperSoft;
        }

        return ColorHelper.CyanSoft;
    }

    private static string GetFallbackGlyph(CleanupHistoryRecord record)
    {
        string decision = record.decision ?? string.Empty;
        if (decision.Equals("forced", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE7EF";
        }

        if (decision.Equals("remaining", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE783";
        }

        if (decision.Equals("report", StringComparison.OrdinalIgnoreCase))
        {
            return "\uE7BA";
        }

        return "\uE8D4";
    }
}
