using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OneClickClose.Core;
using OneClickClose.WinUI.ViewModels;

namespace OneClickClose.WinUI.Pages;

public sealed partial class LogsPage : Page
{
    private bool _showingHistory;
    private readonly ObservableCollection<LogLineViewModel> _colorLogLines = new();

    private NotifyCollectionChangedEventHandler _logHandler;

    public LogsPage()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;

        CurrentLogList.ItemsSource = _colorLogLines;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _logHandler = OnLogLinesChanged;
        AppState.LogLines.CollectionChanged += _logHandler;

        RebuildColorLog();

        if (_showingHistory)
            ShowHistory();
        else
            ShowCurrentLog();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_logHandler != null)
        {
            AppState.LogLines.CollectionChanged -= _logHandler;
            _logHandler = null;
        }
    }

    private void OnLogLinesChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (string line in e.NewItems)
                _colorLogLines.Add(new LogLineViewModel(line));
        }
        // 源集合裁剪最旧行时同步镜像，防止页面集合无界增长
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (var _ in e.OldItems)
                if (_colorLogLines.Count > 0) _colorLogLines.RemoveAt(0);
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
            _colorLogLines.Clear();

        if (!_showingHistory)
            CurrentLogScroll.ChangeView(null, double.MaxValue, null);
    }

    private void RebuildColorLog()
    {
        _colorLogLines.Clear();
        foreach (string line in AppState.LogLines)
            _colorLogLines.Add(new LogLineViewModel(line));
    }

    private void CurrentLogBtn_Click(object sender, RoutedEventArgs e) { ShowCurrentLog(); }
    private void HistoryBtn_Click(object sender, RoutedEventArgs e) { ShowHistory(); }

    private void ClearBtn_Click(object sender, RoutedEventArgs e)
    {
        AppState.ClearLog();
        _colorLogLines.Clear();
        ShowCurrentLog();
    }

    private void ShowCurrentLog()
    {
        _showingHistory = false;
        CurrentLogBtn.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        HistoryBtn.FontWeight = Microsoft.UI.Text.FontWeights.Normal;

        CurrentLogScroll.Visibility = Visibility.Visible;
        HistoryScroll.Visibility = Visibility.Collapsed;

        if (_colorLogLines.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            EmptyText.Text = "执行一键关闭后将在此显示日志";
        }
        else
        {
            EmptyState.Visibility = Visibility.Collapsed;
        }

        CurrentLogScroll.ChangeView(null, double.MaxValue, null);
    }

    private void ShowHistory()
    {
        _showingHistory = true;
        CurrentLogBtn.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        HistoryBtn.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;

        CurrentLogScroll.Visibility = Visibility.Collapsed;
        HistoryScroll.Visibility = Visibility.Visible;

        AppState.LoadPreferences();
        var history = AppState.Preferences?.History;

        if (history == null || history.records == null || history.records.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            EmptyText.Text = "暂无历史记录";
            HistoryCountBadge.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        HistoryCountBadge.Visibility = Visibility.Visible;
        HistoryCountText.Text = history.records.Count + " 条";

        var items = new List<HistoryRowViewModel>();
        for (int i = history.records.Count - 1; i >= 0; i--)
        {
            var r = history.records[i];
            items.Add(new HistoryRowViewModel(r));
        }
        HistoryList.ItemsSource = items;
    }
}
