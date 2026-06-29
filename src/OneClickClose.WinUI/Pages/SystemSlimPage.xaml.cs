using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;

namespace OneClickClose.WinUI.Pages;

/// <summary>
/// 遥测服务列表项的视图模型，用于 XAML 绑定。
/// </summary>
public sealed class TelemetryServiceViewModel
{
    /// <summary>底层 Core 数据模型。</summary>
    public TelemetryServiceItem Item { get; }

    public TelemetryServiceViewModel(TelemetryServiceItem item)
    {
        Item = item;
    }

    public string ServiceName => Item.ServiceName;
    public string DisplayName => Item.DisplayName;
    public string Description => Item.Description;
    public RiskLevel RiskLevel => Item.RiskLevel;
    public ServiceAction RecommendedAction => Item.RecommendedAction;
    public bool IsRunning => Item.IsRunning;
    public int StartType => Item.StartType;

    /// <summary>ToggleSwitch 绑定值：服务未禁用（StartType != 4）为 true。</summary>
    public bool SwitchIsOn
    {
        get => Item.StartType != 4;
        set { /* 通过 Toggled 事件处理 */ }
    }

    // ── Risk badge brushes ──
    public string RiskBadgeText => Item.RiskLevel switch
    {
        RiskLevel.Low => "低风险",
        RiskLevel.Medium => "中风险",
        RiskLevel.High => "高风险",
        _ => ""
    };

    public SolidColorBrush RiskBadgeBackground => Item.RiskLevel switch
    {
        RiskLevel.Low => (SolidColorBrush)Application.Current.Resources["SafeSoftBrush"],
        RiskLevel.Medium => (SolidColorBrush)Application.Current.Resources["CopperSoftBrush"],
        RiskLevel.High => (SolidColorBrush)Application.Current.Resources["DangerSoftBrush"],
        _ => (SolidColorBrush)Application.Current.Resources["SurfaceBrush"],
    };

    public SolidColorBrush RiskBadgeForeground => Item.RiskLevel switch
    {
        RiskLevel.Low => (SolidColorBrush)Application.Current.Resources["SafeBrush"],
        RiskLevel.Medium => (SolidColorBrush)Application.Current.Resources["CopperBrush"],
        RiskLevel.High => (SolidColorBrush)Application.Current.Resources["DangerBrush"],
        _ => (SolidColorBrush)Application.Current.Resources["BodyTextBrush"],
    };

    // ── Status badge brushes ──
    public string StatusBadgeText => Item.IsRunning ? "运行中" : "已停止";

    public SolidColorBrush StatusBadgeBackground => Item.IsRunning
        ? (SolidColorBrush)Application.Current.Resources["CopperSoftBrush"]
        : (SolidColorBrush)Application.Current.Resources["MutedSoftBrush"];

    public SolidColorBrush StatusBadgeForeground => Item.IsRunning
        ? (SolidColorBrush)Application.Current.Resources["CopperBrush"]
        : (SolidColorBrush)Application.Current.Resources["SubtleTextBrush"];
}

/// <summary>
/// 系统瘦身页面，用于管理 Windows 遥测和 AI 后台服务。
/// </summary>
public sealed partial class SystemSlimPage : Page
{
    private readonly DispatcherQueue _dispatcher;
    private List<TelemetryServiceViewModel> _items = new();
    private bool _isBusy;

    public SystemSlimPage()
    {
        this.InitializeComponent();
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        this.Loaded += OnLoaded;
    }

    // ── Lifecycle ──────────────────────────────────────────────

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    // ── Scan & refresh ─────────────────────────────────────────

    private async Task RefreshAsync()
    {
        if (_isBusy) return;
        _isBusy = true;

        LoadingPanel.Visibility = Visibility.Visible;
        DisableSafeButton.IsEnabled = false;
        RestoreAllButton.IsEnabled = false;

        try
        {
            List<TelemetryServiceItem> result = null;
            await Task.Run(() =>
            {
                result = TelemetryServiceScanner.Scan();
            });

            _items = result.Select(i => new TelemetryServiceViewModel(i)).ToList();
            UpdateSummary();
            UpdateList();
        }
        catch (Exception ex)
        {
            EmptyText.Text = "扫描失败：" + ex.Message;
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
            DisableSafeButton.IsEnabled = true;
            RestoreAllButton.IsEnabled = true;
            _isBusy = false;
        }
    }

