using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OneClickClose.Core;
using OneClickClose.WinUI.ViewModels;

namespace OneClickClose.WinUI.Pages;

public sealed partial class LearningPage : Page
{
    private List<SuggestionViewModel> _suggestions;

    public LearningPage()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { RefreshSuggestions(); }
    private void OnUnloaded(object sender, RoutedEventArgs e) { /* cleanup placeholder for symmetry */ }

    private void RefreshSuggestions()
    {
        AppState.LoadPreferences();
        var config = AppConfig.Load(AppState.ConfigPath);
        var rawSuggestions = AppState.Preferences.BuildSuggestions(config);

        if (rawSuggestions == null || rawSuggestions.Count == 0)
        {
            SuggestionsList.Visibility = Visibility.Collapsed;
            NoSuggestionsPanel.Visibility = Visibility.Visible;
            NoSuggestionsText.Text = "暂无建议。使用更多后系统会生成个性化建议。";
            return;
        }

        SuggestionsList.Visibility = Visibility.Visible;
        NoSuggestionsPanel.Visibility = Visibility.Collapsed;

        _suggestions = new List<SuggestionViewModel>();
        foreach (var s in rawSuggestions)
            _suggestions.Add(new SuggestionViewModel(s));
        SuggestionsList.ItemsSource = _suggestions;
    }

    private void ApplySuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SuggestionViewModel vm)
        {
            var config = AppConfig.Load(AppState.ConfigPath);

            if (vm.Raw.Type == "保护名单")
            {
                var list = new List<string>(config.protectedNames ?? System.Array.Empty<string>());
                if (!list.Contains(vm.ProcessName))
                {
                    list.Add(vm.ProcessName);
                    config.protectedNames = list.ToArray();
                }
            }
            else if (vm.Raw.Type == "强制清理名单")
            {
                var list = new List<string>(config.forceAllowedNames ?? System.Array.Empty<string>());
                if (!list.Contains(vm.ProcessName))
                {
                    list.Add(vm.ProcessName);
                    config.forceAllowedNames = list.ToArray();
                }
            }

            AppConfig.Save(AppState.ConfigPath, config);
            RemoveSuggestionAndRefresh(vm);
        }
    }

    private void IgnoreSuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SuggestionViewModel vm)
        {
            AppState.Preferences.IgnoreSuggestion(vm.Raw);
            RemoveSuggestionAndRefresh(vm);
        }
    }

    private void RemoveSuggestionAndRefresh(SuggestionViewModel vm)
    {
        _suggestions.Remove(vm);
        SuggestionsList.ItemsSource = null;
        SuggestionsList.ItemsSource = _suggestions;

        if (_suggestions.Count == 0)
        {
            SuggestionsList.Visibility = Visibility.Collapsed;
            NoSuggestionsPanel.Visibility = Visibility.Visible;
            NoSuggestionsText.Text = "所有建议已处理完毕。";
        }
    }
}
