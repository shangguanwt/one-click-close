using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace OneClickClose.WinUI.Pages;

public sealed partial class LogsPage : Page
{
    private static readonly TimeSpan SessionMergeWindow = TimeSpan.FromSeconds(45);

    private readonly ObservableCollection<LogLineViewModel> _colorLogLines = new();
    private readonly ObservableCollection<CleanupHistorySessionViewModel> _sessions = new();
    private NotifyCollectionChangedEventHandler _logHandler;
    private CleanupHistorySessionViewModel _selectedSession;

    public LogsPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        HistorySessionsList.ItemsSource = _sessions;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _logHandler = OnLogLinesChanged;
        AppState.LogLines.CollectionChanged += _logHandler;

        RebuildColorLog();
        RefreshHistory();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_logHandler != null)
        {
            AppState.LogLines.CollectionChanged -= _logHandler;
            _logHandler = null;
        }
    }

    private void OnLogLinesChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (string line in e.NewItems)
            {
                _colorLogLines.Add(new LogLineViewModel(line));
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (var _ in e.OldItems)
            {
                if (_colorLogLines.Count > 0)
                {
                    _colorLogLines.RemoveAt(0);
                }
            }
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            _colorLogLines.Clear();
        }
    }

    private void RebuildColorLog()
    {
        _colorLogLines.Clear();
        foreach (string line in AppState.LogLines)
        {
            _colorLogLines.Add(new LogLineViewModel(line));
        }
    }

    private void RefreshHistory()
    {
        AppState.LoadPreferences();
        List<CleanupHistoryRecord> records = AppState.Preferences?.History?.records ?? new List<CleanupHistoryRecord>();
        List<CleanupHistorySessionViewModel> sessions = BuildSessions(records);

        _sessions.Clear();
        foreach (CleanupHistorySessionViewModel session in sessions)
        {
            _sessions.Add(session);
        }

        UpdateStats(sessions);
        UpdateHistoryState(sessions);

        if (_sessions.Count > 0)
        {
            HistorySessionsList.SelectedItem = _sessions[0];
            SelectSession(_sessions[0]);
        }
        else
        {
            HistorySessionsList.SelectedItem = null;
            SelectSession(null);
        }
    }

    private static List<CleanupHistorySessionViewModel> BuildSessions(IReadOnlyList<CleanupHistoryRecord> records)
    {
        List<ParsedHistoryRecord> parsed = (records ?? Array.Empty<CleanupHistoryRecord>())
            .Select(ParseRecord)
            .Where(r => r != null)
            .ToList();

        List<ParsedHistoryRecord> executionRecords = parsed
            .Where(r => IsExecutionDecision(r.Record))
            .ToList();

        List<ParsedHistoryRecord> source = executionRecords.Count > 0 ? executionRecords : parsed;
        List<ParsedHistoryRecord> ordered = source
            .OrderByDescending(r => r.Timestamp)
            .ToList();

        List<List<ParsedHistoryRecord>> groups = new();
        foreach (ParsedHistoryRecord item in ordered)
        {
            List<ParsedHistoryRecord> current = groups.LastOrDefault();
            if (current == null || current.Count == 0 ||
                current[0].Timestamp - item.Timestamp > SessionMergeWindow)
            {
                current = new List<ParsedHistoryRecord>();
                groups.Add(current);
            }

            current.Add(item);
        }

        var sessions = groups
            .Select(group => new CleanupHistorySessionViewModel(group.Select(item => item.Record), group[0].Timestamp.LocalDateTime))
            .OrderByDescending(session => session.Timestamp)
            .ToList();

        ApplySectionHeaders(sessions);
        return sessions;
    }

    private static ParsedHistoryRecord ParseRecord(CleanupHistoryRecord record)
    {
        if (record == null)
        {
            return null;
        }

        DateTimeOffset timestamp;
        if (!DateTimeOffset.TryParse(record.timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timestamp) &&
            !DateTimeOffset.TryParse(record.timestamp, out timestamp))
        {
            timestamp = DateTimeOffset.MinValue;
        }

        return new ParsedHistoryRecord(record, timestamp);
    }

    private static bool IsExecutionDecision(CleanupHistoryRecord record)
    {
        string decision = record?.decision ?? string.Empty;
        return decision.Equals("closed", StringComparison.OrdinalIgnoreCase)
            || decision.Equals("forced", StringComparison.OrdinalIgnoreCase)
            || decision.Equals("remaining", StringComparison.OrdinalIgnoreCase)
            || decision.Equals("report", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplySectionHeaders(List<CleanupHistorySessionViewModel> sessions)
    {
        string previous = null;
        foreach (CleanupHistorySessionViewModel session in sessions)
        {
            string section = GetSectionLabel(session.Timestamp);
            if (!string.Equals(previous, section, StringComparison.Ordinal))
            {
                session.SectionHeader = section;
                session.SectionHeaderVisibility = Visibility.Visible;
                previous = section;
            }
            else
            {
                session.SectionHeader = string.Empty;
                session.SectionHeaderVisibility = Visibility.Collapsed;
            }
        }
    }

    private static string GetSectionLabel(DateTime timestamp)
    {
        DateTime today = DateTime.Today;
        DateTime date = timestamp.Date;
        if (date == today)
        {
            return "今天";
        }

        if (date == today.AddDays(-1))
        {
            return "昨天";
        }

        if (date >= today.AddDays(-6))
        {
            return "本周";
        }

        return "更早";
    }

    private void UpdateStats(IReadOnlyList<CleanupHistorySessionViewModel> sessions)
    {
        DateTime weekStart = DateTime.Now.AddDays(-7);
        int weekCount = sessions.Count(s => s.Timestamp >= weekStart);
        int closedCount = sessions.Sum(s => s.ClosedCount);
        int failureCount = sessions.Sum(s => s.FailedCount);
        long releasedMemory = sessions.Sum(s => s.ReleasedMemoryMb);

        WeekCountText.Text = weekCount.ToString(CultureInfo.InvariantCulture);
        WeekHintText.Text = sessions.Count == 0 ? "等待首次执行" : "近 7 天执行";
        ReleasedMemoryText.Text = FormatMemory(releasedMemory);
        ClosedAppsText.Text = closedCount.ToString(CultureInfo.InvariantCulture);
        FailureCountText.Text = failureCount.ToString(CultureInfo.InvariantCulture);
        FailureHintText.Text = failureCount == 0 ? "暂无失败项" : "需要排查";
    }

    private void UpdateHistoryState(IReadOnlyList<CleanupHistorySessionViewModel> sessions)
    {
        bool hasHistory = sessions.Count > 0;
        HistorySessionsList.Visibility = hasHistory ? Visibility.Visible : Visibility.Collapsed;
        HistoryEmptyState.Visibility = hasHistory ? Visibility.Collapsed : Visibility.Visible;
        HistoryCountBadge.Visibility = hasHistory ? Visibility.Visible : Visibility.Collapsed;
        HistoryCountText.Text = sessions.Count + " 条记录";
        HistoryFooterText.Text = hasHistory
            ? "共 " + sessions.Count + " 条清理记录 · 点击左侧记录查看应用、内存和日志详情"
            : "清理记录仅保存在本机设备，用于回顾优化结果与排查问题。";
    }

    private void HistorySessionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectSession(HistorySessionsList.SelectedItem as CleanupHistorySessionViewModel);
    }

    private void SelectSession(CleanupHistorySessionViewModel session)
    {
        _selectedSession = session;
        bool hasSession = session != null;

        DetailScrollViewer.Visibility = hasSession ? Visibility.Visible : Visibility.Collapsed;
        DetailEmptyState.Visibility = hasSession ? Visibility.Collapsed : Visibility.Visible;
        DetailStatusBadge.Visibility = hasSession ? Visibility.Visible : Visibility.Collapsed;

        if (!hasSession)
        {
            DetailTimestampText.Text = "选择一条清理历史查看详情";
            DetailApplicationsList.ItemsSource = null;
            DetailLogList.ItemsSource = null;
            DetailClosedCountText.Text = "0 个";
            DetailMemoryText.Text = "0 MB";
            DetailFailedCountText.Text = "0 个";
            return;
        }

        DetailTimestampText.Text = session.DetailTimestampText;
        DetailStatusText.Text = session.ResultText;
        DetailStatusText.Foreground = session.ResultForeground;
        DetailStatusBadge.Background = session.ResultBackground;
        DetailClosedCountText.Text = session.ClosedCount + " 个";
        DetailMemoryText.Text = session.MemoryText;
        DetailFailedCountText.Text = session.FailedCount + " 个";
        DetailApplicationsList.ItemsSource = session.Items;
        DetailLogList.ItemsSource = session.LogLines;
    }

    private async void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_sessions.Count == 0)
        {
            await ShowMessageAsync("清理记录为空，无需清空。");
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "清空清理记录",
            Content = "清空后无法从应用内恢复。此操作只会删除本机历史记录，不会影响白名单和设置。",
            PrimaryButtonText = "清空",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        AppState.Preferences.History.records.Clear();
        AppState.Preferences.SaveHistory();
        RefreshHistory();
        FooterHintText.Text = "清理记录已清空。";
    }

    private async void ExportHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_sessions.Count == 0)
        {
            await ShowMessageAsync("暂无可导出的清理记录。");
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            IntPtr hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedFileName = "OneClickClose-清理记录";
            picker.FileTypeChoices.Add("Markdown", new List<string> { ".md" });
            picker.FileTypeChoices.Add("文本文件", new List<string> { ".txt" });

            var file = await picker.PickSaveFileAsync();
            if (file == null)
            {
                return;
            }

            File.WriteAllText(file.Path, BuildExportText(_sessions), new UTF8Encoding(true));
            FooterHintText.Text = "清理记录已导出：" + file.Path;
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("导出失败：" + ex.Message);
        }
    }

    private async void ShowFailures_Click(object sender, RoutedEventArgs e)
    {
        CleanupHistorySessionViewModel failedSession = _sessions.FirstOrDefault(s => s.FailedCount > 0);
        if (failedSession == null)
        {
            await ShowMessageAsync("当前历史记录中没有失败项。");
            return;
        }

        HistorySessionsList.SelectedItem = failedSession;
        SelectSession(failedSession);
        FooterHintText.Text = "已定位到最近一次包含失败项的清理记录。";
    }

    private async void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedSession == null)
        {
            await ShowMessageAsync("请先选择一条清理记录。");
            return;
        }

        var package = new DataPackage();
        package.SetText(string.Join(Environment.NewLine, _selectedSession.LogLines.Select(line => line.Text)));
        Clipboard.SetContent(package);
        FooterHintText.Text = "日志已复制到剪贴板。";
    }

    private async System.Threading.Tasks.Task ShowMessageAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Content = message,
            CloseButtonText = "确定",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    private static string BuildExportText(IEnumerable<CleanupHistorySessionViewModel> sessions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# OneClickClose 清理记录");
        builder.AppendLine();
        builder.AppendLine("导出时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture));
        builder.AppendLine();

        foreach (CleanupHistorySessionViewModel session in sessions)
        {
            builder.AppendLine("## " + session.DetailTimestampText + " · " + session.ResultText);
            builder.AppendLine();
            builder.AppendLine("- 关闭应用：" + session.ClosedCount + " 个");
            builder.AppendLine("- 释放内存：" + session.MemoryText);
            builder.AppendLine("- 失败项：" + session.FailedCount + " 个");
            builder.AppendLine();
            builder.AppendLine("| 应用 | PID | 释放内存 | 关闭方式 | 结果 | 路径 |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (CleanupHistoryItemViewModel item in session.Items)
            {
                builder.AppendLine("| " + EscapePipe(item.ProcessName) + " | " +
                    EscapePipe(item.PidText) + " | " +
                    EscapePipe(item.MemoryText) + " | " +
                    EscapePipe(item.ActionText) + " | " +
                    EscapePipe(item.ResultText) + " | " +
                    EscapePipe(item.PathText) + " |");
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapePipe(string value)
    {
        return (value ?? string.Empty).Replace("|", "\\|");
    }

    internal static string FormatMemory(long memoryMb)
    {
        if (memoryMb <= 0)
        {
            return "0 MB";
        }

        if (memoryMb >= 1024)
        {
            return (memoryMb / 1024d).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
        }

        return memoryMb.ToString(CultureInfo.InvariantCulture) + " MB";
    }

    private sealed record ParsedHistoryRecord(CleanupHistoryRecord Record, DateTimeOffset Timestamp);
}
