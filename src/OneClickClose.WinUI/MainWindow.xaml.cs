using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using OneClickClose.WinUI.Pages;
using OneClickClose.WinUI.Services;
using Windows.UI.Core;

namespace OneClickClose.WinUI;

public sealed partial class MainWindow : Window
{
    private const double ThemeToggleMinLeft = 280;
    private const double ThemeToggleFallbackCaptionInset = 138;
    private const double ThemeToggleCaptionGap = 24;
    private const double ThemeToggleTop = 4;
    private const double ThemeToggleSize = 42;
    private const int NavTransitionMs = 150;
    private const int NavPressTransitionMs = 90;
    private const double NavPressedScale = 0.985;

    private readonly Dictionary<string, Button> _navButtons;
    private readonly Dictionary<Button, Border> _navBackgrounds;
    private readonly HashSet<Button> _hoveredNavButtons = new();
    private bool _isLightTheme;
    private string _currentTag = "overview";
    private int _navigationVersion;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public MainWindow()
    {
        InitializeComponent();
        ContentFrame.CacheSize = 5;

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WindowChromeService.Apply(hwnd, AppThemePalette.ForTheme(lightTheme: false));

        WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow appWindow = AppWindow.GetFromWindowId(windowId);
        ResizeForCurrentDpi(appWindow, hwnd, windowId);
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        try { appWindow.SetIcon(File.Exists(iconPath) ? iconPath : "Assets/AppIcon.ico"); } catch { }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        ApplyTheme(lightTheme: false, AppWindow);

        RootGrid.SizeChanged += (_, _) => PositionThemeToggleButton();
        RootGrid.Loaded += async (_, _) =>
        {
            await Task.Delay(250);
            PositionThemeToggleButton();
        };
        ThemeToggleButton.Loaded += (_, _) => PositionThemeToggleButton();

        _navButtons = new Dictionary<string, Button>
        {
            ["overview"] = OverviewNavButton,
            ["candidates"] = CandidatesNavButton,
            ["protected"] = ProtectedNavButton,
            ["logs"] = LogsNavButton,
            ["settings"] = SettingsNavButton
        };

        _navBackgrounds = new Dictionary<Button, Border>
        {
            [OverviewNavButton] = OverviewNavBackground,
            [CandidatesNavButton] = CandidatesNavBackground,
            [ProtectedNavButton] = ProtectedNavBackground,
            [LogsNavButton] = LogsNavBackground,
            [SettingsNavButton] = SettingsNavBackground
        };

        AppState.NavigationRequested += OnNavigationRequested;
        Navigate("overview");
        RootGrid.KeyDown += MainWindow_KeyDown;
    }

    private void OnNavigationRequested(object sender, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            Navigate(tag);
            return;
        }

