using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneClickClose.Core.Helpers;

namespace OneClickClose.Core
{
    public sealed class UserPreferencesStore
    {
        private readonly string dataDir;
        private readonly string prefsPath;
        private readonly string historyPath;
        private UserPreferencesData preferences;
        private CleanupHistoryDocument history;

        public UserPreferencesData Preferences
        {
            get { return preferences; }
        }

        public CleanupHistoryDocument History
        {
            get { return history; }
        }

        internal UserPreferencesStore(string dataDir, UserPreferencesData preferences, CleanupHistoryDocument history)
        {
            this.dataDir = dataDir;
            prefsPath = Path.Combine(dataDir, "user-prefs.json");
            historyPath = Path.Combine(dataDir, "history.json");
            this.preferences = Normalize(preferences);
            this.history = history ?? new CleanupHistoryDocument();
        }

        public static UserPreferencesStore Load(string appName = "OneClickClose")
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
            Directory.CreateDirectory(dir);
            UserPreferencesData prefs = JsonFileStore.ReadJson<UserPreferencesData>(Path.Combine(dir, "user-prefs.json")) ?? new UserPreferencesData();
            CleanupHistoryDocument history = JsonFileStore.ReadJson<CleanupHistoryDocument>(Path.Combine(dir, "history.json")) ?? new CleanupHistoryDocument();
            return new UserPreferencesStore(dir, prefs, history);
        }

        public int IncrementManualRemove(string processName)
        {
            int count = IncrementCounter(preferences.manualRemoveCounts, processName);
            IncrementCounter(preferences.manualSkipCounts, processName);
            SavePreferences();
            return count;
        }

        public int RecordManualSkip(string processName)
        {
            int count = IncrementCounter(preferences.manualSkipCounts, processName);
            SavePreferences();
            return count;
        }

        public int RecordProtected(string processName)
        {
            int count = IncrementCounter(preferences.protectCounts, processName);
            SavePreferences();
            return count;
        }

        public int RecordForceAllowed(string processName)
        {
            int count = IncrementCounter(preferences.forceCounts, processName);
            SavePreferences();
            return count;
        }

        public void RecordCloseConfirmed(IEnumerable<ProcessRecord> records)
        {
            RecordDecision(records, "用户确认关闭", "confirm", preferences.confirmedCloseCounts);
        }

        public void RecordCloseCanceled(IEnumerable<ProcessRecord> records)
        {
            RecordDecision(records, "用户取消关闭", "cancel", preferences.cancelCloseCounts);
        }

        public void RecordCleanup(CloseResult result)
        {
            if (result == null)
            {
                return;
            }

            HashSet<int> completed = new HashSet<int>();
            foreach (ProcessRecord item in result.GracefulClosed ?? new List<ProcessRecord>())
            {
                AddHistory(item, ProcessPlanner.ActionGraceful, "closed");
                completed.Add(item.Id);
            }

            foreach (ProcessRecord item in result.Forced ?? new List<ProcessRecord>())
            {
                AddHistory(item, ProcessPlanner.ActionForce, "forced");
                IncrementCounter(preferences.forceCounts, item.ProcessName);
                completed.Add(item.Id);
            }

            foreach (ProcessRecord item in result.Remaining ?? new List<ProcessRecord>())
            {
                if (!completed.Contains(item.Id))
                {
                    AddHistory(item, "仍在运行", "remaining");
                    completed.Add(item.Id);
                }
            }

            foreach (ProcessRecord item in result.ReportOnly ?? new List<ProcessRecord>())
            {
                if (!completed.Contains(item.Id))
                {
                    AddHistory(item, ProcessPlanner.ActionReport, "report");
                    completed.Add(item.Id);
                }
            }

            TrimHistory();
            SavePreferences();
            SaveHistory();
        }

