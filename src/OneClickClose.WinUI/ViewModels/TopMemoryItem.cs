using Microsoft.UI.Xaml.Media;

namespace OneClickClose.WinUI.ViewModels;

public class TopMemoryItem
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Detail { get; set; }
    public string Memory { get; set; }
    public string Cpu { get; set; }
    public SolidColorBrush StatusDot { get; set; }
    public string StatusText { get; set; }
    public string Action { get; set; }
    public SolidColorBrush ActionBg { get; set; }
    public string Suggestion { get; set; }
    public SolidColorBrush SuggestionFg { get; set; }
    public string IconGlyph { get; set; }
    public SolidColorBrush IconBg { get; set; }
    public SolidColorBrush IconFg { get; set; }
    public int RiskScore { get; set; }
}
