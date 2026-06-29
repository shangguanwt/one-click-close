using OneClickClose.Core;

namespace OneClickClose.WinUI.ViewModels;

public class ProtectedRowViewModel
{
    public string Process { get; }
    public string Action { get; }
    public string Note { get; }
    public int Count { get; }
    public string MemoryText { get; }

    public ProtectedRowViewModel(ProcessGroupRow row)
    {
        Process = row.Process;
        Action = row.Action;
        Note = row.Note ?? "";
        Count = row.Count;
        MemoryText = row.MemoryMb > 0 ? row.MemoryMb + " MB" : "-";
    }
}
