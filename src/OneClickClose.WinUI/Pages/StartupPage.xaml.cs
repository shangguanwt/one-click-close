using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;

namespace OneClickClose.WinUI.Pages;

/// <summary>
/// 启动项 ViewModel，包装 <see cref="StartupItem"/> 并提供界面绑定属性。
/// </summary>
public sealed class StartupItemViewModel
{
    public StartupItem Item { get; }

    public string Name => Item.Name;
    public string Command => Item.Command;
    public string Source => Item.Source;
    public string Location => Item.Location;

    public bool IsEnabled
    {
        get => Item.IsEnabled;
        set => Item.IsEnabled = value;
    }

    /// <summary>来源标签背景色 (低饱和度)。</summary>
    public Brush SourceBadgeBg => Source switch
    {
        "注册表" => Application.Current.Resources["SafeSoftBrush"] as Brush
                    ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 25, 58, 42)),
        "启动文件夹" => Application.Current.Resources["CopperSoftBrush"] as Brush
                       ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 59, 42, 26)),
        "计划任务" => Application.Current.Resources["MutedSoftBrush"] as Brush
                     ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 42, 51, 46)),
        _ => Application.Current.Resources["MutedSoftBrush"] as Brush
    };

    /// <summary>来源标签前景色。</summary>
    public Brush SourceBadgeFg => Source switch
    {
        "注册表" => Application.Current.Resources["SafeBrush"] as Brush
                    ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 69, 180, 125)),
        "启动文件夹" => Application.Current.Resources["CopperBrush"] as Brush
                       ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 201, 130, 59)),
        "计划任务" => Application.Current.Resources["MutedTextBrush"] as Brush
                     ?? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 142, 154, 146)),
        _ => Application.Current.Resources["MutedTextBrush"] as Brush
    };

    public StartupItemViewModel(StartupItem item) => Item = item;
}

public sealed partial class StartupPage : Page
{
    private List<StartupItemViewModel> _allItems = new();
    private List<StartupItemViewModel> _filteredItems = new();
    private string _currentFilter = "全部";

    public StartupPage()
    {
        this.InitializeComponent();
        ShowEmptyState();
    }

    // ── Scan ─────────────────────────────────────────────────────────────

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanBtn.IsEnabled = false;
        StatusInfoBar.IsOpen = false;

        try
        {
            List<StartupItem> items = await Task.Run(() => StartupScanner.ScanAll());

            _allItems = items.Select(i => new StartupItemViewModel(i)).ToList();
            _currentFilter = "全部";
            HighlightFilterButton(FilterAllBtn);

            CountText.Text = _allItems.Count + " 项";
            CountBadge.Visibility = Visibility.Visible;

            ApplyFilter();
            ShowStatusInfo(InfoBarSeverity.Success,
                $"扫描完成，共发现 {_allItems.Count} 个启动项。");
        }
        catch (Exception ex)
        {
            ShowStatusInfo(InfoBarSeverity.Error, $"扫描失败：{ex.Message}");
        }
        finally
        {
            ScanBtn.IsEnabled = true;
        }
    }

    // ── Filter ───────────────────────────────────────────────────────────

    private void FilterAllBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentFilter = "全部";
        HighlightFilterButton(FilterAllBtn);
        ApplyFilter();
    }

    private void FilterRegistryBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentFilter = "注册表";
        HighlightFilterButton(FilterRegistryBtn);
        ApplyFilter();
    }

    private void FilterFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentFilter = "启动文件夹";
        HighlightFilterButton(FilterFolderBtn);
        ApplyFilter();
    }

    private void FilterTaskBtn_Click(object sender, RoutedEventArgs e)
    {
        _currentFilter = "计划任务";
        HighlightFilterButton(FilterTaskBtn);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (_allItems.Count == 0)
        {
            ShowEmptyState();
            return;
        }

        _filteredItems = _currentFilter == "全部"
            ? _allItems.ToList()
            : _allItems.Where(i => i.Source == _currentFilter).ToList();

        if (_filteredItems.Count == 0)
        {
            ListScroll.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Collapsed;
            NoResultsState.Visibility = Visibility.Visible;
            StatusInfoBar.IsOpen = false;
        }
        else
        {
            ListScroll.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            NoResultsState.Visibility = Visibility.Collapsed;
            ItemsList.ItemsSource = _filteredItems;
        }
    }

    private void HighlightFilterButton(Button activeBtn)
    {
        foreach (var btn in new[] { FilterAllBtn, FilterRegistryBtn, FilterFolderBtn, FilterTaskBtn })
        {
            btn.FontWeight = (btn == activeBtn)
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    // ── Toggle enable / disable ─────────────────────────────────────────

    private async void ItemToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle || toggle.Tag is not StartupItemViewModel vm) return;

        // IsOn has already been updated by the binding at this point
        bool enable = toggle.IsOn;

        try
        {
            bool success = await Task.Run(() =>
                enable ? StartupScanner.EnableItem(vm.Item) : StartupScanner.DisableItem(vm.Item));

            if (success)
            {
                vm.IsEnabled = enable;
                ShowStatusInfo(InfoBarSeverity.Success,
                    enable
                        ? $"已启用「{vm.Name}」。"
                        : $"已禁用「{vm.Name}」。");
            }
            else
            {
                // 操作失败，回滚开关状态
                toggle.IsOn = !enable;
                vm.IsEnabled = !enable;
                ShowStatusInfo(InfoBarSeverity.Error,
                    $"操作失败，无法{(enable ? "启用" : "禁用")}「{vm.Name}」。");
            }
        }
        catch (Exception ex)
        {
            toggle.IsOn = !enable;
            vm.IsEnabled = !enable;
            ShowStatusInfo(InfoBarSeverity.Error, $"操作异常：{ex.Message}");
        }
    }

    // ── Locate ───────────────────────────────────────────────────────────

    private void LocateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not StartupItemViewModel vm) return;

        // Show the location string in an InfoBar (placeholder: future could open regedit / folder)
        string location = vm.Location;

        switch (vm.Source)
        {
            case "注册表":
                ShowStatusInfo(InfoBarSeverity.Informational,
                    $"注册表位置：{location}");
                break;
            case "启动文件夹":
                ShowStatusInfo(InfoBarSeverity.Informational,
                    $"文件夹位置：{location}");
                break;
            case "计划任务":
                ShowStatusInfo(InfoBarSeverity.Informational,
                    $"计划任务名称：{location}");
                break;
            default:
                ShowStatusInfo(InfoBarSeverity.Informational,
                    $"位置：{location}");
                break;
        }
    }

    // ── State helpers ────────────────────────────────────────────────────

    private void ShowEmptyState()
    {
        ListScroll.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
        NoResultsState.Visibility = Visibility.Collapsed;
        CountBadge.Visibility = Visibility.Collapsed;
    }

    private void ShowStatusInfo(InfoBarSeverity severity, string message)
    {
        StatusInfoBar.Severity = severity;
        StatusInfoBar.Message = message;
        StatusInfoBar.IsOpen = true;

        // Auto-dismiss after 6 seconds
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(6));
            if (StatusInfoBar.Message == message)
                StatusInfoBar.IsOpen = false;
        });
    }
}