        public List<UserPreferenceSuggestion> BuildSuggestions(AppConfig config)
        {
            config ??= AppConfig.CreateDefault();
            List<UserPreferenceSuggestion> suggestions = new List<UserPreferenceSuggestion>();
            HashSet<string> protectedSet = config.ProtectedSet();
            HashSet<string> forceSet = config.ForceSet();
            HashSet<string> ignoredProtect = JsonFileStore.MakeSet(preferences.ignoredProtectionSuggestions);
            HashSet<string> ignoredForce = JsonFileStore.MakeSet(preferences.ignoredForceSuggestions);
            HashSet<string> ignoredClose = JsonFileStore.MakeSet(preferences.ignoredCloseSuggestions);

            foreach (KeyValuePair<string, int> entry in preferences.manualRemoveCounts.OrderByDescending(kv => kv.Value))
            {
                AddProtectionSuggestion(suggestions, entry.Key, entry.Value, protectedSet, ignoredProtect,
                    "已从清理列表跳过 " + entry.Value + " 次，建议保护", 90);
            }

            foreach (KeyValuePair<string, int> entry in preferences.manualSkipCounts.OrderByDescending(kv => kv.Value))
            {
                AddProtectionSuggestion(suggestions, entry.Key, entry.Value, protectedSet, ignoredProtect,
                    "多次手动跳过，建议默认保护", 85);
            }

            foreach (KeyValuePair<string, int> entry in preferences.confirmedCloseCounts.OrderByDescending(kv => kv.Value))
            {
                int canceled = preferences.cancelCloseCounts.TryGetValue(entry.Key, out int cancelCount) ? cancelCount : 0;
                if (entry.Value >= 3 && entry.Value >= canceled + 2 && !ignoredClose.Contains(entry.Key))
                {
                    suggestions.Add(new UserPreferenceSuggestion
                    {
                        Type = "习惯关闭",
                        ProcessName = entry.Key,
                        Count = entry.Value,
                        Priority = 75,
                        Reason = "你已确认关闭 " + entry.Value + " 次，扫描时会优先建议关闭"
                    });
                }
            }

            foreach (IGrouping<string, CleanupHistoryRecord> group in history.records.GroupBy(r => r.processName ?? "", StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(group.Key) || forceSet.Contains(group.Key) || ignoredForce.Contains(group.Key))
                {
                    continue;
                }

                List<CleanupHistoryRecord> recent = group
                    .OrderByDescending(r => r.timestamp)
                    .Take(3)
                    .ToList();

                if (recent.Count < 3)
                {
                    continue;
                }

                bool allForced = recent.All(r => string.Equals(r.action, ProcessPlanner.ActionForce, StringComparison.Ordinal)
                    || string.Equals(r.decision, "forced", StringComparison.Ordinal));
                bool allRemaining = recent.All(r => string.Equals(r.action, "仍在运行", StringComparison.Ordinal)
                    || string.Equals(r.decision, "remaining", StringComparison.Ordinal));
                if (allForced || allRemaining)
                {
                    suggestions.Add(new UserPreferenceSuggestion
                    {
                        Type = "强制清理名单",
                        ProcessName = group.Key,
                        Count = recent.Count,
                        Priority = 80,
                        Reason = allForced ? "最近 3 次都需要强制关闭" : "最近 3 次清理后仍在运行"
                    });
                }
            }

            return suggestions
                .OrderByDescending(s => s.Priority)
                .ThenBy(s => s.ProcessName)
                .ToList();
        }

        public void IgnoreSuggestion(UserPreferenceSuggestion suggestion)
        {
            if (suggestion == null)
            {
                return;
            }

            if (string.Equals(suggestion.Type, "保护名单", StringComparison.Ordinal)
                || string.Equals(suggestion.Type, "淇濇姢鍚嶅崟", StringComparison.Ordinal))
            {
                preferences.ignoredProtectionSuggestions = JsonFileStore.AddToArray(preferences.ignoredProtectionSuggestions, suggestion.ProcessName);
            }
            else if (string.Equals(suggestion.Type, "强制清理名单", StringComparison.Ordinal)
                || string.Equals(suggestion.Type, "寮哄埗娓呯悊鍚嶅崟", StringComparison.Ordinal))
            {
                preferences.ignoredForceSuggestions = JsonFileStore.AddToArray(preferences.ignoredForceSuggestions, suggestion.ProcessName);
            }
            else if (string.Equals(suggestion.Type, "习惯关闭", StringComparison.Ordinal))
            {
                preferences.ignoredCloseSuggestions = JsonFileStore.AddToArray(preferences.ignoredCloseSuggestions, suggestion.ProcessName);
            }

            SavePreferences();
        }

