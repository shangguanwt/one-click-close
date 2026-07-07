using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.Services;

namespace OneClickClose.WinUI.ViewModels;

public class CandidateRowViewModel : INotifyPropertyChanged
{
    private const int HighRiskScoreThreshold = 75;
    private readonly AppConfig _config;
    private bool _iconLoadStarted;
    private ImageSource _iconSource;
    private List<CandidateChildRowViewModel> _children;

    public ProcessGroupRow Raw { get; }
    public string Process { get; }
    public string Note { get; }
    public string Action { get; }
    public string Status { get; }
    public string StatusText { get; }
    public SolidColorBrush ActionColor => ColorHelper.GetActionBackground(Action);
    public string Count { get; }
    public string CountText { get; }
    public string MemoryText { get; }
    public SolidColorBrush MemoryColor => Raw.MemoryMb >= 512 ? ColorHelper.AccentLight : ColorHelper.BodyText;
    public string RiskText { get; }
    public string RiskScoreText { get; }
    public int RiskScore { get; }
    public string RiskLevelText { get; }
    public SolidColorBrush RiskBadgeBackground => BuildRiskBadgeBackground(Raw);
    public SolidColorBrush RiskDotColor => RiskColor;
    public SolidColorBrush RiskColor => GetRiskColor(Raw.IsHighRisk, Raw.RiskScore);
    public string RecommendationText { get; }
    public bool CanClose { get; }
    public string PrimaryActionText { get; }
    public bool IsHighRisk { get; }
    public double Opacity { get; }
    public string ProcessNameRaw { get; }
    public string Path { get; }
    public string UsageHint { get; }
    public string HabitHint { get; }
    public string DetailLine { get; }
    public ImageSource IconSource
    {
        get => _iconSource;
        private set
        {
            if (ReferenceEquals(_iconSource, value))
            {
                return;
            }

            _iconSource = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IconImageVisibility));
            OnPropertyChanged(nameof(FallbackIconVisibility));
        }
    }
    public Visibility IconImageVisibility => IconSource is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility FallbackIconVisibility => IconSource is null ? Visibility.Visible : Visibility.Collapsed;
    public string IconGlyph { get; }
    public SolidColorBrush IconBackground => ColorHelper.GetActionBackground(Action);
    public SolidColorBrush IconForeground => RiskColor;
    public bool HasChildren { get; }
    public int ChildCount { get; }
    public string SearchText { get; }
    public List<CandidateChildRowViewModel> Children => _children ??= BuildChildren();
    public event PropertyChangedEventHandler PropertyChanged;

    public CandidateRowViewModel(ProcessGroupRow row, AppConfig config = null)
    {
        Raw = row;
        _config = config;
        Process = row.Process ?? "";
        ProcessNameRaw = row.Process ?? "";
        Note = row.Note ?? "";
        Action = row.Action ?? "";
        Status = row.Status ?? "";
        StatusText = BuildStatusText(row, config);
        Count = row.Count.ToString();
        CountText = row.Count + " 个进程";
        MemoryText = row.MemoryMb > 0 ? row.MemoryMb + " MB" : "-";
        RiskText = row.RiskScore + "";
        RiskScoreText = "风险分 " + row.RiskScore;
        RiskScore = row.RiskScore;
        IsHighRisk = row.IsHighRisk;

        RiskLevelText = BuildRiskLevelText(row, config);
        RecommendationText = BuildRecommendationText(row);
        CanClose = (row.Children ?? new List<ProcessRecord>()).Any(r => ProcessPlanner.IsExecutableTarget(r, config));
        PrimaryActionText = CanClose ? "关闭" : "仅提示";

        Opacity = row.IsHighRisk ? 0.7 : 1.0;
        Path = row.Path ?? "";
        UsageHint = row.UsageHint ?? "";
        HabitHint = row.HabitHint ?? "";
        DetailLine = BuildDetailLine(row);
        IconGlyph = GetGroupGlyph(row);
        ChildCount = row.Children?.Count ?? 0;
        HasChildren = ChildCount > 0;
        SearchText = BuildSearchText(row);
    }

    public async Task EnsureIconLoadedAsync()
    {
        if (_iconLoadStarted || IconSource != null)
        {
            return;
        }

        _iconLoadStarted = true;
        try
        {
            IconSource = await ProcessIconProvider.GetIconSourceAsync(Raw);
        }
        catch
        {
            IconSource = null;
        }
    }

    private List<CandidateChildRowViewModel> BuildChildren()
    {
        return (Raw.Children ?? new List<ProcessRecord>())
            .Select(r => new CandidateChildRowViewModel(r, _config))
            .ToList();
    }

    private static string BuildSearchText(ProcessGroupRow row)
    {
        var parts = new List<string>
        {
            row.Process,
            row.Note,
            row.Action,
            row.Status,
            row.Path,
            row.UsageHint,
            row.HabitHint
        };

        foreach (ProcessRecord child in row.Children ?? new List<ProcessRecord>())
        {
            parts.Add(child.Id.ToString());
            parts.Add(child.ProcessName);
            parts.Add(child.Action);
            parts.Add(child.Status);
            parts.Add(child.MainWindowTitle);
            parts.Add(child.Path);
            parts.Add(child.Reason);
            parts.Add(child.UsageHint);
            parts.Add(child.HabitHint);
        }

        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant();
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string BuildDetailLine(ProcessGroupRow row)
    {
        var pieces = new List<string>();
        if (!string.IsNullOrWhiteSpace(row.UsageHint)) pieces.Add(row.UsageHint);
        if (!string.IsNullOrWhiteSpace(row.HabitHint)) pieces.Add(row.HabitHint);
        if (!string.IsNullOrWhiteSpace(row.Note)) pieces.Add(row.Note);
        return string.Join(" · ", pieces);
    }

    private static string GetGroupGlyph(ProcessGroupRow row)
    {
        if (row.Action == ProcessPlanner.ActionProtect || row.Status == "protected") return "\uE72E";
        if (row.Action == ProcessPlanner.ActionSkip || row.Status == "skipped") return "\uE711";
        if (row.Action == ProcessPlanner.ActionForce) return "\uE7EF";
        if (row.Action == ProcessPlanner.ActionReport) return "\uE7BA";
        if (row.HasWindow) return "\uE7F4";
        return "\uE9D5";
    }

    private static string BuildStatusText(ProcessGroupRow row, AppConfig config)
    {
        if ((row.Children ?? new List<ProcessRecord>()).Any(r => ProcessPlanner.IsExecutableTarget(r, config)))
            return "可关闭";
        if (IsProtected(row))
            return "已保护";
        if (IsSkipped(row))
            return "已跳过";
        if (row.Action == ProcessPlanner.ActionReport || row.IsHighRisk)
            return "仅提示";
        if (row.Status == "mixed")
            return "混合状态";
        return "仅提示";
    }

    private static string BuildRiskLevelText(ProcessGroupRow row, AppConfig config)
    {
        if ((row.Children ?? new List<ProcessRecord>()).Any(r => ProcessPlanner.IsExecutableTarget(r, config)))
            return "可关闭";
        if (IsProtected(row))
            return "已保护";
        if (IsSkipped(row))
            return "已跳过";
        if (row.Action == ProcessPlanner.ActionReport || row.IsHighRisk)
            return "仅提示";
        if (row.Status == "mixed")
            return "混合状态";
        if (row.Action == ProcessPlanner.ActionReport || row.RiskScore >= HighRiskScoreThreshold)
            return "建议保留";
        if (row.Action == ProcessPlanner.ActionForce || row.RiskScore >= 45)
            return "正在活动";
        return "低风险";
    }

    private static SolidColorBrush BuildRiskBadgeBackground(ProcessGroupRow row)
    {
        if (IsProtected(row))
            return ColorHelper.SafeGreenSoft;
        if (IsSkipped(row))
            return ColorHelper.CyanSoft;
        if (row.Action == ProcessPlanner.ActionReport || row.RiskScore >= HighRiskScoreThreshold)
            return ColorHelper.CopperSoft;
        if (row.Action == ProcessPlanner.ActionForce || row.RiskScore >= 45)
            return ColorHelper.CopperSoft;
        return ColorHelper.SafeGreenSoft;
    }

    internal static SolidColorBrush GetRiskColor(bool isHighRisk, int riskScore)
    {
        if (isHighRisk)
            return ColorHelper.Danger;
        if (riskScore >= 40)
            return ColorHelper.Copper;
        return ColorHelper.Safe;
    }

    private static bool IsProtected(ProcessGroupRow row)
    {
        return row.Action == ProcessPlanner.ActionProtect
            || row.Status == "protected"
            || (row.Children ?? new List<ProcessRecord>()).Any(r =>
                string.Equals(r.Status, "protected", System.StringComparison.OrdinalIgnoreCase)
                || r.Action == ProcessPlanner.ActionProtect);
    }

    private static bool IsSkipped(ProcessGroupRow row)
    {
        return row.Action == ProcessPlanner.ActionSkip
            || row.Status == "skipped"
            || (row.Children ?? new List<ProcessRecord>()).Any(r =>
                string.Equals(r.Status, "skipped", System.StringComparison.OrdinalIgnoreCase)
                || r.Action == ProcessPlanner.ActionSkip);
    }

    private static string BuildRecommendationText(ProcessGroupRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.HabitHint))
            return row.HabitHint;
        if (IsProtected(row))
            return string.IsNullOrWhiteSpace(row.Note) ? "受保护，不会直接关闭" : row.Note;
        if (IsSkipped(row))
            return string.IsNullOrWhiteSpace(row.Note) ? "未满足关闭条件，当前仅展示" : row.Note;
        if (row.Action == ProcessPlanner.ActionReport || row.IsHighRisk || row.RiskScore >= HighRiskScoreThreshold)
            return "仅提示，需要加入目标名单或强制清理后再处理";
        if (row.Action == ProcessPlanner.ActionReport || row.RiskScore >= HighRiskScoreThreshold)
            return "建议保留，避免影响当前功能";
        if (row.Action == ProcessPlanner.ActionForce)
            return "可清理，但建议先查看详情";
        if (row.HasWindow)
            return "可安全关闭";
        return "低风险优化候选";
    }
}

