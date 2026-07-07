using System;
using System.Collections.Generic;

namespace OneClickClose.Core
{
    public sealed class ProcessRecord
    {
        public int Id { get; set; }
        public string ProcessName { get; set; }
        public string MainWindowTitle { get; set; }
        public string Path { get; set; }
        public bool HasWindow { get; set; }
        public int ParentProcessId { get; set; }
        public string ParentProcessName { get; set; }
        public bool IsUserLaunched { get; set; }
        public bool IsUserPath { get; set; }
        public bool IsSystemPath { get; set; }
        public bool IsAutoDetected { get; set; }
        public long MemoryMb { get; set; }
        public int RiskScore { get; set; }
        public bool IsHighRisk { get; set; }
        public string Action { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
        public string UsageHint { get; set; }
        public string HabitHint { get; set; }
    }

    public sealed class ProcessGroupRow
    {
        public string Process { get; set; }
        public int Count { get; set; }
        public string Action { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public string Path { get; set; }
        public bool HasWindow { get; set; }
        public long MemoryMb { get; set; }
        public int RiskScore { get; set; }
        public bool IsHighRisk { get; set; }
        public string AppKey { get; set; }
        public string UsageHint { get; set; }
        public string HabitHint { get; set; }
        public List<ProcessRecord> Children { get; set; }

        public ProcessGroupRow()
        {
            Children = new List<ProcessRecord>();
        }
    }

    public enum ProcessGroupFilter
    {
        All,
        Closable,
        Protected,
        Skipped,
        HighRisk
    }

    public sealed class ClosePlan
    {
        public AppConfig Config { get; set; }
        public List<ProcessRecord> Candidates { get; set; }
        public List<ProcessRecord> Protected { get; set; }
        public List<ProcessRecord> Skipped { get; set; }
    }

    public sealed class CloseResult
    {
        public List<ProcessRecord> GracefulClosed { get; private set; }
        public List<ProcessRecord> Forced { get; private set; }
        public List<ProcessRecord> ReportOnly { get; private set; }
        public List<ProcessRecord> Remaining { get; private set; }

        public CloseResult()
        {
            GracefulClosed = new List<ProcessRecord>();
            Forced = new List<ProcessRecord>();
            ReportOnly = new List<ProcessRecord>();
            Remaining = new List<ProcessRecord>();
        }
    }

    // ── User preferences data models (moved from UserPreferences.cs) ──

    public sealed class CleanupHistoryRecord
    {
        public string processName { get; set; }
        public string action { get; set; }
        public string timestamp { get; set; }
        public string path { get; set; }
        public int processId { get; set; }
        public string decision { get; set; }
        public long memoryMb { get; set; }
    }

    public sealed class CleanupHistoryDocument
    {
        public List<CleanupHistoryRecord> records { get; set; }

        public CleanupHistoryDocument()
        {
            records = new List<CleanupHistoryRecord>();
        }
    }

    public sealed class UserPreferencesData
    {
        public Dictionary<string, int> manualRemoveCounts { get; set; }
        public Dictionary<string, int> confirmedCloseCounts { get; set; }
        public Dictionary<string, int> cancelCloseCounts { get; set; }
        public Dictionary<string, int> manualSkipCounts { get; set; }
        public Dictionary<string, int> protectCounts { get; set; }
        public Dictionary<string, int> forceCounts { get; set; }
        public string[] ignoredProtectionSuggestions { get; set; }
        public string[] ignoredForceSuggestions { get; set; }
        public string[] ignoredCloseSuggestions { get; set; }

        public UserPreferencesData()
        {
            manualRemoveCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            confirmedCloseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            cancelCloseCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            manualSkipCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            protectCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            forceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            ignoredProtectionSuggestions = new string[0];
            ignoredForceSuggestions = new string[0];
            ignoredCloseSuggestions = new string[0];
        }
    }

    public sealed class UserPreferenceSuggestion
    {
        public string Type { get; set; }
        public string ProcessName { get; set; }
        public string Reason { get; set; }
        public int Count { get; set; }
        public int Priority { get; set; }
    }

    public sealed class HardwareTemperatureReading
    {
        public float? CpuTemperatureC { get; set; }
        public float? GpuTemperatureC { get; set; }
        public float? MotherboardTemperatureC { get; set; }
        public string Source { get; set; }
        public string UnavailableReason { get; set; }
        public bool HasAnyTemperature =>
            CpuTemperatureC.HasValue || GpuTemperatureC.HasValue || MotherboardTemperatureC.HasValue;
    }
}
