using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.ViewModels;

namespace OneClickClose.WinUI.Pages;

public sealed partial class ProtectedProcessesPage : Page
{
    private List<ProtectedRowViewModel> _allRows;

    public ProtectedProcessesPage()
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
        if (!AppState.HasPlan || AppState.ProtectedRows == null || AppState.ProtectedRows.Count == 0)
        {
            ProcessList.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            NoResultsState.Visibility = Visibility.Collapsed;
            CountBadge.Visibility = Visibility.Collapsed;
            EmptyText.Text = AppState.HasPlan
                ? "没有受保护的进程"
                : "点击「扫描进程」查看保护列表";
            _allRows = null;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        ProcessList.Visibility = Visibility.Visible;
        CountBadge.Visibility = Visibility.Visible;

        int total = 0;
        _allRows = new List<ProtectedRowViewModel>();
        foreach (var row in AppState.ProtectedRows)
        {
            total += row.Count;
            _allRows.Add(new ProtectedRowViewModel(row));
        }
        CountText.Text = total + " 个";

        ApplyFilter();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { ApplyFilter(); }

    private void ApplyFilter()
    {
        if (_allRows == null) return;

        string query = SearchFilterHelper.ExtractQuery(SearchBox?.Text);
        var filtered = SearchFilterHelper.FilterByQuery(_allRows, query, r => r.Process + " " + (r.Note ?? ""));

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

        ProcessList.ItemsSource = result;
    }
}
