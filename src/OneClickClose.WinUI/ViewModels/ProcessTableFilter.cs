using OneClickClose.Core;

namespace OneClickClose.WinUI.ViewModels;

public static class ProcessTableFilter
{
    public static List<TopMemoryItem> Apply(IEnumerable<TopMemoryItem> items, string searchText, int filterIndex)
    {
        if (items == null)
        {
            return new List<TopMemoryItem>();
        }

        string search = (searchText ?? string.Empty).Trim();
        IEnumerable<TopMemoryItem> filtered = items;

        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(p =>
                ((p.Name ?? string.Empty) + " " + (p.Detail ?? string.Empty) + " " + (p.Suggestion ?? string.Empty))
                .Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        filtered = filterIndex switch
        {
            1 => filtered.Where(p => p.Action == ProcessPlanner.ActionGraceful),
            2 => filtered.Where(p => p.Action == ProcessPlanner.ActionForce),
            3 => filtered.Where(p => p.Action == ProcessPlanner.ActionReport),
            _ => filtered
        };

        return filtered.ToList();
    }
}
