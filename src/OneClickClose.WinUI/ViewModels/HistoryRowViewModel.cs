using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;

namespace OneClickClose.WinUI.ViewModels;

public class HistoryRowViewModel
{
    private const int TimestampDisplayLength = 16;

    public string Timestamp { get; }
    public string ProcessName { get; }
    public string Action { get; }
    public SolidColorBrush ActionBg { get; }

    public HistoryRowViewModel(CleanupHistoryRecord record)
    {
        Timestamp = record.timestamp?.Length >= TimestampDisplayLength
            ? record.timestamp.Substring(0, TimestampDisplayLength)
            : record.timestamp ?? "";
        ProcessName = record.processName ?? "";
        Action = record.action ?? "";
        ActionBg = ColorHelper.GetActionBackground(record.action);
    }
}
