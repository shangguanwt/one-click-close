using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.ViewModels;

namespace OneClickClose.WinUI.Pages;

public sealed partial class CandidateProcessesPage : Page
{
    private const int HighRiskScoreThreshold = 75;

    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherQueueTimer _searchDebounceTimer;
    private List<CandidateRowViewModel> _allRows;
    private List<CandidateRowViewModel> _visibleRows;
    private List<CandidateRowViewModel> _previewRows;
    private string _activeFilter = "all";
    private bool _isRefreshing;
    private bool _isLoaded;
    private DateTime _loadedScanTime;
    private int _bindVersion;
    private int _refreshVersion;
    private bool _isBindingComplete;

    public CandidateProcessesPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _searchDebounceTimer = _dispatcher.CreateTimer();
        _searchDebounceTimer.Interval = TimeSpan.FromMilliseconds(200);
        _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        if (_allRows != null && _loadedScanTime == AppState.LastScanTime && _isBindingComplete)
        {
            return;
        }

        ShowDeferredLoadingState();
        await Task.Delay(16);
        if (!_isLoaded)
        {
            return;
        }

        RefreshData();
    }
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        _searchDebounceTimer.Stop();
    }

    public void FocusSearch() => SearchBox?.Focus(FocusState.Programmatic);

    private void ShowDeferredLoadingState()
    {
        if (!AppState.HasPlan || AppState.AllProcessRows == null || AppState.AllProcessRows.Count == 0)
        {
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        NoResultsState.Visibility = Visibility.Collapsed;
        ProcessList.Visibility = Visibility.Collapsed;
        CountBadge.Visibility = Visibility.Visible;
        CountText.Text = "加载中...";
        PreviewAppsText.Text = "正在加载后台进程列表...";
    }

    private async void RefreshData()
    {
        int refreshVersion = ++_refreshVersion;
        if (!AppState.HasPlan || AppState.AllProcessRows == null || AppState.AllProcessRows.Count == 0)
        {
            _bindVersion++;
            _isBindingComplete = true;
            EmptyState.Visibility = Visibility.Visible;
            NoResultsState.Visibility = Visibility.Collapsed;
            ProcessList.Visibility = Visibility.Collapsed;
            CountBadge.Visibility = Visibility.Collapsed;
            _allRows = null;
            _visibleRows = null;
            _previewRows = null;
            UpdateClosePreview();
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        ProcessList.Visibility = Visibility.Visible;
        _allRows = new List<CandidateRowViewModel>(AppState.AllProcessRows.Count);
        _visibleRows = new List<CandidateRowViewModel>();
        ProcessList.ItemsSource = null;
        _isBindingComplete = false;
        _loadedScanTime = AppState.LastScanTime;
        CountBadge.Visibility = Visibility.Visible;
        int totalChildren = AppState.AllProcessRows.Sum(r => r.Children?.Count ?? 0);
        CountText.Text = _allRows.Count + " 组 / " + totalChildren + " 个";
        string query = SearchFilterHelper.ExtractQuery(SearchBox?.Text);
        IReadOnlyList<ProcessGroupRow> sourceRows = AppState.AllProcessRows.ToList();
        for (int i = 0; i < sourceRows.Count; i++)
        {
            if (!_isLoaded || refreshVersion != _refreshVersion)
            {
                return;
            }

            CandidateRowViewModel vm = new(sourceRows[i], AppState.CurrentPlan?.Config);
            _allRows.Add(vm);
            if (MatchesCurrentFilter(vm, query))
            {
                _visibleRows.Add(vm);
            }

            if (i == 23 || (i > 23 && (i + 1) % 32 == 0))
            {
                await Task.Delay(1);
            }
        }

        _isBindingComplete = true;
        CountText.Text = _allRows.Count + " 组 / " + totalChildren + " 个";
        ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void SearchDebounceTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        ApplyFilter();
    }

    private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _activeFilter = FilterCombo?.SelectedIndex switch
        {
            1 => "closable",
            2 => "protected",
            3 => "skipped",
            4 => "risk",
            _ => "all"
        };
        ApplyFilter();
    }

    private void LowRiskToggle_Toggled(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        if (_allRows == null)
        {
            UpdateClosePreview();
            return;
        }

        string query = SearchFilterHelper.ExtractQuery(SearchBox?.Text);
        IEnumerable<CandidateRowViewModel> filtered = SearchFilterHelper.FilterByQuery(_allRows, query, BuildSearchText);

        filtered = _activeFilter switch
        {
            "closable" => filtered.Where(r => ProcessPlanner.MatchesFilter(r.Raw, ProcessGroupFilter.Closable)),
            "protected" => filtered.Where(r => ProcessPlanner.MatchesFilter(r.Raw, ProcessGroupFilter.Protected)),
            "skipped" => filtered.Where(r => ProcessPlanner.MatchesFilter(r.Raw, ProcessGroupFilter.Skipped)),
            "risk" => filtered.Where(r => ProcessPlanner.MatchesFilter(r.Raw, ProcessGroupFilter.HighRisk)),
            _ => filtered
        };

        if (LowRiskToggle?.IsOn == true)
        {
            filtered = filtered.Where(r => r.RiskScore < 45 && !r.IsHighRisk && r.Action != ProcessPlanner.ActionReport);
        }

        var result = filtered.ToList();
        if (result.Count == 0 && _allRows.Count > 0)
        {
            NoResultsState.Visibility = Visibility.Visible;
            ProcessList.Visibility = Visibility.Collapsed;
        }
        else
        {
            NoResultsState.Visibility = Visibility.Collapsed;
            ProcessList.Visibility = Visibility.Visible;
        }

        _visibleRows = result;
        BindRows(result);
        UpdateClosePreview();
    }

    private bool MatchesCurrentFilter(CandidateRowViewModel row, string query)
    {
        if (!string.IsNullOrEmpty(query) && !BuildSearchText(row).Contains(query, StringComparison.Ordinal))
        {
            return false;
        }

        bool filterMatch = _activeFilter switch
        {
            "closable" => ProcessPlanner.MatchesFilter(row.Raw, ProcessGroupFilter.Closable),
            "protected" => ProcessPlanner.MatchesFilter(row.Raw, ProcessGroupFilter.Protected),
            "skipped" => ProcessPlanner.MatchesFilter(row.Raw, ProcessGroupFilter.Skipped),
            "risk" => ProcessPlanner.MatchesFilter(row.Raw, ProcessGroupFilter.HighRisk),
            _ => true
        };

        if (!filterMatch)
        {
            return false;
        }

        return LowRiskToggle?.IsOn != true
            || (row.RiskScore < 45 && !row.IsHighRisk && row.Action != ProcessPlanner.ActionReport);
    }

    private async void BindRows(IReadOnlyList<CandidateRowViewModel> rows)
    {
        int version = ++_bindVersion;
        _isBindingComplete = false;
        if (rows == null || rows.Count <= 80)
        {
            ProcessList.ItemsSource = rows;
            _isBindingComplete = true;
            return;
        }

        var visible = new ObservableCollection<CandidateRowViewModel>();
        ProcessList.ItemsSource = visible;

        const int firstChunk = 36;
        const int chunkSize = 32;
        for (int i = 0; i < rows.Count; i++)
        {
            if (version != _bindVersion || !_isLoaded)
            {
                return;
            }

            visible.Add(rows[i]);

            int threshold = i < firstChunk ? firstChunk : chunkSize;
            if ((i + 1) % threshold == 0)
            {
                await Task.Delay(1);
            }
        }

        _isBindingComplete = true;
    }

    private static string BuildSearchText(CandidateRowViewModel row) => row.SearchText;

    private async void AddToProtected_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedRow(sender, out CandidateRowViewModel vm))
        {
            return;
        }

        var cfg = AppConfig.Load(AppState.ConfigPath);
        var list = new List<string>(cfg.protectedNames ?? Array.Empty<string>());
        if (!list.Contains(vm.ProcessNameRaw, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(vm.ProcessNameRaw);
        }

        cfg.protectedNames = list.ToArray();
        AppConfig.Save(AppState.ConfigPath, cfg);
        AppState.Preferences.RecordProtected(vm.ProcessNameRaw);
        await RefreshAfterConfigChangeAsync("已将 " + vm.ProcessNameRaw + " 加入白名单");
    }

    private async void AddToForce_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedRow(sender, out CandidateRowViewModel vm))
        {
            return;
        }

        var cfg = AppConfig.Load(AppState.ConfigPath);
        var list = new List<string>(cfg.forceAllowedNames ?? Array.Empty<string>());
        if (!list.Contains(vm.ProcessNameRaw, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(vm.ProcessNameRaw);
        }

        cfg.forceAllowedNames = list.ToArray();
        AppConfig.Save(AppState.ConfigPath, cfg);
        AppState.Preferences.RecordForceAllowed(vm.ProcessNameRaw);
        await RefreshAfterConfigChangeAsync("已将 " + vm.ProcessNameRaw + " 加入强制清理名单");
    }

    private async void AddToTarget_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedRow(sender, out CandidateRowViewModel vm))
        {
            return;
        }

        var cfg = AppConfig.Load(AppState.ConfigPath);
        var list = new List<string>(cfg.targetNames ?? Array.Empty<string>());
        if (!list.Contains(vm.ProcessNameRaw, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(vm.ProcessNameRaw);
        }

        cfg.targetNames = list.ToArray();
        AppConfig.Save(AppState.ConfigPath, cfg);
        await RefreshAfterConfigChangeAsync("已将 " + vm.ProcessNameRaw + " 添加到目标名单，请再次确认后再关闭");
    }

    private async void RemoveFromTarget_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaggedRow(sender, out CandidateRowViewModel vm))
        {
            return;
        }

        var cfg = AppConfig.Load(AppState.ConfigPath);
        var list = new List<string>(cfg.targetNames ?? Array.Empty<string>());
        list.RemoveAll(n => StringComparer.OrdinalIgnoreCase.Equals(n, vm.ProcessNameRaw));
        cfg.targetNames = list.ToArray();
        AppConfig.Save(AppState.ConfigPath, cfg);
        AppState.Preferences.IncrementManualRemove(vm.ProcessNameRaw);
        await RefreshAfterConfigChangeAsync("已从目标名单移除 " + vm.ProcessNameRaw);
    }

    private async Task RefreshAfterConfigChangeAsync(string message)
    {
        if (_isRefreshing)
        {
            return;
        }

        SetRefreshing(true);
        try
        {
            await Task.Yield();
            await AppState.ScanAsync(AppState.ConfigPath);
            RefreshData();
            await ShowToastAsync(message);
        }
        finally
        {
            SetRefreshing(false);
        }
    }

    private async void RefreshScan_Click(object sender, RoutedEventArgs e)
    {
        if (_isRefreshing)
        {
            return;
        }

        SetRefreshing(true);
        try
        {
            await Task.Yield();
            await AppState.ScanAsync(AppState.ConfigPath);
            RefreshData();
        }
        finally
        {
            SetRefreshing(false);
        }
    }

    private void SetRefreshing(bool isRefreshing)
    {
        _isRefreshing = isRefreshing;

        if (RefreshButton != null)
        {
            RefreshButton.IsEnabled = !isRefreshing;
        }

        if (SearchBox != null)
        {
            SearchBox.IsEnabled = !isRefreshing;
        }

        if (FilterCombo != null)
        {
            FilterCombo.IsEnabled = !isRefreshing;
        }

        if (LowRiskToggle != null)
        {
            LowRiskToggle.IsEnabled = !isRefreshing;
        }

        if (ProcessList != null)
        {
            ProcessList.IsEnabled = !isRefreshing;
        }

        if (isRefreshing)
        {
            CountBadge.Visibility = Visibility.Visible;
            CountText.Text = "刷新中...";
            PreviewAppsText.Text = "正在扫描进程，请稍候...";
        }
    }

    private async void CloseGroup_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetTaggedRow(sender, out CandidateRowViewModel vm))
        {
            await CloseRowsAsync(new List<CandidateRowViewModel> { vm });
        }
    }

    private void ToggleDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not DependencyObject current)
        {
            return;
        }

        while (current != null)
        {
            if (current is Expander expander)
            {
                expander.IsExpanded = !expander.IsExpanded;
                return;
            }

            current = VisualTreeHelper.GetParent(current);
        }
    }

    private async void ProcessList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Item is not CandidateRowViewModel vm)
        {
            return;
        }

        await vm.EnsureIconLoadedAsync();
    }

    private async void ConfirmCloseSelected_Click(object sender, RoutedEventArgs e)
    {
        await CloseRowsAsync(_previewRows ?? new List<CandidateRowViewModel>());
    }

    private void CancelClosePreview_Click(object sender, RoutedEventArgs e)
    {
        _ = ShowToastAsync("已取消本次关闭预览");
    }

    private async void CloseChildProcess_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not int pid)
        {
            return;
        }

        ProcessRecord target = AppState.CurrentPlan?.Candidates?.FirstOrDefault(p => p.Id == pid);
        AppConfig cfg = AppConfig.Load(AppState.ConfigPath);
        if (target == null || !ProcessPlanner.IsExecutableTarget(target, cfg))
        {
            await ShowToastAsync("当前配置已保护或跳过该进程，请重新扫描后再操作");
            return;
        }

        await CloseRecordsAsync(new List<ProcessRecord> { target }, BuildSingleClosePreview(target));
    }

    private async Task CloseRowsAsync(IReadOnlyList<CandidateRowViewModel> rows)
    {
        ClosePlan plan = AppState.CurrentPlan;
        AppConfig cfg = AppConfig.Load(AppState.ConfigPath);
        var targets = rows?
            .SelectMany(r => r.Raw.Children ?? new List<ProcessRecord>())
            .Select(child => plan?.Candidates?.FirstOrDefault(p => p.Id == child.Id))
            .Where(p => p != null)
            .Cast<ProcessRecord>()
            .Where(p => ProcessPlanner.IsExecutableTarget(p, cfg))
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .ToList() ?? new List<ProcessRecord>();

        if (plan == null || targets.Count == 0)
        {
            await ShowToastAsync("当前没有可关闭的低风险进程");
            return;
        }

        await CloseRecordsAsync(targets, BuildGroupClosePreview(rows, targets));
    }

    private async Task CloseRecordsAsync(IReadOnlyList<ProcessRecord> targets, string preview)
    {
        ClosePlan plan = AppState.CurrentPlan;
        if (plan == null || targets == null || targets.Count == 0)
        {
            return;
        }

        AppConfig cfg = AppConfig.Load(AppState.ConfigPath);
        targets = ProcessPlanner.FilterExecutableTargets(targets, cfg);
        if (targets.Count == 0)
        {
            await ShowToastAsync("当前配置已保护或跳过所选进程，请重新扫描后再操作");
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "确认关闭",
            Content = preview,
            PrimaryButtonText = "关闭",
            CloseButtonText = "取消",
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
        try
        {
            await Task.Run(() =>
            {
                var miniPlan = new ClosePlan
                {
                    Config = cfg,
                    Candidates = targets.ToList(),
                    Protected = plan.Protected ?? new List<ProcessRecord>(),
                    Skipped = new List<ProcessRecord>()
                };

                CloseExecutor.Execute(
                    miniPlan,
                    line => _dispatcher.TryEnqueue(() => AppState.AddLog(line)),
                    CancellationToken.None);
            });

            await AppState.ScanAsync(AppState.ConfigPath);
            RefreshData();
            await ShowToastAsync("已处理 " + targets.Count + " 个进程");
        }
        catch (Exception ex)
        {
            await ShowToastAsync("关闭失败：" + ex.Message);
        }
    }

    private void UpdateClosePreview()
    {
        if (PreviewCountText == null || PreviewMemoryText == null || PreviewAppsText == null || ConfirmCloseButton == null)
        {
            return;
        }

        string query = SearchFilterHelper.ExtractQuery(SearchBox?.Text);
        _previewRows = (_visibleRows ?? new List<CandidateRowViewModel>())
            .Where(r => r.CanClose && r.RiskScore < HighRiskScoreThreshold)
            .Where(r => ProcessPlanner.MatchesPrimaryIdentity(r.Raw, query))
            .Take(8)
            .ToList();

        int appCount = _previewRows.Count;
        long memoryMb = _previewRows
            .SelectMany(r => r.Raw.Children ?? new List<ProcessRecord>())
            .Where(r => ProcessPlanner.IsExecutableTarget(r, AppState.CurrentPlan?.Config))
            .Sum(r => r.MemoryMb);

        PreviewCountText.Text = "即将关闭 " + appCount + " 个应用";
        PreviewMemoryText.Text = "预计释放内存 " + FormatMemory(memoryMb);
        PreviewAppsText.Text = appCount == 0
            ? (string.IsNullOrWhiteSpace(query) ? "暂无可关闭应用" : "当前搜索没有主应用可关闭项；辅助进程请在列表中单独确认")
            : string.Join("、", _previewRows.Take(5).Select(r => r.Process)) + (appCount > 5 ? " 等" : "");
        ConfirmCloseButton.Content = "确认关闭 " + appCount + " 个应用";
        ConfirmCloseButton.IsEnabled = appCount > 0;
    }

    private static string BuildGroupClosePreview(IReadOnlyList<CandidateRowViewModel> rows, IReadOnlyList<ProcessRecord> targets)
    {
        long memoryMb = targets.Sum(t => t.MemoryMb);
        var names = rows.Select(r => r.Process).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();
        var lines = new List<string>
        {
            "应用：" + string.Join("、", names),
            "进程数：" + targets.Count,
            "预计释放：" + FormatMemory(memoryMb),
            "说明：高风险提示项会跳过，不会关闭系统进程和白名单应用。"
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildSingleClosePreview(ProcessRecord target)
    {
        var lines = new List<string>
        {
            "进程：" + target.ProcessName + " (" + target.Id + ")",
            "动作：" + target.Action,
            "风险分：" + target.RiskScore,
            "预计释放：" + (target.MemoryMb > 0 ? target.MemoryMb + " MB" : "-")
        };

        if (!string.IsNullOrWhiteSpace(target.MainWindowTitle))
        {
            lines.Add("窗口：" + target.MainWindowTitle);
        }

        if (!string.IsNullOrWhiteSpace(target.Path))
        {
            lines.Add("路径：" + target.Path);
        }

        if (target.Action == ProcessPlanner.ActionForce || target.IsHighRisk)
        {
            lines.Add("请确认：该操作可能触发强制关闭，未保存数据可能丢失。");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static long ParseMemoryMb(string memoryText)
    {
        string first = (memoryText ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return long.TryParse(first, out long value) ? value : 0;
    }

    private static string FormatMemory(long memoryMb)
    {
        if (memoryMb >= 1024)
        {
            return (memoryMb / 1024d).ToString("0.0") + " GB";
        }

        return Math.Max(0, memoryMb) + " MB";
    }

    private static bool TryGetTaggedRow(object sender, out CandidateRowViewModel vm)
    {
        vm = sender switch
        {
            Button button => button.Tag as CandidateRowViewModel,
            MenuFlyoutItem item => item.Tag as CandidateRowViewModel,
            _ => null
        };

        return vm != null;
    }

    private async Task ShowToastAsync(string message)
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
}
