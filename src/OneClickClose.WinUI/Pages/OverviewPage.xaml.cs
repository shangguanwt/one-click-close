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
using Microsoft.UI.Xaml.Media.Animation;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.Services;
using OneClickClose.WinUI.ViewModels;

namespace OneClickClose.WinUI.Pages;

public sealed partial class OverviewPage : Page
{
    private const int HighRiskPreviewScore = 75;

    private CancellationTokenSource _closeCts;
    private readonly DispatcherQueue _dispatcher;
    private bool _initialScanDone;
    private NotifyCollectionChangedEventHandler _logHandler;
    private readonly ObservableCollection<LogLineViewModel> _logViewModels = new();
    private readonly ObservableCollection<TopMemoryItem> _backgroundPreviewItems = new();
    private readonly ObservableCollection<TopMemoryItem> _riskPreviewItems = new();
    private ViewModels.SystemMonitorViewModel _monitorVm;
    private List<TopMemoryItem> _allTableItems = new();
    private int _lastStartupItems = -1;
    private int _scanSessionId;
    private bool _isScanning;
    private long _lastPlanMemoryEstimate;
    private int _lastPlanCandidateCount;

    public OverviewPage()
    {
        this.InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        LogList.ItemsSource = _logViewModels;
        BackgroundPreviewList.ItemsSource = _backgroundPreviewItems;
        RiskPreviewList.ItemsSource = _riskPreviewItems;
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

        SetGridColumns(PreviewGrid, narrow ? 1 : 2);
        Place(PreviewGrid, BackgroundPreviewCard, 0, 0);
        Place(PreviewGrid, RiskPreviewCard, narrow ? 1 : 0, narrow ? 0 : 1);

        SetGridColumns(InsightGrid, narrow ? 1 : medium ? 2 : 3);
        Place(InsightGrid, StartupInsightCard, 0, 0);
        Place(InsightGrid, HabitInsightCard, narrow ? 1 : 0, narrow ? 0 : 1);
        Place(InsightGrid, EfficiencyInsightCard, narrow ? 2 : medium ? 1 : 0, narrow ? 0 : medium ? 0 : 2, medium ? 2 : 1);

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
        ApplyResponsiveLayout(ActualWidth);
        _dispatcher.TryEnqueue(() => ApplyResponsiveLayout(ActualWidth));

        _logHandler = OnLogLinesChanged;
        AppState.LogLines.CollectionChanged += _logHandler;

        _monitorVm = new ViewModels.SystemMonitorViewModel();
        _monitorVm.PropertyChanged += Monitor_PropertyChanged;
        _monitorVm.StartMonitoring();

        if (AppState.HasPlan)
        {
            UpdateStatsFromPlan();
            return;
        }

        if (!_initialScanDone && !AppState.IsExecuting)
        {
            _initialScanDone = true;
            _ = RunScanAsync();
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

                TempChipText.Text = FormatTemperatureChip();
                ToolTipService.SetToolTip(TempChipCard, BuildTemperatureTooltip());
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
        await RunScanAsync();
    }

    private async Task RunScanAsync()
    {
        if (_isScanning)
        {
            return;
        }

        _isScanning = true;
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
            ScanTimeText.Text = "扫描中...";
            HeroInsightText.Text = "正在检测可优化项目，请稍候";
            UpdateHeroBadges(0, 0, _lastStartupItems);

            await ScanWithTimeoutAsync();

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
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            ScanButton.IsEnabled = true;
            _isScanning = false;
        }
    }