        public string GetHabitHint(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return string.Empty;
            }

            string key = processName.Trim();
            int closeCount = preferences.confirmedCloseCounts.TryGetValue(key, out int closes) ? closes : 0;
            int skipCount = preferences.manualSkipCounts.TryGetValue(key, out int skips) ? skips : 0;
            int cancelCount = preferences.cancelCloseCounts.TryGetValue(key, out int cancels) ? cancels : 0;

            if (skipCount >= 3 && skipCount >= closeCount)
            {
                return "本地习惯：经常跳过，建议保护";
            }

            if (closeCount >= 3 && closeCount >= cancelCount + 2)
            {
                return "本地习惯：经常关闭，优先建议";
            }

            if (cancelCount >= 2 && cancelCount > closeCount)
            {
                return "本地习惯：最近常取消，关闭前请确认";
            }

            return string.Empty;
        }

        public void SavePreferences()
        {
            JsonFileStore.WriteJson(prefsPath, preferences);
        }

        public void SaveHistory()
        {
            JsonFileStore.WriteJson(historyPath, history);
        }

        private void RecordDecision(IEnumerable<ProcessRecord> records, string action, string decision, Dictionary<string, int> counter)
        {
            if (records == null)
            {
                return;
            }

            foreach (ProcessRecord record in records)
            {
                IncrementCounter(counter, record?.ProcessName);
                AddHistory(record, action, decision);
            }

            TrimHistory();
            SavePreferences();
            SaveHistory();
        }

        private static void AddProtectionSuggestion(
            List<UserPreferenceSuggestion> suggestions,
            string processName,
            int count,
            HashSet<string> protectedSet,
            HashSet<string> ignoredProtect,
            string reason,
            int priority)
        {
            if (count < 3 || protectedSet.Contains(processName) || ignoredProtect.Contains(processName))
            {
                return;
            }

            if (suggestions.Any(s => string.Equals(s.ProcessName, processName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(s.Type, "保护名单", StringComparison.Ordinal)))
            {
                return;
            }

            suggestions.Add(new UserPreferenceSuggestion
            {
                Type = "保护名单",
                ProcessName = processName,
                Count = count,
                Priority = priority,
                Reason = reason
            });
        }

        private void AddHistory(ProcessRecord item, string action, string decision)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ProcessName))
            {
                return;
            }

            history.records.Add(new CleanupHistoryRecord
            {
                processName = item.ProcessName,
                action = action,
                timestamp = DateTime.Now.ToString("o"),
                path = item.Path,
                processId = item.Id,
                decision = decision,
                memoryMb = item.MemoryMb
            });
        }

        private void TrimHistory()
        {
            history.records = history.records
                .OrderByDescending(r => r.timestamp)
                .Take(1000)
                .OrderBy(r => r.timestamp)
                .ToList();
        }

        private static int IncrementCounter(Dictionary<string, int> counters, string processName)
        {
            if (counters == null || string.IsNullOrWhiteSpace(processName))
            {
                return 0;
            }

            string key = processName.Trim();
            if (!counters.ContainsKey(key))
            {
                counters[key] = 0;
            }

            counters[key]++;
            return counters[key];
        }

        private static UserPreferencesData Normalize(UserPreferencesData data)
        {
            data ??= new UserPreferencesData();
            data.manualRemoveCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            data.confirmedCloseCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            data.cancelCloseCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            data.manualSkipCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            data.protectCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            data.forceCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            data.ignoredProtectionSuggestions ??= Array.Empty<string>();
            data.ignoredForceSuggestions ??= Array.Empty<string>();
            data.ignoredCloseSuggestions ??= Array.Empty<string>();
            return data;
        }
    }
}
