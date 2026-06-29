using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.ViewModels;

namespace OneClickClose.WinUI.Pages;

public sealed partial class OverviewPage : Page
{
    private CancellationTokenSource _closeCts;
    private readonly DispatcherQueue _dispatcher;
    private bool _initialScanDone;
    private NotifyCollectionChangedEventHandler _logHandler;
    private readonly ObservableCollection<LogLineViewModel> _logViewModels = new();
    private ViewModels.SystemMonitorViewModel _monitorVm;
    private List<TopMemoryItem> _allTableItems = new();
    private int _lastStartupItems = -1;
    private int _scanSessionId;

    public OverviewPage()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        LogList.ItemsSource = _logViewModels;
        FilterCombo.SelectedIndex = 0;
    }

    private void OverviewPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        if (width <= 0) return;

        bool narrow = width < 760;
        bool medium = width >= 760 && width < 1120;

        PageStack.Padding = narrow ? new Thickness(18, 0, 18, 24) : new Thickness(30, 0, 34, 32);

        SetGridColumns(HardwareGrid, narrow ? 1 : medium ? 2 : 4);
        Place(HardwareGrid, CpuChipCard, 0, 0);
        Place(HardwareGrid, MemoryChipCard, narrow ? 1 : medium ? 0 : 0, narrow ? 0 : medium ? 1 : 1);
        Place(HardwareGrid, TempChipCard, narrow ? 2 : medium ? 1 : 0, narrow ? 0 : medium ? 0 : 2);
        Place(HardwareGrid, BatteryChipCard, narrow ? 3 : medium ? 1 : 0, narrow ? 0 : medium ? 1 : 3);

        if (narrow)
        {
            SetHeroRowsAndColumns(1);
            Place(HeroGrid, HeroCopyPanel, 0, 0);
            Place(HeroGrid, MemoryHalo, 1, 0);
            Place(HeroGrid, PerformancePanel, 2, 0);
            PrimaryActionsPanel.Orientation = Orientation.Vertical;
            PrimaryActionsPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            OptimizeButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            ScanButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            CancelCloseButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            MemoryHalo.Width = 218;
            MemoryHalo.Height = 218;
        }
        else if (medium)
        {
            SetHeroRowsAndColumns(2);
            Place(HeroGrid, HeroCopyPanel, 0, 0);
            Place(HeroGrid, MemoryHalo, 0, 1);
            Place(HeroGrid, PerformancePanel, 1, 0, 2);
            PrimaryActionsPanel.Orientation = Orientation.Horizontal;
            PrimaryActionsPanel.HorizontalAlignment = HorizontalAlignment.Left;
            OptimizeButton.HorizontalAlignment = HorizontalAlignment.Left;
            ScanButton.HorizontalAlignment = HorizontalAlignment.Left;
            CancelCloseButton.HorizontalAlignment = HorizontalAlignment.Left;
            MemoryHalo.Width = 232;
            MemoryHalo.Height = 232;
        }
        else
        {
            SetHeroRowsAndColumns(3);
            Place(HeroGrid, HeroCopyPanel, 0, 0);
            Place(HeroGrid, MemoryHalo, 0, 1);
            Place(HeroGrid, PerformancePanel, 0, 2);
            PrimaryActionsPanel.Orientation = Orientation.Horizontal;
            PrimaryActionsPanel.HorizontalAlignment = HorizontalAlignment.Left;
            OptimizeButton.HorizontalAlignment = HorizontalAlignment.Left;
            ScanButton.HorizontalAlignment = HorizontalAlignment.Left;
            CancelCloseButton.HorizontalAlignment = HorizontalAlignment.Left;
            MemoryHalo.Width = 246;
            MemoryHalo.Height = 246;
        }

        SetGridColumns(MetricsInner, narrow ? 1 : medium ? 2 : 4);
        Place(MetricsInner, MetricCandidatesCard, 0, 0);
        Place(MetricsInner, MetricMemoryCard, narrow ? 1 : medium ? 0 : 0, narrow ? 0 : medium ? 1 : 1);
        Place(MetricsInner, MetricBackgroundCard, narrow ? 2 : medium ? 1 : 0, narrow ? 0 : medium ? 0 : 2);
        Place(MetricsInner, MetricStartupCard, narrow ? 3 : medium ? 1 : 0, narrow ? 0 : medium ? 1 : 3);

        if (narrow)
        {
            ProcessToolbar.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            ProcessToolbar.ColumnDefinitions[2].Width = new GridLength(132);
            Grid.SetRow(SearchBox, 1);
            Grid.SetColumn(SearchBox, 0);
            Grid.SetColumnSpan(SearchBox, 2);
            Grid.SetRow(FilterCombo, 1);
            Grid.SetColumn(FilterCombo, 2);
        }
        else
        {
            ProcessToolbar.ColumnDefinitions[1].Width = new GridLength(260);
            ProcessToolbar.ColumnDefinitions[2].Width = new GridLength(132);
            Grid.SetRow(SearchBox, 0);
            Grid.SetColumn(SearchBox, 1);
            Grid.SetColumnSpan(SearchBox, 1);
            Grid.SetRow(FilterCombo, 0);
            Grid.SetColumn(FilterCombo, 2);
        }
    }

    private static void SetGridColumns(Grid grid, int count)
    {
        if (grid.ColumnDefinitions.Count == count) return;
        grid.ColumnDefinitions.Clear();
        for (int i = 0; i < count; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    }

    private void SetHeroRowsAndColumns(int columns)
    {
        HeroGrid.ColumnDefinitions.Clear();
        if (columns == 1)
        {
            HeroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
        else if (columns == 2)
        {
            HeroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            HeroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        }
        else
        {
            HeroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.22, GridUnitType.Star) });
            HeroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            HeroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }
    }

    private static void Place(Grid owner, FrameworkElement element, int row, int column, int columnSpan = 1)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, columnSpan);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _logHandler = OnLogLinesChanged;
        AppState.LogLines.CollectionChanged += _logHandler;

        _monitorVm = new ViewModels.SystemMonitorViewModel();
        _monitorVm.PropertyChanged += Monitor_PropertyChanged;
        _monitorVm.StartMonitoring();

        if (!_initialScanDone && !AppState.HasPlan && !AppState.IsExecuting)
        {
            _initialScanDone = true;
            _ = Task.Run(async () =>
            {
                await Task.Delay(200);
                _dispatcher.TryEnqueue(() => ScanButton_Click(this, new RoutedEventArgs()));
            });
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_logHandler != null)
        {
            AppState.LogLines.CollectionChanged -= _logHandler;
            _logHandler = null;
        }
        if (_monitorVm != null)
        {
            _monitorVm.PropertyChanged -= Monitor_PropertyChanged;
            _monitorVm.StopMonitoring();
        }
    }

    private void OnLogLinesChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (e.NewItems != null)
                foreach (string line in e.NewItems)
                    _logViewModels.Add(new LogLineViewModel(line));
            // 源集合裁剪最旧行时同步镜像，防止页面集合无界增长
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
                foreach (var _ in e.OldItems)
                    if (_logViewModels.Count > 0) _logViewModels.RemoveAt(0);
            if (e.Action == NotifyCollectionChangedAction.Reset)
                _logViewModels.Clear();
            LogCountText.Text = _logViewModels.Count > 0 ? _logViewModels.Count + " 行" : "";
        });
    }

    private void Monitor_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_monitorVm == null) return;
        _dispatcher.TryEnqueue(() =>
        {
            try
            {
                CpuPercentText.Text = $"{_monitorVm.CpuUsagePercent:F1}%";
                CpuChipText.Text = $"{_monitorVm.CpuUsagePercent:F0}%";
                CpuSparkline.Values = _monitorVm.CpuSparklineData;
                CpuChipSparkline.Values = _monitorVm.CpuSparklineData;

                GpuValueText.Text = _monitorVm.GpuUsagePercent > 0
                    ? $"{_monitorVm.GpuUsagePercent:F1}%" : "--";
                GpuSparkline.Values = _monitorVm.GpuSparklineData;

                DiskValueText.Text = $"{_monitorVm.DiskUsagePercent:F1}%";
                DiskSparkline.Values = _monitorVm.DiskSparklineData;

                NetworkValueText.Text = _monitorVm.NetworkMbps >= 1
                    ? $"{_monitorVm.NetworkMbps:F1} Mbps"
                    : $"{_monitorVm.NetworkMbps * 1024:F0} KB/s";
                NetworkSparkline.Values = _monitorVm.NetworkSparklineData;

                double memoryPercent = _monitorVm.TotalMemoryMb > 0
                    ? (double)_monitorVm.UsedMemoryMb / _monitorVm.TotalMemoryMb * 100 : 0;
                MemoryRing.Value = memoryPercent;
                MemoryRing.UsedText = $"{FormatMemoryGb(_monitorVm.UsedMemoryMb)} / {FormatMemoryGb(_monitorVm.TotalMemoryMb)}";
                MemoryChipText.Text = $"{FormatMemoryGb(_monitorVm.UsedMemoryMb)} / {FormatMemoryGb(_monitorVm.TotalMemoryMb)}";
                MemoryChipSparkline.Values = _monitorVm.MemorySparklineData;

                if (_monitorVm.TemperatureC.HasValue)
                    TempChipText.Text = $"{_monitorVm.TemperatureC.Value:F0}°C";
                else
                    TempChipText.Text = "--°C";
                TempChipSparkline.Values = _monitorVm.TemperatureSparklineData;

                if (_monitorVm.BatteryPresent && _monitorVm.BatteryPercent.HasValue)
                    BatteryChipText.Text = $"{_monitorVm.BatteryPercent.Value:F0}%";
                else
                    BatteryChipText.Text = "--%";
                BatteryChipSparkline.Values = _monitorVm.BatterySparklineData;
            }
            catch { }
        });
    }

    public void TriggerScan() { if (ScanButton.IsEnabled) ScanButton_Click(this, new RoutedEventArgs()); }
    public void TriggerCloseAll() { if (OptimizeButton.IsEnabled) CloseAllButton_Click(this, new RoutedEventArgs()); }
    public void TryCancel() { if (CancelCloseButton.Visibility == Visibility.Visible) CancelCloseButton_Click(this, new RoutedEventArgs()); }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ScanButton.IsEnabled = false;
            OptimizeButton.IsEnabled = false;
            LoadingPanel.Visibility = Visibility.Visible;
            ErrorBar.Visibility = Visibility.Collapsed;
            ErrorBar.IsOpen = false;
            ResultBar.Visibility = Visibility.Collapsed;
            ResultBar.IsOpen = false;
            SetStatus("scanning", "正在扫描后台进程...");

            await ScanWithTimeoutAsync();

            LoadingPanel.Visibility = Visibility.Collapsed;
            UpdateStatsFromPlan();
        }
        catch (Exception ex)
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ErrorBar.Message = ex.Message;
            ErrorBar.Visibility = Visibility.Visible;
            ErrorBar.IsOpen = true;
            SetStatus("error", "扫描失败");
        }
        finally { ScanButton.IsEnabled = true; }
    }

    private async Task ScanWithTimeoutAsync()
    {
        Task scanTask = AppState.ScanAsync(AppState.ConfigPath);
        Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(20));
        if (await Task.WhenAny(scanTask, timeoutTask) != scanTask)
        {
            throw new TimeoutException("扫描超时，请稍后重试。");
        }

        await scanTask;
    }
    private void UpdateStatsFromPlan()
    {
        var plan = AppState.CurrentPlan;
        int candidateCount = plan.Candidates?.Count ?? 0;
        int gracefulCount = 0;
        long memoryEstimate = 0;
        if (plan.Candidates != null)
        {
            gracefulCount = plan.Candidates.Count(p => p.Action == ProcessPlanner.ActionGraceful);
            memoryEstimate = plan.Candidates.Sum(p => p.MemoryMb);
        }

        int scanSessionId = Interlocked.Increment(ref _scanSessionId);
        _lastStartupItems = -1;

        ScanTrendTracker.RecordScan(candidateCount, memoryEstimate, gracefulCount, 0);
        UpdateTrendText(TrendCandidates, ScanTrendTracker.KeyCandidates, candidateCount);
        UpdateTrendText(TrendMemory, ScanTrendTracker.KeyMemoryEstimate, (int)Math.Min(memoryEstimate, int.MaxValue));
        UpdateTrendText(TrendBackground, ScanTrendTracker.KeyBackgroundSoftware, gracefulCount);
        TrendStartup.Text = "--";
        TrendStartup.Foreground = (SolidColorBrush)Application.Current.Resources["SubtleTextBrush"];

        MetricsInner.Visibility = Visibility.Visible;
        TotalCountText.Text = candidateCount.ToString();
        string memoryText = FormatMemoryEstimate(memoryEstimate);
        MemoryEstimateText.Text = memoryText;
        MemoryEstimateHint.Text = "可释放内存";
        GracefulCountText.Text = gracefulCount.ToString();
        ProtectedCountText.Text = "--";
        HeroInsightText.Text = $"已检测到 {candidateCount} 个可优化项目 / 预计释放 {memoryText} 内存";

        if (candidateCount > 0)
            SetStatus("ready", string.Format("发现 {0} 个可安全优化项目", candidateCount));
        else
            SetStatus("clean", "当前状态良好，无需清理");

        ScanTimeText.Text = "扫描于 " + AppState.LastScanTime.ToString("HH:mm:ss");
        OptimizeButton.IsEnabled = candidateCount > 0;
        ShowProcessTable(plan);
        _ = RefreshStartupCountAsync(scanSessionId, candidateCount, memoryEstimate, gracefulCount);
    }

    private async Task RefreshStartupCountAsync(int scanSessionId, int candidateCount, long memoryEstimate, int gracefulCount)
    {
        try
        {
            int startupItems = await Task.Run(() => StartupScanner.ScanAll().Count);
            if (scanSessionId != _scanSessionId)
            {
                return;
            }

            _lastStartupItems = startupItems;
            ProtectedCountText.Text = startupItems.ToString();
            UpdateTrendText(TrendStartup, ScanTrendTracker.KeyStartupItems, startupItems);
            ScanTrendTracker.RecordScan(candidateCount, memoryEstimate, gracefulCount, startupItems);
        }
        catch
        {
            if (scanSessionId != _scanSessionId)
            {
                return;
            }

            _lastStartupItems = -1;
            ProtectedCountText.Text = "--";
            TrendStartup.Text = "--";
            TrendStartup.Foreground = (SolidColorBrush)Application.Current.Resources["SubtleTextBrush"];
        }
    }
    private void UpdateTrendText(TextBlock element, string key, int currentValue)
    {
        var trend = ScanTrendTracker.GetTrend(key, currentValue);
        if (trend.HasValue)
        {
            string arrow = trend.Value >= 0 ? "↑" : "↓";
            element.Text = $"{arrow} {Math.Abs(trend.Value):F1}%";
            element.Foreground = trend.Value >= 0 ? ColorHelper.Copper : ColorHelper.Safe;
        }
        else
        {
            element.Text = "--";
            element.Foreground = (SolidColorBrush)Application.Current.Resources["SubtleTextBrush"];
        }
    }

    private void SetStatus(string state, string headline)
    {
        StatusHeadline.Text = headline;
        StatusDot.Fill = state switch
        {
            "ready" => ColorHelper.Safe,
            "clean" => ColorHelper.Safe,
            "scanning" => ColorHelper.AccentLight,
            "executing" => ColorHelper.Copper,
            "error" => ColorHelper.Danger,
            _ => (SolidColorBrush)Application.Current.Resources["SubtleTextBrush"]
        };
    }

    private void ShowProcessTable(ClosePlan plan)
    {
        if (plan.Candidates == null || plan.Candidates.Count == 0)
        {
            ProcessTableList.Visibility = Visibility.Collapsed;
            ProcessTableBorder.Visibility = Visibility.Collapsed;
            TopEmptyText.Visibility = Visibility.Visible;
            ProcessTableTitle.Text = "后台进程";
            _allTableItems = new List<TopMemoryItem>();
            ApplyFilter();
            return;
        }
        ProcessTableList.Visibility = Visibility.Visible;
        ProcessTableBorder.Visibility = Visibility.Visible;
        TopEmptyText.Visibility = Visibility.Collapsed;
        ProcessTableTitle.Text = $"后台进程 ({plan.Candidates.Count})";

        var cpuMap = new Dictionary<int, float>();
        if (_monitorVm?.TopProcesses != null)
            foreach (var p in _monitorVm.TopProcesses) cpuMap[p.Id] = p.CpuPercent;

        _allTableItems = plan.Candidates
            .OrderBy(p => p.RiskScore)
            .ThenByDescending(p => p.MemoryMb)
            .Select(p => new TopMemoryItem
            {
                Id = p.Id,
                Name = p.ProcessName,
                Detail = BuildProcessDetail(p),
                Memory = p.MemoryMb > 0 ? p.MemoryMb + " MB" : "-",
                Cpu = cpuMap.TryGetValue(p.Id, out var cpu) ? $"{cpu:F1}%" : "--",
                StatusDot = GetStatusDot(p.Action),
                StatusText = GetStatusText(p.Action),
                Action = p.Action,
                ActionBg = ColorHelper.GetActionBackground(p.Action),
                Suggestion = GetSuggestionText(p.Action),
                SuggestionFg = GetSuggestionForeground(p.Action),
                IconGlyph = GetProcessGlyph(p),
                IconBg = GetProcessIconBackground(p.Action),
                IconFg = GetSuggestionForeground(p.Action),
                RiskScore = p.RiskScore
            }).ToList();
        ApplyFilter();
    }

    private static string BuildProcessDetail(ProcessRecord p)
    {
        if (!string.IsNullOrWhiteSpace(p.MainWindowTitle)) return p.MainWindowTitle;
        if (!string.IsNullOrWhiteSpace(p.Path)) return p.Path;
        if (!string.IsNullOrWhiteSpace(p.ParentProcessName)) return "父进程 " + p.ParentProcessName;
        return "后台用户进程";
    }

    private SolidColorBrush GetStatusDot(string action)
    {
        if (action == ProcessPlanner.ActionGraceful) return ColorHelper.AccentLight;
        if (action == ProcessPlanner.ActionForce) return ColorHelper.Copper;
        if (action == ProcessPlanner.ActionReport) return ColorHelper.Danger;
        return ColorHelper.MutedSoft;
    }

    private string GetStatusText(string action)
    {
        if (action == ProcessPlanner.ActionGraceful) return "运行中";
        if (action == ProcessPlanner.ActionForce) return "后台驻留";
        if (action == ProcessPlanner.ActionReport) return "高风险";
        return "未知";
    }

    private string GetSuggestionText(string action)
    {
        if (action == ProcessPlanner.ActionGraceful) return "建议关闭";
        if (action == ProcessPlanner.ActionForce) return "可强制";
        if (action == ProcessPlanner.ActionReport) return "仅提示";
        return "观察";
    }

    private SolidColorBrush GetSuggestionForeground(string action)
    {
        if (action == ProcessPlanner.ActionGraceful) return ColorHelper.AccentLight;
        if (action == ProcessPlanner.ActionForce) return ColorHelper.Copper;
        if (action == ProcessPlanner.ActionReport) return ColorHelper.Danger;
        return ColorHelper.Info;
    }

    private SolidColorBrush GetProcessIconBackground(string action)
    {
        if (action == ProcessPlanner.ActionGraceful) return ColorHelper.CyanSoft;
        if (action == ProcessPlanner.ActionForce) return ColorHelper.CopperSoft;
        if (action == ProcessPlanner.ActionReport) return ColorHelper.DangerSoft;
        return ColorHelper.MutedSoft;
    }

    private static string GetProcessGlyph(ProcessRecord process)
    {
        if (process.Action == ProcessPlanner.ActionForce) return "\uE7EF";
        if (process.Action == ProcessPlanner.ActionReport) return "\uE7BA";
        if (process.HasWindow) return "\uE7F4";
        return "\uE9D5";
    }

    private void ApplyFilter()
    {
        if (ProcessTableList == null) return;
        if (_allTableItems == null || _allTableItems.Count == 0)
        { ProcessTableList.ItemsSource = null; return; }
        string search = SearchBox?.Text?.Trim() ?? "";
        int filterIndex = FilterCombo?.SelectedIndex ?? 0;
        var filtered = _allTableItems.AsEnumerable();
        if (!string.IsNullOrEmpty(search))
            filtered = filtered.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (filterIndex == 1) filtered = filtered.Where(p => p.Action == ProcessPlanner.ActionGraceful);
        else if (filterIndex == 2) filtered = filtered.Where(p => p.Action == ProcessPlanner.ActionForce);
        else if (filterIndex == 3) filtered = filtered.Where(p => p.Action == ProcessPlanner.ActionReport);
        ProcessTableList.ItemsSource = filtered.ToList();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { ApplyFilter(); }
    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { ApplyFilter(); }

    private async void CloseProcess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int pid)
        {
            var plan = AppState.CurrentPlan;
            var target = plan?.Candidates?.FirstOrDefault(p => p.Id == pid);
            if (target == null) return;
            var dialog = new ContentDialog
            {
                Title = "确认关闭",
                Content = $"即将关闭 {target.ProcessName} (PID {pid})，是否继续？",
                PrimaryButtonText = "关闭",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close
            };
            dialog.XamlRoot = this.XamlRoot;
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;
            AppState.AddLog(DateTime.Now.ToString("HH:mm:ss") + " [INFO] 开始关闭：" + target.ProcessName + " (" + pid + ")。");
            try
            {
                await Task.Run(() =>
                {
                    var miniPlan = new ClosePlan
                    {
                        Config = plan.Config,
                        Candidates = new List<ProcessRecord> { target },
                        Protected = plan.Protected,
                        Skipped = new List<ProcessRecord>()
                    };
                    CloseExecutor.Execute(miniPlan,
                        line => _dispatcher.TryEnqueue(() => AppState.AddLog(line)),
                        CancellationToken.None);
                });
                await ScanWithTimeoutAsync();
                UpdateStatsFromPlan();
            }
            catch (Exception ex) { AppState.AddLog("[错误] 关闭失败：" + ex.Message); }
        }
    }

    private async void CloseAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (!AppState.HasPlan || AppState.CurrentPlan.Candidates == null || AppState.CurrentPlan.Candidates.Count == 0) return;
        var dialog = new ContentDialog
        {
            Title = "确认执行",
            Content = string.Format("即将关闭 {0} 个候选进程，是否继续？", AppState.CurrentPlan.Candidates.Count),
            PrimaryButtonText = "执行关闭",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };
        dialog.XamlRoot = this.XamlRoot;
        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        AppState.IsExecuting = true;
        AppState.ClearLog();
        _closeCts = new CancellationTokenSource();
        _logViewModels.Clear();
        OptimizeButton.IsEnabled = false;
        ScanButton.IsEnabled = false;
        CancelCloseButton.Visibility = Visibility.Visible;
        ExecProgressPanel.Visibility = Visibility.Visible;
        ExecProgress.IsActive = true;
        ExecStatusText.Text = "正在执行渐进式清理...";
        ResultBar.Visibility = Visibility.Collapsed;
        ResultBar.IsOpen = false;
        SetStatus("executing", "正在清理后台进程...");

        try
        {
            CloseResult closeResult = null;
            await Task.Run(() =>
            {
                closeResult = CloseExecutor.Execute(
                    AppState.CurrentPlan,
                    line => { _dispatcher.TryEnqueue(() => AppState.AddLog(line)); },
                    _closeCts.Token);
            });
            AppState.LastResult = closeResult;
            AppState.IsExecuting = false;
            AppState.LoadPreferences();
            AppState.Preferences.RecordCleanup(closeResult);
            ShowResultBar(closeResult);
            await AutoRescanAfterCleanup();
        }
        catch (OperationCanceledException)
        {
            AppState.AddLog("[取消] 用户取消了操作");
            ExecStatusText.Text = "已取消";
            SetStatus("idle", "准备就绪");
        }
        catch (Exception ex)
        {
            AppState.AddLog("[错误] " + ex.Message);
            ExecStatusText.Text = "执行失败";
            SetStatus("error", "执行出错");
        }
        finally
        {
            AppState.IsExecuting = false;
            OptimizeButton.IsEnabled = true;
            ScanButton.IsEnabled = true;
            CancelCloseButton.Visibility = Visibility.Collapsed;
            ExecProgress.IsActive = false;
            ExecProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async Task AutoRescanAfterCleanup()
    {
        try
        {
            ExecStatusText.Text = "正在刷新状态...";
            await Task.Delay(300);
            await ScanWithTimeoutAsync();
            UpdateStatsFromPlan();
        }
        catch (Exception ex) { ExecStatusText.Text = "刷新失败：" + ex.Message; }
    }

    private void CancelCloseButton_Click(object sender, RoutedEventArgs e)
    {
        _closeCts?.Cancel();
        ExecStatusText.Text = "正在取消...";
    }

    private void ShowResultBar(CloseResult result)
    {
        int graceful = result.GracefulClosed?.Count ?? 0;
        int forced = result.Forced?.Count ?? 0;
        int remaining = result.Remaining?.Count ?? 0;
        int reported = result.ReportOnly?.Count ?? 0;
        ResultBar.Title = string.Format("温和 {0}，强制 {1}，仍在运行 {2}，跳过 {3}", graceful, forced, remaining, reported);
        ResultBar.Message = "";
        ResultBar.Severity = (graceful + forced > 0) ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ResultBar.Visibility = Visibility.Visible;
        ResultBar.IsOpen = true;
    }

    private static string FormatMemoryEstimate(long mb)
    {
        if (mb >= 1024) return $"{(double)mb / 1024:F1} GB";
        return $"{mb} MB";
    }

    private static string FormatMemoryGb(long mb)
    {
        if (mb <= 0) return "0 GB";
        return $"{(double)mb / 1024:F1} GB";
    }
}
