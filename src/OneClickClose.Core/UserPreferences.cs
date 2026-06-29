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

        private UserPreferencesStore(string dataDir, UserPreferencesData preferences, CleanupHistoryDocument history)
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
            if (string.IsNullOrWhiteSpace(processName))
            {
                return 0;
            }

            string key = processName.Trim();
            if (!preferences.manualRemoveCounts.ContainsKey(key))
            {
                preferences.manualRemoveCounts[key] = 0;
            }

            preferences.manualRemoveCounts[key]++;
            SavePreferences();
            return preferences.manualRemoveCounts[key];
        }

        public void RecordCleanup(CloseResult result)
        {
            if (result == null)
            {
                return;
            }

            HashSet<int> completed = new HashSet<int>();
            foreach (ProcessRecord item in result.GracefulClosed)
            {
                AddHistory(item, ProcessPlanner.ActionGraceful);
                completed.Add(item.Id);
            }

            foreach (ProcessRecord item in result.Forced)
            {
                AddHistory(item, ProcessPlanner.ActionForce);
                completed.Add(item.Id);
            }

            foreach (ProcessRecord item in result.Remaining)
            {
                if (!completed.Contains(item.Id))
                {
                    AddHistory(item, "仍在运行");
                    completed.Add(item.Id);
                }
            }

            foreach (ProcessRecord item in result.ReportOnly)
            {
                if (!completed.Contains(item.Id))
                {
                    AddHistory(item, ProcessPlanner.ActionReport);
                    completed.Add(item.Id);
                }
            }

            TrimHistory();
            SaveHistory();
        }

        public List<UserPreferenceSuggestion> BuildSuggestions(AppConfig config)
        {
            List<UserPreferenceSuggestion> suggestions = new List<UserPreferenceSuggestion>();
            HashSet<string> protectedSet = config.ProtectedSet();
            HashSet<string> forceSet = config.ForceSet();
            HashSet<string> ignoredProtect = JsonFileStore.MakeSet(preferences.ignoredProtectionSuggestions);
            HashSet<string> ignoredForce = JsonFileStore.MakeSet(preferences.ignoredForceSuggestions);

            foreach (KeyValuePair<string, int> entry in preferences.manualRemoveCounts.OrderByDescending(kv => kv.Value))
            {
                if (entry.Value >= 3 && !protectedSet.Contains(entry.Key) && !ignoredProtect.Contains(entry.Key))
                {
                    suggestions.Add(new UserPreferenceSuggestion
                    {
                        Type = "保护名单",
                        ProcessName = entry.Key,
                        Count = entry.Value,
                        Reason = "已从本次清理列表移除 " + entry.Value + " 次"
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

                bool allForced = recent.All(r => string.Equals(r.action, ProcessPlanner.ActionForce, StringComparison.Ordinal));
                bool allRemaining = recent.All(r => string.Equals(r.action, "仍在运行", StringComparison.Ordinal));
                if (allForced || allRemaining)
                {
                    suggestions.Add(new UserPreferenceSuggestion
                    {
                        Type = "强制清理名单",
                        ProcessName = group.Key,
                        Count = recent.Count,
                        Reason = allForced ? "最近 3 次都被强制清理" : "最近 3 次清理后仍在运行"
                    });
                }
            }

            return suggestions
                .OrderBy(s => s.Type)
                .ThenBy(s => s.ProcessName)
                .ToList();
        }

        public void IgnoreSuggestion(UserPreferenceSuggestion suggestion)
        {
            if (suggestion == null)
            {
                return;
            }

            if (string.Equals(suggestion.Type, "保护名单", StringComparison.Ordinal))
            {
                preferences.ignoredProtectionSuggestions = JsonFileStore.AddToArray(preferences.ignoredProtectionSuggestions, suggestion.ProcessName);
            }
            else if (string.Equals(suggestion.Type, "强制清理名单", StringComparison.Ordinal))
            {
                preferences.ignoredForceSuggestions = JsonFileStore.AddToArray(preferences.ignoredForceSuggestions, suggestion.ProcessName);
            }

            SavePreferences();
        }

        public void SavePreferences()
        {
            JsonFileStore.WriteJson(prefsPath, preferences);
        }

        public void SaveHistory()
        {
            JsonFileStore.WriteJson(historyPath, history);
        }

        private void AddHistory(ProcessRecord item, string action)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ProcessName))
            {
                return;
            }

            history.records.Add(new CleanupHistoryRecord
            {
                processName = item.ProcessName,
                action = action,
                timestamp = DateTime.Now.ToString("o")
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

        private static UserPreferencesData Normalize(UserPreferencesData data)
        {
            if (data == null)
            {
                data = new UserPreferencesData();
            }

            if (data.manualRemoveCounts == null)
            {
                data.manualRemoveCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            if (data.ignoredProtectionSuggestions == null)
            {
                data.ignoredProtectionSuggestions = new string[0];
            }

            if (data.ignoredForceSuggestions == null)
            {
                data.ignoredForceSuggestions = new string[0];
            }

            return data;
        }

    }
}
