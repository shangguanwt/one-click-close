using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using OneClickClose.WinUI.Pages;
using Windows.UI.Core;

namespace OneClickClose.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<string, Button> _navButtons;
    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    private static void ResizeForCurrentDpi(AppWindow appWindow, IntPtr hwnd, WindowId windowId)
    {
        const int desiredWidthEp = 1440;
        const int desiredHeightEp = 920;
        double scale = 1.0;
        try
        {
            uint dpi = GetDpiForWindow(hwnd);
            if (dpi > 0) scale = dpi / 96.0;
        }
        catch { }

        int width = (int)Math.Round(desiredWidthEp * scale);
        int height = (int)Math.Round(desiredHeightEp * scale);
        try
        {
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            var workArea = displayArea.WorkArea;
            width = Math.Min(width, Math.Max(1000, workArea.Width - 96));
            height = Math.Min(height, Math.Max(720, workArea.Height - 96));
        }
        catch { }

        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    public MainWindow()
    {
        this.InitializeComponent();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        ResizeForCurrentDpi(appWindow, hwnd, windowId);
        try { appWindow.SetIcon("Assets/AppIcon.ico"); } catch { }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        _navButtons = new Dictionary<string, Button>
        {
            ["overview"] = OverviewNavButton,
            ["quickclose"] = QuickCloseNavButton,
            ["candidates"] = CandidatesNavButton,
            ["protected"] = ProtectedNavButton,
            ["performance"] = PerformanceNavButton,
            ["logs"] = LogsNavButton,
            ["settings"] = SettingsNavButton
        };

        Navigate("overview");
        RootGrid.KeyDown += MainWindow_KeyDown;
    }

    private void SidebarNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            Navigate(tag == "overview" && button == QuickCloseNavButton ? "quickclose" : tag);
        }
    }

    private void Navigate(string tag)
    {
        Type pageType = tag switch
        {
            "overview" => typeof(OverviewPage),
            "quickclose" => typeof(OverviewPage),
            "candidates" => typeof(CandidateProcessesPage),
            "protected" => typeof(ProtectedProcessesPage),
            "performance" => typeof(OverviewPage),
            "logs" => typeof(LogsPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(OverviewPage)
        };

        ContentFrame.Navigate(pageType);
        SelectNavItem(tag);
        UpdatePageHeader(tag);
    }

    private void SelectNavItem(string tag)
    {
        foreach (var pair in _navButtons)
        {
            bool selected = pair.Key == tag || (tag == "performance" && pair.Key == "performance");
            ApplyNavState(pair.Value, selected);
        }
    }

    private void ApplyNavState(Button button, bool selected)
    {
        button.Background = selected
            ? (Brush)Application.Current.Resources["SidebarSelectedBrush"]
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
        SetNavChildrenForeground(button, new SolidColorBrush(Microsoft.UI.Colors.White));
    }

    private static void SetNavChildrenForeground(DependencyObject root, Brush foreground)
    {
        if (root is FontIcon icon) icon.Foreground = foreground;
        if (root is TextBlock text) text.Foreground = foreground;

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            SetNavChildrenForeground(VisualTreeHelper.GetChild(root, i), foreground);
        }
    }

    private void UpdatePageHeader(string tag)
    {
        PageTitleText.Text = tag switch
        {
            "quickclose" => "一键关闭",
            "candidates" => "后台进程",
            "protected" => "白名单",
            "performance" => "性能监控",
            "logs" => "清理记录",
            "settings" => "设置",
            _ => "总览"
        };

        PageSubtitleText.Text = tag switch
        {
            "quickclose" => "安全释放后台占用，保留重要同步与系统服务",
            "candidates" => "查看当前可优化的软件与风险建议",
            "protected" => "管理不会被清理的关键应用",
            "performance" => "实时观察 CPU、内存、磁盘与网络状态",
            "logs" => "回看最近一次清理过程和结果",
            "settings" => "调整扫描、关闭策略和安全名单",
            _ => "快速优化，让电脑保持最佳状态"
        };
    }

    private void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (ContentFrame.Content is OverviewPage overview)
            {
                overview.TryCancel();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Windows.System.VirtualKey.F5)
        {
            _ = NavigateToOverviewAndScan();
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter &&
            InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                .HasFlag(CoreVirtualKeyStates.Down))
        {
            if (ContentFrame.Content is OverviewPage overview)
            {
                overview.TriggerCloseAll();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Windows.System.VirtualKey.F &&
            InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                .HasFlag(CoreVirtualKeyStates.Down))
        {
            if (ContentFrame.Content is CandidateProcessesPage candidates)
            {
                candidates.FocusSearch();
                e.Handled = true;
            }
            else if (ContentFrame.Content is ProtectedProcessesPage protect)
            {
                protect.FocusSearch();
                e.Handled = true;
            }
            else
            {
                Navigate("candidates");
                e.Handled = true;
            }
        }
    }

    private async Task NavigateToOverviewAndScan()
    {
        if (ContentFrame.Content is not OverviewPage overview)
        {
            Navigate("overview");
            await Task.Delay(100);
            overview = ContentFrame.Content as OverviewPage;
        }
        overview?.TriggerScan();
    }
}