public class CandidateChildRowViewModel
{
    private readonly bool _isHighRisk;
    private readonly int _riskScore;

    public int Id { get; }
    public string Process { get; }
    public string WindowTitle { get; }
    public string Path { get; }
    public string MemoryText { get; }
    public string RiskText { get; }
    public string Action { get; }
    public bool CanClose { get; }
    public string PrimaryActionText { get; }
    public string StatusText { get; }
    public SolidColorBrush RiskColor => CandidateRowViewModel.GetRiskColor(_isHighRisk, _riskScore);
    public ImageSource IconSource { get; }
    public Visibility IconImageVisibility => IconSource is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility FallbackIconVisibility => IconSource is null ? Visibility.Visible : Visibility.Collapsed;
    public string IconGlyph { get; }
    public SolidColorBrush IconBackground => ColorHelper.GetActionBackground(Action);
    public SolidColorBrush IconForeground => RiskColor;
    public string Detail { get; }

    public CandidateChildRowViewModel(ProcessRecord record, AppConfig config = null)
    {
        Id = record.Id;
        Process = record.ProcessName ?? "";
        WindowTitle = string.IsNullOrWhiteSpace(record.MainWindowTitle) ? "-" : record.MainWindowTitle;
        Path = string.IsNullOrWhiteSpace(record.Path) ? "-" : record.Path;
        MemoryText = record.MemoryMb > 0 ? record.MemoryMb + " MB" : "-";
        RiskText = record.RiskScore.ToString();
        _isHighRisk = record.IsHighRisk;
        _riskScore = record.RiskScore;
        Action = record.Action ?? "";
        CanClose = ProcessPlanner.IsExecutableTarget(record, config);
        PrimaryActionText = CanClose ? "关闭" : "仅提示";
        StatusText = CanClose
            ? "可关闭"
            : string.Equals(record.Status, "protected", System.StringComparison.OrdinalIgnoreCase) || record.Action == ProcessPlanner.ActionProtect
                ? "已保护"
                : string.Equals(record.Status, "skipped", System.StringComparison.OrdinalIgnoreCase) || record.Action == ProcessPlanner.ActionSkip
                    ? "已跳过"
                    : "仅提示";
        IconSource = null;
        IconGlyph = GetProcessGlyph(record);
        Detail = "PID " + record.Id + " · " + WindowTitle;
    }

    private static string GetProcessGlyph(ProcessRecord record)
    {
        if (record.Action == ProcessPlanner.ActionProtect || string.Equals(record.Status, "protected", System.StringComparison.OrdinalIgnoreCase)) return "\uE72E";
        if (record.Action == ProcessPlanner.ActionSkip || string.Equals(record.Status, "skipped", System.StringComparison.OrdinalIgnoreCase)) return "\uE711";
        if (record.Action == ProcessPlanner.ActionForce) return "\uE7EF";
        if (record.Action == ProcessPlanner.ActionReport) return "\uE7BA";
        if (record.HasWindow) return "\uE7F4";
        return "\uE9D5";
    }
}
