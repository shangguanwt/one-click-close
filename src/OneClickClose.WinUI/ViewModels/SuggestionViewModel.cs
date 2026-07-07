using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;

namespace OneClickClose.WinUI.ViewModels;

public class SuggestionViewModel
{
    public string Type { get; }
    public string ProcessName { get; }
    public string Reason { get; }
    public int Count { get; }
    public SolidColorBrush TypeBg { get; }
    public UserPreferenceSuggestion Raw { get; }

    public SuggestionViewModel(UserPreferenceSuggestion suggestion)
    {
        Raw = suggestion;
        Type = suggestion.Type;
        ProcessName = suggestion.ProcessName;
        Reason = suggestion.Reason ?? "";
        Count = suggestion.Count;

        TypeBg = suggestion.Type == "保护名单" || suggestion.Type == "习惯关闭"
            ? ColorHelper.Accent
            : ColorHelper.CopperSoft;
    }
}
