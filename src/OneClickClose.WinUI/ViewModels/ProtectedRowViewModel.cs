using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using OneClickClose.Core;
using OneClickClose.WinUI.Helpers;
using OneClickClose.WinUI.Services;

namespace OneClickClose.WinUI.ViewModels;

public class ProtectedRowViewModel : INotifyPropertyChanged
{
    private bool _iconLoadStarted;
    private ImageSource _iconSource;

    public ProcessGroupRow Raw { get; }
    public string Process { get; }
    public string Action { get; }
    public string Note { get; }
    public int Count { get; }
    public string CountText { get; }
    public string MemoryText { get; }
    public string PathText { get; }
    public string SummaryLine { get; }
    public string ReasonText { get; }
    public SolidColorBrush ReasonBadgeBackground => BuildReasonBackground(Raw);
    public SolidColorBrush ReasonForeground => BuildReasonForeground(Raw);
    public ImageSource IconSource
    {
        get => _iconSource;
        private set
        {
            if (ReferenceEquals(_iconSource, value))
            {
                return;
            }

            _iconSource = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IconImageVisibility));
            OnPropertyChanged(nameof(FallbackIconVisibility));
        }
    }
    public Visibility IconImageVisibility => IconSource is null ? Visibility.Collapsed : Visibility.Visible;
    public Visibility FallbackIconVisibility => IconSource is null ? Visibility.Visible : Visibility.Collapsed;
    public string IconGlyph { get; }
    public SolidColorBrush IconBackground => ColorHelper.SafeGreenSoft;
    public SolidColorBrush IconForeground => ColorHelper.Safe;
    public event PropertyChangedEventHandler PropertyChanged;

    public ProtectedRowViewModel(ProcessGroupRow row)
    {
        Raw = row;
        Process = row.Process ?? "";
        Action = row.Action ?? "";
        Note = row.Note ?? "";
        Count = row.Count;
        CountText = row.Count.ToString();
        MemoryText = row.MemoryMb > 0 ? row.MemoryMb + " MB" : "-";
        PathText = string.IsNullOrWhiteSpace(row.Path) ? "-" : row.Path;
        SummaryLine = BuildSummaryLine(row);
        ReasonText = BuildReasonText(row);
        IconGlyph = row.HasWindow ? "\uE7F4" : "\uE9D5";
    }

    public async Task EnsureIconLoadedAsync()
    {
        if (_iconLoadStarted || IconSource != null)
        {
            return;
        }

        _iconLoadStarted = true;
        try
        {
            IconSource = await ProcessIconProvider.GetIconSourceAsync(Raw);
        }
        catch
        {
            IconSource = null;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string BuildSummaryLine(ProcessGroupRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.UsageHint))
        {
            return row.UsageHint;
        }

        if (!string.IsNullOrWhiteSpace(row.Note))
        {
            return row.Note;
        }

        return "受保护应用";
    }

    private static string BuildReasonText(ProcessGroupRow row)
    {
        string note = row.Note ?? "";
        if (note.Contains("系统路径"))
        {
            return "系统保护";
        }

        if (note.Contains("Codex"))
        {
            return "工具保护";
        }

        if (note.Contains("终端"))
        {
            return "终端保护";
        }

        if (note.Contains("保护名单"))
        {
            return "保护名单";
        }

        return "保护";
    }

    private static SolidColorBrush BuildReasonBackground(ProcessGroupRow row)
    {
        string note = row.Note ?? "";
        if (note.Contains("系统路径") || note.Contains("终端"))
        {
            return ColorHelper.CopperSoft;
        }

        if (note.Contains("Codex"))
        {
            return ColorHelper.CyanSoft;
        }

        return ColorHelper.SafeGreenSoft;
    }

    private static SolidColorBrush BuildReasonForeground(ProcessGroupRow row)
    {
        string note = row.Note ?? "";
        if (note.Contains("系统路径") || note.Contains("终端"))
        {
            return ColorHelper.Copper;
        }

        if (note.Contains("Codex"))
        {
            return ColorHelper.AccentLight;
        }

        return ColorHelper.Safe;
    }
}
