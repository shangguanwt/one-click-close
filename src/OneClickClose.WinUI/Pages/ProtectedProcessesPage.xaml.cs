using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.ViewModels;

namespace OneClickClose.WinUI.Pages;

public sealed partial class ProtectedProcessesPage : Page
{
    private List<ProtectedRowViewModel> _allRows;
    private bool _isLoaded;
    private DateTime _loadedScanTime;
    private int _bindVersion;
    private int _refreshVersion;
    private bool _isBindingComplete;

    public ProtectedProcessesPage()
    {
        InitializeComponent();
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
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
    }

    public void FocusSearch() => SearchBox?.Focus(FocusState.Programmatic);

    private void ShowDeferredLoadingState()
    {
        if (!AppState.HasPlan || AppState.ProtectedRows == null || AppState.ProtectedRows.Count == 0)
        {
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        NoResultsState.Visibility = Visibility.Collapsed;
        ProcessList.Visibility = Visibility.Collapsed;
        CountBadge.Visibility = Visibility.Visible;
        CountText.Text = "加载中...";
        FooterText.Text = "正在加载白名单与保护进程...";
    }

    private async void RefreshData()
    {
        int refreshVersion = ++_refreshVersion;
        UpdateSuggestions();

        if (!AppState.HasPlan || AppState.ProtectedRows == null || AppState.ProtectedRows.Count == 0)
        {
            _bindVersion++;
            _isBindingComplete = true;
            ProcessList.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            NoResultsState.Visibility = Visibility.Collapsed;
            CountBadge.Visibility = Visibility.Collapsed;
            FooterText.Text = "共 0 项受保护应用";
            EmptyText.Text = AppState.HasPlan
                ? "没有受保护的进程"
                : "点击“重新扫描”查看保护列表";
            _allRows = null;
            RecentProtectedList.ItemsSource = null;
            RecentEmptyText.Visibility = Visibility.Visible;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        ProcessList.Visibility = Visibility.Visible;
        CountBadge.Visibility = Visibility.Visible;

        IReadOnlyList<ProcessGroupRow> sourceRows = AppState.ProtectedRows.ToList();
        _allRows = new List<ProtectedRowViewModel>(sourceRows.Count);
        ProcessList.ItemsSource = null;
        _isBindingComplete = false;
        _loadedScanTime = AppState.LastScanTime;

        for (int i = 0; i < sourceRows.Count; i++)
        {
            if (!_isLoaded || refreshVersion != _refreshVersion)
            {
                return;
            }

            _allRows.Add(new ProtectedRowViewModel(sourceRows[i]));
            if (i == 23 || (i > 23 && (i + 1) % 32 == 0))
            {
                await Task.Delay(1);
            }
        }

        _isBindingComplete = true;

        int totalProcesses = _allRows.Sum(r => r.Count);
        CountText.Text = _allRows.Count + " 组 / " + totalProcesses + " 个";
        FooterText.Text = "共 " + _allRows.Count + " 项受保护应用 · 一键关闭会自动跳过，规则仅保存在本机";
        RecentProtectedList.ItemsSource = _allRows
            .OrderByDescending(r => ParseMemoryMb(r.MemoryText))
            .Take(3)
            .ToList();
        RecentEmptyText.Visibility = _allRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private async void ProcessList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Item is not ProtectedRowViewModel vm)
        {
            return;
        }

        await vm.EnsureIconLoadedAsync();
    }

    private void ApplyFilter()
    {
        if (_allRows == null)
        {
            return;
        }

        string query = SearchFilterHelper.ExtractQuery(SearchBox?.Text);
        var filtered = SearchFilterHelper.FilterByQuery(
            _allRows,
            query,
            r => string.Join(" ", r.Process, r.Note, r.SummaryLine, r.ReasonText, r.PathText));

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

        BindRows(result);
    }

    private async void BindRows(IReadOnlyList<ProtectedRowViewModel> rows)
    {
        int version = ++_bindVersion;
        _isBindingComplete = false;
        if (rows == null || rows.Count <= 80)
        {
            ProcessList.ItemsSource = rows;
            _isBindingComplete = true;
            return;
        }

        var visible = new ObservableCollection<ProtectedRowViewModel>();
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

    private async void AddProtected_Click(object sender, RoutedEventArgs e)
    {
        var input = new TextBox
        {
            PlaceholderText = "例如 OneDrive、steam、Code",
            MinWidth = 360
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "输入要保护的进程名。下次扫描后，它会从一键关闭计划中跳过。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BodyTextBrush"]
        });
        content.Children.Add(input);

        var dialog = new ContentDialog
        {
            Title = "添加白名单应用",
            Content = content,
            PrimaryButtonText = "添加",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await AddProtectedNamesAsync(new[] { input.Text }, "已添加到白名单");
        }
    }