        DispatcherQueue.TryEnqueue(() => Navigate(tag));
    }

    private static void ResizeForCurrentDpi(AppWindow appWindow, IntPtr hwnd, WindowId windowId)
    {
        const int desiredWidthEp = 1440;
        const int desiredHeightEp = 920;
        double scale = 1.0;

        try
        {
            uint dpi = GetDpiForWindow(hwnd);
            if (dpi > 0)
            {
                scale = dpi / 96.0;
            }
        }
        catch { }

        int width = (int)Math.Round(desiredWidthEp * scale);
        int height = (int)Math.Round(desiredHeightEp * scale);
        try
        {
            DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest);
            Windows.Graphics.RectInt32 workArea = displayArea.WorkArea;
            width = Math.Min(width, Math.Max(1000, workArea.Width - 96));
            height = Math.Min(height, Math.Max(720, workArea.Height - 96));
        }
        catch { }

        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyTheme(!_isLightTheme, AppWindow);
        SelectNavItem(_currentTag);
        PositionThemeToggleButton();
    }

    private void ApplyTheme(bool lightTheme, AppWindow appWindow)
    {
        _isLightTheme = lightTheme;
        RootGrid.RequestedTheme = lightTheme ? ElementTheme.Light : ElementTheme.Dark;
        ContentFrame.RequestedTheme = RootGrid.RequestedTheme;
        ThemeToggleIcon.Glyph = lightTheme ? "\uE708" : "\uE706";
        ToolTipService.SetToolTip(ThemeToggleButton, lightTheme ? "切换到暗色主题" : "切换到亮色主题");

        AppThemePalette palette = AppThemeService.Apply(lightTheme, appWindow);
        AppAccentColorService.ApplyCurrent(persist: false);
        WindowChromeService.Apply(WinRT.Interop.WindowNative.GetWindowHandle(this), palette);
    }

    private void PositionThemeToggleButton()
    {
        double visibleWidth = RootGrid.ActualWidth;
        double captionInset = ThemeToggleFallbackCaptionInset;

        try
        {
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            double scale = Math.Max(1, GetDpiForWindow(hwnd) / 96.0);
            if (GetWindowRect(hwnd, out WindowRect rect) && rect.Right > rect.Left)
            {
                visibleWidth = (rect.Right - rect.Left) / scale;
            }

            if (AppWindow.TitleBar.RightInset > 0)
            {
                captionInset = AppWindow.TitleBar.RightInset / scale;
            }
        }
        catch
        {
        }

        if (visibleWidth <= 0)
        {
            return;
        }

        double left = Math.Max(
            ThemeToggleMinLeft,
            visibleWidth - captionInset - ThemeToggleCaptionGap - ThemeToggleSize);
        ThemeToggleButton.Margin = new Thickness(left, ThemeToggleTop, 0, 0);
    }

    private void SidebarNav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            Navigate(tag);
        }
    }

    private void Navigate(string tag)
    {
        tag = NormalizeNavTag(tag);
        int version = ++_navigationVersion;

        _currentTag = tag;
        SelectNavItem(tag, animate: false);
        UpdatePageHeader(tag);

        DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => NavigateFrame(tag, version));
    }

    private void NavigateFrame(string tag, int version)
    {
        if (version != _navigationVersion)
        {
            return;
        }

        Type pageType = GetPageType(tag);
        if (ContentFrame.Content?.GetType() == pageType)
        {
            return;
        }

        ContentFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());
    }

    private static string NormalizeNavTag(string tag)
    {
        return tag is "overview" or "candidates" or "protected" or "logs" or "settings"
            ? tag
            : "overview";
    }

    private static Type GetPageType(string tag)
    {
        return tag switch
        {
            "candidates" => typeof(CandidateProcessesPage),
            "protected" => typeof(ProtectedProcessesPage),
            "logs" => typeof(LogsPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(OverviewPage)
        };
    }

    private void SelectNavItem(string tag, bool animate = false)
    {
        foreach (KeyValuePair<string, Button> pair in _navButtons)
        {
            ApplyNavState(pair.Value, pair.Key == tag, animate);
        }
    }

    private void ApplyNavState(Button button, bool selected, bool animate = false)
    {
        Brush transparent = GetThemeBrush("SidebarNavBackgroundBrush", new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)));
        Brush selectedForeground = GetThemeBrush("SidebarNavSelectedForegroundBrush", new SolidColorBrush(Colors.White));
        Brush normalForeground = GetThemeBrush("SidebarNavForegroundBrush", (Brush)Application.Current.Resources["TitleTextBrush"]);

        button.Background = transparent;
        button.Resources["ButtonBackgroundPointerOver"] = transparent;
        button.Resources["ButtonBackgroundPressed"] = transparent;
        button.Resources["ButtonForegroundPointerOver"] = selected
            ? selectedForeground
            : GetThemeBrush("SidebarNavHoverForegroundBrush", normalForeground);
        button.Resources["ButtonForegroundPressed"] = selected
            ? selectedForeground
            : GetThemeBrush("SidebarNavPressedForegroundBrush", normalForeground);
        button.Resources["ButtonBorderBrushPointerOver"] = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        button.Resources["ButtonBorderBrushPressed"] = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        if (_navBackgrounds.TryGetValue(button, out Border background))
        {
            if (selected)
            {
                SetNavBackground(background, (Brush)Application.Current.Resources["SidebarSelectedBrush"], 1, animate ? NavTransitionMs : 0);
            }
            else
            {
                SetNavBackground(background, transparent, 0, animate ? NavTransitionMs : 0);
            }
        }

        Brush foreground = selected
            ? selectedForeground
            : normalForeground;
        button.Foreground = foreground;
        SetNavChildrenForeground(button, foreground);
        SetNavScale(button, 1, animate ? NavTransitionMs : 0);
    }

    private void RegisterNavButtonStateHandlers(Button button)
    {
        button.PointerEntered += SidebarNav_PointerEntered;
        button.PointerExited += SidebarNav_PointerExited;
        button.PointerPressed += SidebarNav_PointerPressed;
        button.PointerReleased += SidebarNav_PointerReleased;
        button.PointerCanceled += SidebarNav_PointerReleased;
        button.PointerCaptureLost += SidebarNav_PointerReleased;
    }

    private void SidebarNav_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button && !IsNavButtonSelected(button))
        {
            _hoveredNavButtons.Add(button);
            ApplyNavInteractiveState(
                button,
                GetThemeBrush("SidebarNavHoverBrush", (Brush)Application.Current.Resources["HoverBrush"]),
                GetThemeBrush("SidebarNavHoverForegroundBrush", (Brush)Application.Current.Resources["TitleTextBrush"]),
                1,
                NavTransitionMs);
        }
    }

    private void SidebarNav_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            _hoveredNavButtons.Remove(button);
            ApplyNavState(button, IsNavButtonSelected(button));
        }
    }

    private void SidebarNav_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button && !IsNavButtonSelected(button))
        {
            ApplyNavInteractiveState(
                button,
                GetThemeBrush("SidebarNavPressedBrush", (Brush)Application.Current.Resources["ButtonPressedBrush"]),
                GetThemeBrush("SidebarNavPressedForegroundBrush", (Brush)Application.Current.Resources["TitleTextBrush"]),
                NavPressedScale,
                NavPressTransitionMs);
        }
    }

    private void SidebarNav_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Button button)
        {
            bool selected = IsNavButtonSelected(button);
            if (!selected && _hoveredNavButtons.Contains(button))
            {
                ApplyNavInteractiveState(
                    button,
                    GetThemeBrush("SidebarNavHoverBrush", (Brush)Application.Current.Resources["HoverBrush"]),
                    GetThemeBrush("SidebarNavHoverForegroundBrush", (Brush)Application.Current.Resources["TitleTextBrush"]),
                    1,
                    NavTransitionMs);
                return;
            }

            ApplyNavState(button, selected);
        }
    }

    private bool IsNavButtonSelected(Button button)
    {
        return button.Tag is string tag && string.Equals(tag, _currentTag, StringComparison.Ordinal);
    }

    private void ApplyNavInteractiveState(Button button, Brush background, Brush foreground, double scale, int durationMs)
    {
        if (_navBackgrounds.TryGetValue(button, out Border layer))
        {
            AnimateNavBackground(layer, background, 1, durationMs);
        }

        button.Background = GetThemeBrush("SidebarNavBackgroundBrush", new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)));
        button.Foreground = foreground;
        SetNavChildrenForeground(button, foreground);
        AnimateNavScale(button, scale, durationMs);
    }

    private static void AnimateNavBackground(Border layer, Brush targetBrush, double targetOpacity, int durationMs)
    {
        if (targetOpacity > 0 && targetBrush is SolidColorBrush targetSolid)
        {
            Windows.UI.Color startColor = layer.Background is SolidColorBrush currentSolid
                ? currentSolid.Color
                : targetSolid.Color;
            var animatedBrush = new SolidColorBrush(startColor);
            layer.Background = animatedBrush;
            AnimateBrushColor(animatedBrush, targetSolid.Color, durationMs);
        }
        else if (targetOpacity > 0)
        {
            layer.Background = targetBrush;
        }

        AnimateDouble(layer, "Opacity", targetOpacity, durationMs);
    }

    private static void SetNavBackground(Border layer, Brush targetBrush, double targetOpacity, int durationMs)
    {
        if (durationMs <= 0)
        {
            if (targetOpacity > 0)
            {
                layer.Background = targetBrush;
            }

            layer.Opacity = targetOpacity;
            return;
        }

        AnimateNavBackground(layer, targetBrush, targetOpacity, durationMs);
    }

    private static void AnimateNavScale(Button button, double targetScale, int durationMs)
    {
        ScaleTransform scale = EnsureNavScaleTransform(button);
        AnimateDouble(scale, "ScaleX", targetScale, durationMs);
        AnimateDouble(scale, "ScaleY", targetScale, durationMs);
    }

    private static void SetNavScale(Button button, double targetScale, int durationMs)
    {
        ScaleTransform scale = EnsureNavScaleTransform(button);
        if (durationMs <= 0)
        {
            scale.ScaleX = targetScale;
            scale.ScaleY = targetScale;
            return;
        }

        AnimateDouble(scale, "ScaleX", targetScale, durationMs);
        AnimateDouble(scale, "ScaleY", targetScale, durationMs);
    }

    private static ScaleTransform EnsureNavScaleTransform(Button button)
    {
        if (button.RenderTransform is ScaleTransform scale)
        {
            return scale;
        }

        scale = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        button.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        button.RenderTransform = scale;
        return scale;
    }

    private static void AnimateDouble(DependencyObject target, string property, double value, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            To = value,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private static void AnimateBrushColor(SolidColorBrush brush, Windows.UI.Color color, int durationMs)
    {
        var animation = new ColorAnimation
        {
            To = color,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, brush);
        Storyboard.SetTargetProperty(animation, "Color");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private static Brush GetThemeBrush(string key, Brush fallback)
    {
        return Application.Current.Resources.TryGetValue(key, out object resource) && resource is Brush brush
            ? brush
            : fallback;
    }

    private static void SetNavChildrenForeground(DependencyObject root, Brush foreground)
    {
        if (root is FontIcon icon)
        {
            icon.Foreground = foreground;
        }

        if (root is TextBlock text)
        {
            text.Foreground = foreground;
        }

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
            "candidates" => "后台进程",
            "protected" => "白名单",
            "logs" => "清理记录",
            "settings" => "设置",
            _ => "总览"
        };

        PageSubtitleText.Text = tag switch
        {
            "candidates" => "查看当前可优化的软件与风险建议",
            "protected" => "管理不会被清理的关键应用",
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
