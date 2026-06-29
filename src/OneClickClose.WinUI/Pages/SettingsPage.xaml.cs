using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;
using Windows.Storage.Pickers;
using OneClickClose.WinUI.Helpers;

namespace OneClickClose.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    private AppConfig _config;

    public SettingsPage()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { LoadConfig(); }
    private void OnUnloaded(object sender, RoutedEventArgs e) { /* cleanup placeholder for symmetry */ }

    private void LoadConfig()
    {
        try
        {
            _config = AppConfig.Load(AppState.ConfigPath);
            PopulateUI();
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
        TargetNamesBox.Text = string.Join("\r\n", _config.targetNames ?? Array.Empty<string>());
        ProtectedNamesBox.Text = string.Join("\r\n", _config.protectedNames ?? Array.Empty<string>());
        ForceNamesBox.Text = string.Join("\r\n", _config.forceAllowedNames ?? Array.Empty<string>());
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _config.waitSeconds = (int)WaitSecondsBox.Value;
            _config.gracefulTimeoutSeconds = (int)GracefulTimeoutBox.Value;
            _config.queryTimeoutSeconds = (int)QueryTimeoutBox.Value;
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
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
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
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var imported = AppConfig.Load(file.Path);
                AppConfig.Save(AppState.ConfigPath, imported);
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
}
