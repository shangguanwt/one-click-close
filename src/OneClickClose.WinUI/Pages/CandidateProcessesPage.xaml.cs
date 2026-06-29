using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.ViewModels;

namespace OneClickClose.WinUI.Pages;

public sealed partial class CandidateProcessesPage : Page
{
    private List<CandidateRowViewModel> _allRows;
    private string _activeFilter = "all";

    public CandidateProcessesPage()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { RefreshData(); }
    private void OnUnloaded(object sender, RoutedEventArgs e) { /* cleanup placeholder for symmetry */ }

    public void FocusSearch() { SearchBox?.Focus(FocusState.Programmatic); }

    private void RefreshData()
    {
        if (!AppState.HasPlan || AppState.CandidateRows == null || AppState.CandidateRows.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            NoResultsState.Visibility = Visibility.Collapsed;
            ProcessList.Visibility = Visibility.Collapsed;
            CountBadge.Visibility = Visibility.Collapsed;
            return;
        }
        EmptyState.Visibility = Visibility.Collapsed;
        ProcessList.Visibility = Visibility.Visible;
        _allRows = AppState.CandidateRows.Select(r => new CandidateRowViewModel(r)).ToList();
        CountBadge.Visibility = Visibility.Visible;
        CountText.Text = _allRows.Count + " 组";
        ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { ApplyFilter(); }

    private void FilterAllBtn_Click(object sender, RoutedEventArgs e) { _activeFilter = "all"; UpdateFilterStyles(); ApplyFilter(); }
    private void FilterGracefulBtn_Click(object sender, RoutedEventArgs e) { _activeFilter = "graceful"; UpdateFilterStyles(); ApplyFilter(); }
    private void FilterForceBtn_Click(object sender, RoutedEventArgs e) { _activeFilter = "force"; UpdateFilterStyles(); ApplyFilter(); }

    private void UpdateFilterStyles()
    {
        FilterAllBtn.FontWeight = _activeFilter == "all" ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        FilterGracefulBtn.FontWeight = _activeFilter == "graceful" ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
        FilterForceBtn.FontWeight = _activeFilter == "force" ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
    }

    private void ApplyFilter()
    {
        if (_allRows == null) return;
        string query = SearchFilterHelper.ExtractQuery(SearchBox?.Text);
        var filtered = SearchFilterHelper.FilterByQuery(_allRows, query, r => r.Process + " " + (r.Note ?? ""));
        if (_activeFilter == "graceful") filtered = filtered.Where(r => r.Action == ProcessPlanner.ActionGraceful);
        else if (_activeFilter == "force") filtered = filtered.Where(r => r.Action == ProcessPlanner.ActionForce);

        var result = filtered.ToList();
        if (result.Count == 0 && _allRows.Count > 0)
        { NoResultsState.Visibility = Visibility.Visible; ProcessList.Visibility = Visibility.Collapsed; }
        else
        { NoResultsState.Visibility = Visibility.Collapsed; ProcessList.Visibility = Visibility.Visible; }
        ProcessList.ItemsSource = result;
    }

    private void AddToProtected_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is CandidateRowViewModel vm)
        {
            var cfg = AppConfig.Load(AppState.ConfigPath);
            var list = new List<string>(cfg.protectedNames ?? Array.Empty<string>());
            if (!list.Contains(vm.ProcessNameRaw, StringComparer.OrdinalIgnoreCase)) list.Add(vm.ProcessNameRaw);
            cfg.protectedNames = list.ToArray();
            AppConfig.Save(AppState.ConfigPath, cfg);
            _ = ShowToastAsync("已将 " + vm.ProcessNameRaw + " 添加到保护名单");
        }
    }

    private void AddToForce_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is CandidateRowViewModel vm)
        {
            var cfg = AppConfig.Load(AppState.ConfigPath);
            var list = new List<string>(cfg.forceAllowedNames ?? Array.Empty<string>());
            if (!list.Contains(vm.ProcessNameRaw, StringComparer.OrdinalIgnoreCase)) list.Add(vm.ProcessNameRaw);
            cfg.forceAllowedNames = list.ToArray();
            AppConfig.Save(AppState.ConfigPath, cfg);
            _ = ShowToastAsync("已将 " + vm.ProcessNameRaw + " 添加到强制清理名单");
        }
    }

    private void RemoveFromTarget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is CandidateRowViewModel vm)
        {
            var cfg = AppConfig.Load(AppState.ConfigPath);
            var list = new List<string>(cfg.targetNames ?? Array.Empty<string>());
            list.RemoveAll(n => StringComparer.OrdinalIgnoreCase.Equals(n, vm.ProcessNameRaw));
            cfg.targetNames = list.ToArray();
            AppConfig.Save(AppState.ConfigPath, cfg);
            _ = ShowToastAsync("已从目标名单移除 " + vm.ProcessNameRaw);
        }
    }

    private async Task ShowToastAsync(string message)
    {
        var dialog = new ContentDialog { Content = message, CloseButtonText = "确定", DefaultButton = ContentDialogButton.Close };
        dialog.XamlRoot = this.XamlRoot;
        await dialog.ShowAsync();
    }
}
