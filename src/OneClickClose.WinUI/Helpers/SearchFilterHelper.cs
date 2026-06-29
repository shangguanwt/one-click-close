using System;
using System.Collections.Generic;
using System.Linq;

namespace OneClickClose.WinUI.Helpers;

/// <summary>
/// Shared search/filter logic for process list pages.
/// </summary>
public static class SearchFilterHelper
{
    /// <summary>
    /// Filters a source collection by a text query, matching against the provided selector.
    /// </summary>
    public static IEnumerable<T> FilterByQuery<T>(IEnumerable<T> source, string query, Func<T, string> selector)
    {
        if (string.IsNullOrEmpty(query))
            return source;

        return source.Where(item =>
        {
            string value = selector(item);
            return value != null && value.ToLowerInvariant().Contains(query);
        });
    }

    /// <summary>
    /// Extracts and normalizes a search query from a text box.
    /// </summary>
    public static string ExtractQuery(string text)
    {
        return text?.Trim()?.ToLowerInvariant() ?? "";
    }
}
