using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using OneClickClose.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.Services;

namespace OneClickClose.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    private const double WideLayoutThreshold = 1080;
    private const double SingleColumnThreshold = 820;
    private const string SupportSiteUrl = "https://oneclick.03142023.xyz/";

    private AppConfig _config;

    public SettingsPage()
    {
        this.InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
        this.SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadConfig();
        AppAccentColorService.ApplyCurrent(persist: false);
        UpdateAccentSelection();
        UpdateAdaptiveLayout(ActualWidth);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        this.SizeChanged -= OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAdaptiveLayout(e.NewSize.Width);
    }

    private void UpdateAdaptiveLayout(double width)
    {
        if (width <= 0 || double.IsNaN(width))
        {
            return;
        }

        double layoutWidth = SettingsScrollViewer.ViewportWidth;
        if (layoutWidth <= 0 || double.IsNaN(layoutWidth) || double.IsInfinity(layoutWidth))
        {
            layoutWidth = width;
        }

        SettingsRootGrid.Width = Math.Max(0, layoutWidth);

        bool wideLayout = layoutWidth >= WideLayoutThreshold;
        bool singleColumn = layoutWidth < SingleColumnThreshold;

        SettingsSideColumn.Width = wideLayout ? new GridLength(300) : new GridLength(0);
        SettingsRootGrid.ColumnSpacing = wideLayout ? 18 : 0;
        Grid.SetColumn(SettingsSidePanel, wideLayout ? 1 : 0);
        Grid.SetRow(SettingsSidePanel, wideLayout ? 0 : 1);

        SettingsCardsSecondColumn.Width = singleColumn
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        SettingsCardsGrid.ColumnSpacing = singleColumn ? 0 : 14;

        Grid.SetColumn(ScanCard, singleColumn ? 0 : 1);
        Grid.SetRow(ScanCard, singleColumn ? 1 : 0);
        Grid.SetColumn(SafetyCard, 0);
        Grid.SetRow(SafetyCard, singleColumn ? 2 : 1);
        Grid.SetColumn(LearningCard, singleColumn ? 0 : 1);
        Grid.SetRow(LearningCard, singleColumn ? 3 : 1);

        ProcessListsSecondColumn.Width = singleColumn
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        ProcessListsThirdColumn.Width = singleColumn
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        ProcessListsGrid.ColumnSpacing = singleColumn ? 0 : 12;

        Grid.SetColumn(ProtectedNamesPanel, singleColumn ? 0 : 1);
        Grid.SetRow(ProtectedNamesPanel, singleColumn ? 1 : 0);
        Grid.SetColumn(ForceNamesPanel, singleColumn ? 0 : 2);
        Grid.SetRow(ForceNamesPanel, singleColumn ? 2 : 0);
    }

    private void LoadConfig()
    {
        try
        {
            _config = AppConfig.Load(AppState.ConfigPath);
            PopulateUI();
            PopulateSystemInfo();
        }
        catch (Exception ex)
        {
            ShowStatus("加载配置失败：" + ex.Message, isError: true);
        }
    }

    private void PopulateUI()
    {
        WaitSecondsBox.Value = _config.waitSeconds;
        GracefulTimeoutBox.Value = _config.gracefulTimeoutSeconds;
        QueryTimeoutBox.Value = _config.queryTimeoutSeconds;
        AutoDetectUserAppsToggle.IsOn = _config.AutoDetectUserApps;
        CloseShutdownBlockingAppsToggle.IsOn = _config.CloseShutdownBlockingApps;
        ForceAfterGracefulFailureToggle.IsOn = _config.ForceAfterGracefulFailure;
        CandidateMemoryThresholdBox.Value = _config.candidateMemoryThresholdMb;
        TargetNamesBox.Text = string.Join("\r\n", _config.targetNames ?? Array.Empty<string>());
        ProtectedNamesBox.Text = string.Join("\r\n", _config.protectedNames ?? Array.Empty<string>());
        ForceNamesBox.Text = string.Join("\r\n", _config.forceAllowedNames ?? Array.Empty<string>());
        ConfigPathBox.Text = AppState.ConfigPath;
    }

    private void AccentColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string accentId)
        {
            return;
        }

        AccentColorOption option = AppAccentColorService.Apply(accentId);
        UpdateAccentSelection();
        ShowStatus($"强调色已切换为{option.Name}。");
    }

    private void UpdateAccentSelection()
    {
        string selected = AppAccentColorService.CurrentAccentId;
        UpdateAccentButton(AccentBlueButton, AccentBlueCheck, "blue", selected);
        UpdateAccentButton(AccentPurpleButton, AccentPurpleCheck, "purple", selected);
        UpdateAccentButton(AccentCyanButton, AccentCyanCheck, "cyan", selected);
        UpdateAccentButton(AccentGreenButton, AccentGreenCheck, "green", selected);
        UpdateAccentButton(AccentOrangeButton, AccentOrangeCheck, "orange", selected);
        UpdateAccentButton(AccentRedButton, AccentRedCheck, "red", selected);
    }

    private static void UpdateAccentButton(Button button, FontIcon checkIcon, string accentId, string selectedAccentId)
    {
        bool selected = string.Equals(accentId, selectedAccentId, StringComparison.OrdinalIgnoreCase);
        Brush accentLight = GetResourceBrush("AccentLightBrush");
        Brush selectedBackground = GetResourceBrush("CyanSoftBrush");
        Brush transparent = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        button.BorderThickness = selected ? new Thickness(2) : new Thickness(0);
        button.BorderBrush = selected ? accentLight : transparent;
        button.Background = selected ? selectedBackground : transparent;
        button.Resources["ButtonBackgroundPointerOver"] = selectedBackground;
        button.Resources["ButtonBackgroundPressed"] = GetResourceBrush("ButtonPressedBrush");
        button.Resources["ButtonBorderBrushPointerOver"] = accentLight;
        button.Resources["ButtonBorderBrushPressed"] = accentLight;
        checkIcon.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Brush GetResourceBrush(string key)
    {
        return Application.Current.Resources.TryGetValue(key, out object resource) && resource is Brush brush
            ? brush
            : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _config.waitSeconds = (int)WaitSecondsBox.Value;
            _config.gracefulTimeoutSeconds = (int)GracefulTimeoutBox.Value;
            _config.queryTimeoutSeconds = (int)QueryTimeoutBox.Value;
            _config.autoDetectUserApps = AutoDetectUserAppsToggle.IsOn;
            _config.closeShutdownBlockingApps = CloseShutdownBlockingAppsToggle.IsOn;
            _config.forceAfterGracefulFailure = ForceAfterGracefulFailureToggle.IsOn;
            _config.candidateMemoryThresholdMb = (int)CandidateMemoryThresholdBox.Value;
            _config.targetNames = ParseLines(TargetNamesBox.Text);
            _config.protectedNames = ParseLines(ProtectedNamesBox.Text);
            _config.forceAllowedNames = ParseLines(ForceNamesBox.Text);

            AppConfig.Save(AppState.ConfigPath, _config);
            ShowStatus("配置已保存。");
        }
        catch (Exception ex)
        {
            ShowStatus("保存失败：" + ex.Message, isError: true);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _config = AppConfig.CreateDefault();
        PopulateUI();
        ShowStatus("已恢复默认配置（尚未保存，点击「保存配置」写入磁盘）。");
    }

    private void OpenConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(AppState.ConfigPath))
            {
                AppConfig.Save(AppState.ConfigPath, _config ?? AppConfig.CreateDefault());
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = AppState.ConfigPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowStatus("无法打开配置文件：" + ex.Message, isError: true);
        }
    }

    private async void ExportConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker();
            IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedFileName = "close-user-apps.config";
            picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var config = AppConfig.Load(AppState.ConfigPath);
                AppConfig.Save(file.Path, config);
                ShowStatus("配置已导出");
            }
        }
        catch (Exception ex)
        {
            ShowStatus("导出失败：" + ex.Message, isError: true);
        }
    }

    private async void ImportConfigBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var imported = AppConfig.Load(file.Path);
                AppConfig.Save(AppState.ConfigPath, imported);
                _config = imported;
                PopulateUI();
                ShowStatus("配置已导入，请重新扫描");
            }
        }
        catch (Exception ex)
        {
            ShowStatus("导入失败：" + ex.Message, isError: true);
        }
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusPanel.Visibility = Visibility.Visible;
        StatusText.Text = message;
        StatusText.Foreground = isError ? ColorHelper.Danger : ColorHelper.Safe;
        StatusPanel.Background = isError ? ColorHelper.DangerSoft : ColorHelper.SafeGreenSoft;
    }

    private async void ResetLearning_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "重置本地学习数据",
            Content = "将清除本地习惯计数和已忽略建议，但不会删除清理记录、白名单或配置文件。",
            PrimaryButtonText = "重置",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        AppState.LoadPreferences();
        var prefs = AppState.Preferences.Preferences;
        prefs.manualRemoveCounts.Clear();
        prefs.confirmedCloseCounts.Clear();
        prefs.cancelCloseCounts.Clear();
        prefs.manualSkipCounts.Clear();
        prefs.protectCounts.Clear();
        prefs.forceCounts.Clear();
        prefs.ignoredProtectionSuggestions = Array.Empty<string>();
        prefs.ignoredForceSuggestions = Array.Empty<string>();
        prefs.ignoredCloseSuggestions = Array.Empty<string>();
        AppState.Preferences.SavePreferences();
        ShowStatus("本地学习数据已重置。");
    }

    private void OpenWebsite_Click(object sender, RoutedEventArgs e)
    {
        OpenSupportUrl("");
    }

    private void OpenReadme_Click(object sender, RoutedEventArgs e)
    {
        OpenSupportUrl("docs.html");
    }

    private void OpenDevelopmentDocs_Click(object sender, RoutedEventArgs e)
    {
        OpenSupportUrl("faq.html");
    }

    private void OpenFeedback_Click(object sender, RoutedEventArgs e)
    {
        OpenSupportUrl("feedback.html");
    }

    private void CopySystemInfo_Click(object sender, RoutedEventArgs e)
    {
        var text = string.Join(Environment.NewLine, new[]
        {
            VersionText.Text,
            OsInfoText.Text,
            CpuInfoText.Text,
            MemoryInfoText.Text,
            "配置文件：" + AppState.ConfigPath
        });

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        ShowStatus("系统信息已复制。");
    }

    private void PopulateSystemInfo()
    {
        Version version = typeof(App).Assembly.GetName().Version ?? new Version(1, 0, 0);
        VersionText.Text = $"版本：v{version.Major}.{version.Minor}.{version.Build}";
        OsInfoText.Text = GetWindowsDisplayName();
        CpuInfoText.Text = GetProcessorName();
        MemoryInfoText.Text = GetMemoryText();
    }

    private void OpenSupportUrl(string fragment)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SupportSiteUrl + fragment,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowStatus("无法打开网站：" + ex.Message, isError: true);
        }
    }

    private static string GetWindowsDisplayName()
    {
        try
        {
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string productName = key?.GetValue("ProductName") as string ?? "Windows";
            string displayVersion = key?.GetValue("DisplayVersion") as string ?? "";
            string build = key?.GetValue("CurrentBuildNumber") as string ?? "";
            return string.IsNullOrWhiteSpace(displayVersion)
                ? $"{productName} ({build})"
                : $"{productName} {displayVersion} ({build})";
        }
        catch
        {
            return Environment.OSVersion.VersionString;
        }
    }

    private static string GetProcessorName()
    {
        try
        {
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            string name = key?.GetValue("ProcessorNameString") as string;
            return string.IsNullOrWhiteSpace(name) ? "CPU 信息不可用" : name.Trim();
        }
        catch
        {
            return "CPU 信息不可用";
        }
    }

    private static string GetMemoryText()
    {
        var status = new MemoryStatusEx();
        status.dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
        if (GlobalMemoryStatusEx(ref status))
        {
            double gb = status.ullTotalPhys / 1024d / 1024d / 1024d;
            return $"{gb:0.#} GB RAM";
        }

        return "内存信息不可用";
    }

    private static string[] ParseLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        return text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l))
            .ToArray();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