    private async Task ScanWithTimeoutAsync()
    {
        using CancellationTokenSource scanCts = new CancellationTokenSource();
        Task scanTask = AppState.ScanAsync(AppState.ConfigPath, scanCts.Token);
        Task timeoutTask = Task.Delay(TimeSpan.FromSeconds(20));
        if (await Task.WhenAny(scanTask, timeoutTask) != scanTask)
        {
            scanCts.Cancel();
            throw new TimeoutException("扫描超时，请稍后重试。");
        }

        await scanTask;
    }
    private void UpdateStatsFromPlan()
    {
        var plan = AppState.CurrentPlan;
        int candidateCount = plan.Candidates?.Count ?? 0;
        int groupCount = AppState.CandidateRows?.Count ?? 0;
        int gracefulCount = 0;
        long memoryEstimate = 0;
        if (plan.Candidates != null)
        {
            gracefulCount = plan.Candidates.Count(p => p.Action == ProcessPlanner.ActionGraceful);
            memoryEstimate = plan.Candidates.Sum(p => p.MemoryMb);
        }
        _lastPlanCandidateCount = candidateCount;
        _lastPlanMemoryEstimate = memoryEstimate;

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
        HeroInsightText.Text = $"已检测到 {groupCount} 个应用组 / {candidateCount} 个可优化进程，预计释放 {memoryText}";
        UpdateHeroBadges(groupCount, candidateCount, -1);

        if (candidateCount > 0)
            SetStatus("ready", string.Format("发现 {0} 个可安全优化项目", candidateCount));
        else
            SetStatus("clean", "当前状态良好，无需清理");

        ScanTimeText.Text = "扫描于 " + AppState.LastScanTime.ToString("HH:mm:ss");
        OptimizeButton.IsEnabled = candidateCount > 0;
        UpdateInsightCards(plan, _lastStartupItems);
        UpdatePreviewCards(plan);
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
            UpdateHeroBadges(AppState.CandidateRows?.Count ?? 0, candidateCount, startupItems);
            UpdateTrendText(TrendStartup, ScanTrendTracker.KeyStartupItems, startupItems);
            ScanTrendTracker.RecordScan(candidateCount, memoryEstimate, gracefulCount, startupItems);
            UpdateInsightCards(AppState.CurrentPlan, startupItems);
        }
        catch
        {
            if (scanSessionId != _scanSessionId)
            {
                return;
            }

            _lastStartupItems = -1;
            ProtectedCountText.Text = "--";
            UpdateHeroBadges(AppState.CandidateRows?.Count ?? 0, candidateCount, -1);
            TrendStartup.Text = "--";
            TrendStartup.Foreground = (SolidColorBrush)Application.Current.Resources["SubtleTextBrush"];
            UpdateInsightCards(AppState.CurrentPlan, -1);
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
        UpdateHeroStateCopy(state);
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

    private void UpdateHeroStateCopy(string state)
    {
        switch (state)
        {
            case "scanning":
                HeroTitleText.Text = "正在扫描后台资源";
                HeroSubtitleText.Text = "请稍候，正在识别可优化应用";
                break;
            case "executing":
                HeroTitleText.Text = "正在释放后台资源";
                HeroSubtitleText.Text = "保持当前窗口打开以查看结果";
                break;
            case "ready":
                HeroTitleText.Text = "发现可优化后台进程";
                HeroSubtitleText.Text = "一键优化前可先查看风险建议";
                break;
            case "error":
                HeroTitleText.Text = "扫描遇到问题";
                HeroSubtitleText.Text = "请稍后重新扫描或检查权限";
                break;
            case "clean":
            default:
                HeroTitleText.Text = "系统状态良好";
                HeroSubtitleText.Text = "后台负载保持在安全范围";
                break;
        }
    }

    private void UpdateHeroBadges(int groupCount, int candidateCount, int startupItems)
    {
        HeroGroupBadgeText.Text = "应用组 " + Math.Max(0, groupCount);
        HeroProcessBadgeText.Text = "可优化 " + Math.Max(0, candidateCount);
        HeroStartupBadgeText.Text = startupItems >= 0 ? "启动项 " + startupItems : "启动项 --";
    }

    private void UpdatePreviewCards(ClosePlan plan)
    {
        var groups = AppState.CandidateRows ?? ProcessPlanner.GroupRows(plan?.Candidates ?? new List<ProcessRecord>());

        int backgroundTotal = groups.Count(row => row.Action == ProcessPlanner.ActionGraceful);
        int riskTotal = groups.Count(IsRiskPreviewRow);

        ReplacePreviewItems(
            _backgroundPreviewItems,
            groups
                .Where(row => row.Action == ProcessPlanner.ActionGraceful)
                .OrderByDescending(row => row.MemoryMb)
                .ThenBy(row => row.RiskScore)
                .Take(4)
                .Select(CreatePreviewItem));

        ReplacePreviewItems(
            _riskPreviewItems,
            groups
                .Where(IsRiskPreviewRow)
                .OrderByDescending(row => row.RiskScore)
                .ThenByDescending(row => row.MemoryMb)
                .Take(4)
                .Select(CreatePreviewItem));

        BackgroundPreviewCountText.Text = backgroundTotal > 0 ? backgroundTotal + " 组" : "暂无";
        RiskPreviewCountText.Text = riskTotal > 0 ? riskTotal + " 组" : "暂无";
        BackgroundPreviewEmptyText.Visibility = _backgroundPreviewItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RiskPreviewEmptyText.Visibility = _riskPreviewItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PlayPreviewEntranceAnimation();
    }

    private static bool IsRiskPreviewRow(ProcessGroupRow row)
    {
        return row.Action == ProcessPlanner.ActionForce
            || row.Action == ProcessPlanner.ActionReport
            || row.RiskScore >= HighRiskPreviewScore;
    }

    private TopMemoryItem CreatePreviewItem(ProcessGroupRow row)
    {
        return new TopMemoryItem
        {
            Id = row.Children.FirstOrDefault()?.Id ?? 0,
            GroupKey = row.AppKey,
            Name = row.Process,
            Detail = BuildPreviewDetail(row),
            Memory = row.MemoryMb > 0 ? FormatMemoryEstimate(row.MemoryMb) : "-",
            Instances = row.Count + " 个",
            StatusDot = GetStatusDot(row.Action),
            StatusText = GetStatusText(row.Action),
            Action = row.Action,
            ActionBg = ColorHelper.GetActionBackground(row.Action),
            Suggestion = string.IsNullOrWhiteSpace(row.HabitHint) ? GetSuggestionText(row.Action) : "习惯建议",
            SuggestionFg = GetSuggestionForeground(row.Action),
            BadgeText = string.IsNullOrWhiteSpace(row.HabitHint) ? GetSuggestionText(row.Action) : "习惯建议",
            MetaText = row.Count + " 个实例 · 风险 " + row.RiskScore,
            RiskText = BuildRiskText(row.RiskScore),
            RiskFg = GetRiskForeground(row),
            IconSource = ProcessIconProvider.GetIconSource(row),
            IconGlyph = GetGroupGlyph(row),
            IconBg = GetProcessIconBackground(row.Action),
            IconFg = GetSuggestionForeground(row.Action),
            RiskScore = row.RiskScore
        };
    }

    private static string BuildPreviewDetail(ProcessGroupRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.UsageHint)) return row.UsageHint;
        if (!string.IsNullOrWhiteSpace(row.HabitHint)) return row.HabitHint;
        if (!string.IsNullOrWhiteSpace(row.Note)) return row.Note;
        if (row.HasWindow) return "窗口应用，可温和关闭";
        return "后台常驻应用";
    }

    private static string BuildRiskText(int riskScore)
    {
        if (riskScore >= HighRiskPreviewScore) return "高风险";
        if (riskScore >= 45) return "中风险";
        return "低风险";
    }

    private static SolidColorBrush GetRiskForeground(ProcessGroupRow row)
    {
        if (row.Action == ProcessPlanner.ActionReport || row.RiskScore >= HighRiskPreviewScore) return ColorHelper.Danger;
        if (row.Action == ProcessPlanner.ActionForce || row.RiskScore >= 45) return ColorHelper.Copper;
        return ColorHelper.Safe;
    }

    private static void ReplacePreviewItems(ObservableCollection<TopMemoryItem> target, IEnumerable<TopMemoryItem> source)
    {
        target.Clear();
        foreach (TopMemoryItem item in source)
        {
            target.Add(item);
        }
    }

    private void PlayPreviewEntranceAnimation()
    {
        if (PreviewGrid == null)
        {
            return;
        }

        var transform = PreviewGrid.RenderTransform as TranslateTransform;
        if (transform == null)
        {
            transform = new TranslateTransform();
            PreviewGrid.RenderTransform = transform;
        }

        PreviewGrid.Opacity = 0;
        transform.Y = 10;

        var storyboard = new Storyboard();
        var opacity = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(190)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(opacity, PreviewGrid);
        Storyboard.SetTargetProperty(opacity, "Opacity");

        var offset = new DoubleAnimation
        {
            From = 10,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(210)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(offset, transform);
        Storyboard.SetTargetProperty(offset, "Y");

        storyboard.Children.Add(opacity);
        storyboard.Children.Add(offset);
        storyboard.Begin();
    }

    private void ViewAllCandidates_Click(object sender, RoutedEventArgs e)
    {
        AppState.RequestNavigation("candidates");
    }

    private void UpdateInsightCards(ClosePlan plan, int startupItems)
    {
        int groupCount = AppState.CandidateRows?.Count ?? 0;
        long memory = plan?.Candidates?.Sum(p => p.MemoryMb) ?? 0;
        int highMemoryGroups = AppState.CandidateRows?.Count(r => r.MemoryMb >= 512) ?? 0;

        if (startupItems >= 0)
        {
            StartupImpactText.Text = startupItems == 0
                ? "未发现明显开机启动项压力"
                : startupItems + " 个启动项，建议优先检查常驻聊天、同步和更新组件";
        }
        else
        {
            StartupImpactText.Text = "启动项仍在统计中，稍后刷新建议";
        }

        var suggestions = AppState.Preferences.BuildSuggestions(plan?.Config ?? AppConfig.CreateDefault());
        HabitSuggestionText.Text = suggestions.Count > 0
            ? suggestions[0].ProcessName + "：" + suggestions[0].Reason
            : "暂无强习惯信号；确认/取消/跳过会在本机形成建议";

        EfficiencyModeText.Text = groupCount == 0
            ? "当前无需关闭；后续可接入 Efficiency Mode / 降低后台优先级"
            : "发现 " + highMemoryGroups + " 个高占用应用组，低风险模式仍会先确认再执行";

        if (memory > 0 && groupCount > 0)
        {
            MemoryEstimateHint.Text = groupCount + " 组应用约占用 " + FormatMemoryEstimate(memory);
        }
    }

    private string FormatTemperatureChip()
    {
        if (_monitorVm.CpuTemperatureC.HasValue)
        {
            return $"{_monitorVm.CpuTemperatureC.Value:F0}°C";
        }

        if (_monitorVm.TemperatureC.HasValue)
        {
            return $"{_monitorVm.TemperatureC.Value:F0}°C";
        }

        string reason = _monitorVm.TemperatureUnavailableReason ?? string.Empty;
        if (reason.Contains("授权", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("admin", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("access", StringComparison.OrdinalIgnoreCase))
        {
            return "未授权";
        }

        return "无传感器";
    }

    private string BuildTemperatureTooltip()
    {
        if (_monitorVm == null)
        {
            return "温度监控尚未启动";
        }

        var lines = new List<string>
        {
            "来源：" + (string.IsNullOrWhiteSpace(_monitorVm.TemperatureSource) ? "未确定" : _monitorVm.TemperatureSource),
            "CPU：" + FormatNullableTemperature(_monitorVm.CpuTemperatureC),
            "GPU：" + FormatNullableTemperature(_monitorVm.GpuTemperatureC),
            "主板：" + FormatNullableTemperature(_monitorVm.MotherboardTemperatureC)
        };

        if (!string.IsNullOrWhiteSpace(_monitorVm.TemperatureUnavailableReason))
        {
            lines.Add("原因：" + _monitorVm.TemperatureUnavailableReason);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatNullableTemperature(float? value)
    {
        return value.HasValue ? $"{value.Value:F0}°C" : "未检测到";
    }

    private void ShowProcessTable(ClosePlan plan)
    {
        var groups = AppState.CandidateRows ?? ProcessPlanner.GroupRows(plan.Candidates ?? new List<ProcessRecord>());
        if (groups.Count == 0)
        {
            ProcessTableList.Visibility = Visibility.Collapsed;
            ProcessTableBorder.Visibility = Visibility.Collapsed;
            TopEmptyText.Visibility = Visibility.Visible;
            ProcessTableTitle.Text = "后台进程";
            _allTableItems = new List<TopMemoryItem>();
            ApplyFilter();
            return;
        }
        ProcessTableList.Visibility = Visibility.Collapsed;
        ProcessTableBorder.Visibility = Visibility.Collapsed;
        TopEmptyText.Visibility = Visibility.Collapsed;
        ProcessTableTitle.Text = $"后台进程 ({groups.Count} 组 / {plan.Candidates?.Count ?? 0} 个)";

        _allTableItems = groups
            .OrderBy(row => row.RiskScore)
            .ThenByDescending(row => row.MemoryMb)
            .Select(row => new TopMemoryItem
            {
                Id = row.Children.FirstOrDefault()?.Id ?? 0,
                GroupKey = row.AppKey,
                Name = row.Process,
                Detail = BuildGroupDetail(row),
                Memory = row.MemoryMb > 0 ? row.MemoryMb + " MB" : "-",
                Instances = row.Count + " 个",
                StatusDot = GetStatusDot(row.Action),
                StatusText = GetStatusText(row.Action),
                Action = row.Action,
                ActionBg = ColorHelper.GetActionBackground(row.Action),
                Suggestion = string.IsNullOrWhiteSpace(row.HabitHint) ? GetSuggestionText(row.Action) : "习惯建议",
                SuggestionFg = GetSuggestionForeground(row.Action),
                IconSource = ProcessIconProvider.GetIconSource(row),
                IconGlyph = GetGroupGlyph(row),
                IconBg = GetProcessIconBackground(row.Action),
                IconFg = GetSuggestionForeground(row.Action),
                RiskScore = row.RiskScore
            }).ToList();
        ApplyFilter();
    }

    private static string BuildGroupDetail(ProcessGroupRow row)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(row.UsageHint)) parts.Add(row.UsageHint);
        if (!string.IsNullOrWhiteSpace(row.HabitHint)) parts.Add(row.HabitHint);
        if (!string.IsNullOrWhiteSpace(row.Note)) parts.Add(row.Note);
        return parts.Count == 0 ? "后台用户应用组" : string.Join(" · ", parts);
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

    private static string GetGroupGlyph(ProcessGroupRow row)
    {
        if (row.Action == ProcessPlanner.ActionForce) return "\uE7EF";
        if (row.Action == ProcessPlanner.ActionReport) return "\uE7BA";
        if (row.HasWindow) return "\uE7F4";
        return "\uE9D5";
    }

    private void ApplyFilter()
    {
        if (ProcessTableList == null) return;
        if (_allTableItems == null || _allTableItems.Count == 0)
        { ProcessTableList.ItemsSource = null; return; }
        ProcessTableList.ItemsSource = ProcessTableFilter.Apply(
            _allTableItems,
            SearchBox?.Text,
            FilterCombo?.SelectedIndex ?? 0);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { ApplyFilter(); }
    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { ApplyFilter(); }

    private async void CloseProcess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string groupKey || string.IsNullOrWhiteSpace(groupKey))
        {
            return;
        }

        var plan = AppState.CurrentPlan;
        var row = AppState.CandidateRows?.FirstOrDefault(r => string.Equals(r.AppKey, groupKey, StringComparison.Ordinal));
        var targets = row?.Children ?? new List<ProcessRecord>();
        if (plan == null || targets.Count == 0)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "\u786e\u8ba4\u5173\u95ed",
            Content = BuildGroupClosePreview(row),
            PrimaryButtonText = "\u5173\u95ed",
            CloseButtonText = "\u53d6\u6d88",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            AppState.Preferences.RecordCloseCanceled(targets);
            return;
        }

        AppState.Preferences.RecordCloseConfirmed(targets);
        AppState.AddLog(DateTime.Now.ToString("HH:mm:ss") + " [INFO] 开始优化应用组：" + row.Process + "（" + targets.Count + " 个进程）。");
        try
        {
            await Task.Run(() =>
            {
                var miniPlan = new ClosePlan
                {
                    Config = plan.Config,
                    Candidates = targets,
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
        catch (Exception ex)
        {
            AppState.AddLog("[\u9519\u8bef] \u5173\u95ed\u5931\u8d25\uff1a" + ex.Message);
        }
    }

    private string BuildGroupClosePreview(ProcessGroupRow row)
    {
        var miniPlan = new ClosePlan
        {
            Config = AppState.CurrentPlan?.Config,
            Candidates = row.Children ?? new List<ProcessRecord>(),
            Protected = new List<ProcessRecord>(),
            Skipped = new List<ProcessRecord>()
        };

        var lines = new List<string>
        {
            $"应用组：{row.Process}",
            $"实例数：{row.Count}",
            $"动作：{row.Action}",
            $"最高风险分：{row.RiskScore}",
            $"预计释放：{FormatMemoryEstimate(Math.Max(0, row.MemoryMb))}"
        };

        if (!string.IsNullOrWhiteSpace(row.UsageHint))
        {
            lines.Add("占用解释：" + row.UsageHint);
        }

        if (!string.IsNullOrWhiteSpace(row.HabitHint))
        {
            lines.Add("习惯建议：" + row.HabitHint);
        }

        ClosePlanPreview preview = ClosePlanPreview.FromPlan(miniPlan, sampleLimit: 3);
        lines.Add("");
        lines.Add(preview.ToDialogMessage());
        return string.Join(Environment.NewLine, lines);
    }

    private string BuildSingleClosePreview(ProcessRecord target)
    {
        var miniPlan = new ClosePlan
        {
            Config = AppState.CurrentPlan?.Config,
            Candidates = new List<ProcessRecord> { target },
            Protected = new List<ProcessRecord>(),
            Skipped = new List<ProcessRecord>()
        };

        var lines = new List<string>
        {
            $"\u8fdb\u7a0b\uff1a{target.ProcessName} ({target.Id})",
            $"\u52a8\u4f5c\uff1a{target.Action}",
            $"\u98ce\u9669\u5206\uff1a{target.RiskScore}",
            $"\u9884\u8ba1\u91ca\u653e\uff1a{FormatMemoryEstimate(Math.Max(0, target.MemoryMb))}"
        };

        if (!string.IsNullOrWhiteSpace(target.MainWindowTitle))
        {
            lines.Add("\u7a97\u53e3\uff1a" + target.MainWindowTitle);
        }

        if (!string.IsNullOrWhiteSpace(target.Path))
        {
            lines.Add("\u8def\u5f84\uff1a" + target.Path);
        }

        ClosePlanPreview preview = ClosePlanPreview.FromPlan(miniPlan, sampleLimit: 1);
        if (preview.HasForceRisk)
        {
            lines.Add("\u8bf7\u786e\u8ba4\uff1a\u8be5\u64cd\u4f5c\u53ef\u80fd\u89e6\u53d1\u5f3a\u5236\u5173\u95ed\uff0c\u672a\u4fdd\u5b58\u6570\u636e\u53ef\u80fd\u4e22\u5931\u3002");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async void CloseAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (!AppState.HasPlan || AppState.CurrentPlan.Candidates == null || AppState.CurrentPlan.Candidates.Count == 0)
        {
            return;
        }

        ClosePlanPreview preview = ClosePlanPreview.FromPlan(AppState.CurrentPlan);
        var dialog = new ContentDialog
        {
            Title = preview.HasForceRisk ? "\u786e\u8ba4\u6267\u884c\uff1a\u5305\u542b\u5f3a\u5236\u5173\u95ed" : "\u786e\u8ba4\u6267\u884c",
            Content = preview.ToDialogMessage(),
            PrimaryButtonText = "\u6267\u884c\u5173\u95ed",
            CloseButtonText = "\u53d6\u6d88",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            AppState.Preferences.RecordCloseCanceled(AppState.CurrentPlan.Candidates);
            return;
        }

        AppState.Preferences.RecordCloseConfirmed(AppState.CurrentPlan.Candidates);
        AppState.IsExecuting = true;
        AppState.ClearLog();
        _closeCts = new CancellationTokenSource();
        _logViewModels.Clear();
        OptimizeButton.IsEnabled = false;
        ScanButton.IsEnabled = false;
        CancelCloseButton.Visibility = Visibility.Visible;
        ExecProgressPanel.Visibility = Visibility.Visible;
        ExecProgress.IsActive = true;
        ExecStatusText.Text = "\u6b63\u5728\u6267\u884c\u6e10\u8fdb\u5f0f\u6e05\u7406...";
        ResultBar.Visibility = Visibility.Collapsed;
        ResultBar.IsOpen = false;
        SetStatus("executing", "\u6b63\u5728\u6e05\u7406\u540e\u53f0\u8fdb\u7a0b...");

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
            AppState.AddLog("[\u53d6\u6d88] \u7528\u6237\u53d6\u6d88\u4e86\u64cd\u4f5c");
            ExecStatusText.Text = "\u5df2\u53d6\u6d88";
            SetStatus("idle", "\u51c6\u5907\u5c31\u7eea");
        }
        catch (Exception ex)
        {
            AppState.AddLog("[\u9519\u8bef] " + ex.Message);
            ExecStatusText.Text = "\u6267\u884c\u5931\u8d25";
            SetStatus("error", "\u6267\u884c\u51fa\u9519");
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
            ExecStatusText.Text = "\u6b63\u5728\u5237\u65b0\u72b6\u6001...";
            await Task.Delay(300);
            await ScanWithTimeoutAsync();
            UpdateStatsFromPlan();
        }
        catch (Exception ex)
        {
            ExecStatusText.Text = "\u5237\u65b0\u5931\u8d25\uff1a" + ex.Message;
        }
    }

    private void CancelCloseButton_Click(object sender, RoutedEventArgs e)
    {
        _closeCts?.Cancel();
        ExecStatusText.Text = "\u6b63\u5728\u53d6\u6d88...";
    }

    private void ShowResultBar(CloseResult result)
    {
        int graceful = result.GracefulClosed?.Count ?? 0;
        int forced = result.Forced?.Count ?? 0;
        int remaining = result.Remaining?.Count ?? 0;
        int reported = result.ReportOnly?.Count ?? 0;
        long releasedMb = (result.GracefulClosed?.Sum(p => p.MemoryMb) ?? 0)
            + (result.Forced?.Sum(p => p.MemoryMb) ?? 0);
        ResultBar.Title = string.Format("\u6e29\u548c {0}\uff0c\u5f3a\u5236 {1}\uff0c\u4ecd\u5728\u8fd0\u884c {2}\uff0c\u8df3\u8fc7 {3}", graceful, forced, remaining, reported);
        ResultBar.Message = "清理前预计 " + FormatMemoryEstimate(_lastPlanMemoryEstimate)
            + "，本次已关闭 " + (graceful + forced) + " 个，约释放 " + FormatMemoryEstimate(releasedMb)
            + "；仍在运行 " + remaining + " 个。";
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
