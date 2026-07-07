using System.Collections.Generic;
using System.Linq;

namespace OneClickClose.Core;

public static class ProcessGroupSearchText
{
    public static string Build(ProcessGroupRow row)
    {
        var parts = new List<string>
        {
            row.Process,
            row.Note,
            row.Action,
            row.Status,
            row.Path,
            row.UsageHint,
            row.HabitHint
        };

        foreach (ProcessRecord child in row.Children ?? new List<ProcessRecord>())
        {
            parts.Add(child.Id.ToString());
            parts.Add(child.ProcessName);
            parts.Add(child.Action);
            parts.Add(child.Status);
            parts.Add(child.MainWindowTitle);
            parts.Add(child.Path);
            parts.Add(child.Reason);
            parts.Add(child.UsageHint);
            parts.Add(child.HabitHint);
        }

        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant();
    }
}
