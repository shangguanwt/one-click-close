using Microsoft.UI.Xaml.Media;
using OneClickClose.WinUI.Helpers;

namespace OneClickClose.WinUI.ViewModels;

/// <summary>
/// Color-coded log line with muted ink-green/copper palette.
/// Shared between OverviewPage and LogsPage.
/// </summary>
public class LogLineViewModel
{
    public string Text { get; }
    public SolidColorBrush Color { get; }

    public LogLineViewModel(string line)
    {
        Text = line;

        if (line.StartsWith("[错误]") || line.StartsWith("[失败]"))
            Color = ColorHelper.Danger;
        else if (line.StartsWith("[成功]") || line.StartsWith("[完成]"))
            Color = ColorHelper.Safe;
        else if (line.StartsWith("[警告]") || line.StartsWith("[跳过]"))
            Color = ColorHelper.Copper;
        else if (line.StartsWith("[取消]"))
            Color = ColorHelper.Accent;
        else if (line.StartsWith("[关闭]") || line.StartsWith("[强制]") || line.StartsWith("[温和]"))
            Color = ColorHelper.Safe;
        else
            Color = ColorHelper.Info;
    }
}