    private void UpdateSummary()
    {
        // 可优化：CanDisable 且正在运行或未禁用的服务
        int optimizable = _items.Count(v =>
            v.RecommendedAction == ServiceAction.CanDisable && v.SwitchIsOn);

        // 已禁用：StartType == 4
        int disabled = _items.Count(v => v.StartType == 4);

        // 运行中：IsRunning == true
        int running = _items.Count(v => v.IsRunning);

        OptimizableCountText.Text = optimizable.ToString();
        DisabledCountText.Text = disabled.ToString();
        RunningCountText.Text = running.ToString();
    }

    private void UpdateList()
    {
        if (_items.Count == 0)
        {
            ServiceList.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            EmptyText.Text = "未检测到可管理的遥测服务";
            return;
        }

        ServiceList.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        ServiceList.ItemsSource = _items;
    }

    // ── Toggle switch ───────────────────────────────────────────

    private async void ServiceToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle || toggle.Tag is not TelemetryServiceViewModel vm)
            return;

        bool turningOn = toggle.IsOn;
        string serviceName = vm.ServiceName;
        bool success;

        try
        {
            success = await Task.Run(() =>
                turningOn
                    ? TelemetryServiceScanner.EnableService(serviceName)
                    : TelemetryServiceScanner.DisableService(serviceName));
        }
        catch
        {
            success = false;
        }

        if (!success)
        {
            // 恢复原状态
            toggle.IsOn = !turningOn;
        }

        // 重新扫描以刷新所有状态
        await RefreshAsync();
    }

    // ── 一键禁用安全项 ──────────────────────────────────────────

    private async void DisableSafeButton_Click(object sender, RoutedEventArgs e)
    {
        var targets = _items.Where(v =>
            v.RiskLevel == RiskLevel.Low &&
            v.RecommendedAction == ServiceAction.CanDisable &&
            v.IsRunning).ToList();

        if (targets.Count == 0)
        {
            await ShowToastAsync("没有需要禁用的安全项");
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "确认禁用",
            Content = string.Format("即将禁用 {0} 个低风险服务，是否继续？", targets.Count),
            PrimaryButtonText = "禁用",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };
        confirm.XamlRoot = this.XamlRoot;
        var result = await confirm.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        DisableSafeButton.IsEnabled = false;
        int successCount = 0;

        foreach (var vm in targets)
        {
            try
            {
                bool ok = await Task.Run(() => TelemetryServiceScanner.DisableService(vm.ServiceName));
                if (ok) successCount++;
            }
            catch
            {
                // 单项失败不影响其余
            }
        }

        await RefreshAsync();
        await ShowToastAsync(string.Format("已禁用 {0} 个服务", successCount));
    }

    // ── 恢复全部 ────────────────────────────────────────────────

    private async void RestoreAllButton_Click(object sender, RoutedEventArgs e)
    {
        var disabled = _items.Where(v => v.StartType == 4).ToList();

        if (disabled.Count == 0)
        {
            await ShowToastAsync("没有需要恢复的服务");
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "确认恢复",
            Content = string.Format("即将恢复 {0} 个已禁用的服务，是否继续？", disabled.Count),
            PrimaryButtonText = "恢复",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };
        confirm.XamlRoot = this.XamlRoot;
        var result = await confirm.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        RestoreAllButton.IsEnabled = false;
        int successCount = 0;

        foreach (var vm in disabled)
        {
            try
            {
                bool ok = await Task.Run(() => TelemetryServiceScanner.EnableService(vm.ServiceName));
                if (ok) successCount++;
            }
            catch
            {
                // 单项失败不影响其余
            }
        }

        await RefreshAsync();
        await ShowToastAsync(string.Format("已恢复 {0} 个服务", successCount));
    }

    // ── Helpers ─────────────────────────────────────────────────

    private async Task ShowToastAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Content = message,
            CloseButtonText = "确定",
            DefaultButton = ContentDialogButton.Close
        };
        dialog.XamlRoot = this.XamlRoot;
        await dialog.ShowAsync();
    }
}