    private async void ImportRules_Click(object sender, RoutedEventArgs e)
    {
        var input = new TextBox
        {
            PlaceholderText = "每行一个名称，也支持用逗号分隔。例如：\r\nsteam\r\nOneDrive\r\nCode",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 150,
            MinWidth = 420
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "批量粘贴需要保护的进程名。规则只会保存到本机配置。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BodyTextBrush"]
        });
        content.Children.Add(input);

        var dialog = new ContentDialog
        {
            Title = "导入白名单规则",
            Content = content,
            PrimaryButtonText = "导入",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await AddProtectedNamesAsync(SplitImportedNames(input.Text), "已导入白名单规则");
        }
    }

    private async void AddSuggestionToProtected_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is CandidateRowViewModel suggestion)
        {
            await AddProtectedNamesAsync(new[] { suggestion.ProcessNameRaw }, "已添加到白名单");
        }
    }

    private async void ShowProtectedDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem item || item.Tag is not ProtectedRowViewModel row)
        {
            return;
        }

        string detail = string.Join(Environment.NewLine, new[]
        {
            "应用：" + row.Process,
            "路径：" + row.PathText,
            "保护原因：" + row.ReasonText,
            "进程数量：" + row.CountText,
            "内存占用：" + row.MemoryText,
            "说明：" + row.SummaryLine
        });

        var dialog = new ContentDialog
        {
            Title = "白名单详情",
            Content = detail,
            CloseButtonText = "确定",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async Task AddProtectedNamesAsync(IEnumerable<string> names, string successTitle)
    {
        var normalizedNames = names
            .Select(NormalizeProcessName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedNames.Count == 0)
        {
            await ShowToastAsync("没有可添加的应用名称");
            return;
        }

        AppConfig cfg = AppConfig.Load(AppState.ConfigPath);
        var list = new List<string>(cfg.protectedNames ?? Array.Empty<string>());
        int added = 0;
        foreach (string name in normalizedNames)
        {
            if (!list.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(name);
                AppState.Preferences.RecordProtected(name);
                added++;
            }
        }

        cfg.protectedNames = list.ToArray();
        AppConfig.Save(AppState.ConfigPath, cfg);

        if (added > 0)
        {
            await AppState.ScanAsync(AppState.ConfigPath);
            RefreshData();
        }

        await ShowToastAsync(added == 0
            ? "这些应用已经在白名单中"
            : successTitle + "：" + added + " 项");
    }

    private void UpdateSuggestions()
    {
        var protectedSet = AppConfig.Load(AppState.ConfigPath).ProtectedSet();
        var suggestions = (AppState.CandidateRows ?? new List<ProcessGroupRow>())
            .Where(row => row != null)
            .Where(row => !protectedSet.Contains(row.Process ?? ""))
            .Where(row => row.Action == ProcessPlanner.ActionReport || row.IsHighRisk || row.RiskScore >= 45)
            .GroupBy(row => NormalizeProcessName(row.Process), StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(row => row.RiskScore)
                .ThenByDescending(row => row.MemoryMb)
                .First())
            .OrderByDescending(row => row.RiskScore)
            .ThenByDescending(row => row.MemoryMb)
            .Take(2)
            .Select(row => new CandidateRowViewModel(row))
            .ToList();

        SuggestedProtectList.ItemsSource = suggestions;
        SuggestionEmptyText.Visibility = suggestions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static IEnumerable<string> SplitImportedNames(string text)
    {
        return (text ?? string.Empty)
            .Split(new[] { "\r\n", "\n", "\r", ",", "，", ";", "；" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim());
    }

    private static string NormalizeProcessName(string value)
    {
        string text = (value ?? string.Empty).Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        if (text.Contains("\\") || text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            text = Path.GetFileNameWithoutExtension(text);
        }

        return text.Trim();
    }

    private static long ParseMemoryMb(string memoryText)
    {
        string first = (memoryText ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return long.TryParse(first, out long value) ? value : 0;
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
