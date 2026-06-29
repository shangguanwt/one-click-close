using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using OneClickClose.Core;

namespace OneClickClose.WinUI;

/// <summary>
/// Application shared state: scan results, execution status, and user preferences.
/// Implements INotifyPropertyChanged for data-binding via Instance singleton.
/// Static properties forward to Instance for backward compatibility.
/// </summary>
public class AppState : INotifyPropertyChanged
{
    private static AppState _instance;
    public static AppState Instance => _instance ??= new AppState();

    public event PropertyChangedEventHandler PropertyChanged;

    // ── Backing fields ──

    private ClosePlan _currentPlan;
    private DateTime _lastScanTime;
    private bool _isExecuting;
    private List<ProcessGroupRow> _candidateRows;
    private List<ProcessGroupRow> _protectedRows;
    private CloseResult _lastResult;
    private UserPreferencesStore _preferences;

    private readonly ObservableCollection<string> _logLines = new();

    // ── Instance properties (bindable via Instance) ──

    public ObservableCollection<string> LogLinesInstance => _logLines;

    public UserPreferencesStore PreferencesInstance
    {
        get => _preferences ??= UserPreferencesStore.Load();
        set => _preferences = value;
    }

    public string ConfigPathInstance { get; } = GetConfigPath();

    // ── Static backward-compatible accessors ──

    public static ClosePlan CurrentPlan
    {
        get => Instance._currentPlan;
        set => Instance.SetCurrentPlan(value);
    }

    public static DateTime LastScanTime
    {
        get => Instance._lastScanTime;
        set => Instance.SetLastScanTime(value);
    }

    public static bool HasPlan => Instance._currentPlan != null;

    public static List<ProcessGroupRow> CandidateRows
    {
        get => Instance._candidateRows;
        set => Instance.SetCandidateRows(value);
    }

    public static List<ProcessGroupRow> ProtectedRows
    {
        get => Instance._protectedRows;
        set => Instance.SetProtectedRows(value);
    }

    public static CloseResult LastResult
    {
        get => Instance._lastResult;
        set => Instance.SetLastResult(value);
    }

    public static bool IsExecuting
    {
        get => Instance._isExecuting;
        set => Instance.SetIsExecuting(value);
    }

    public static ObservableCollection<string> LogLines => Instance._logLines;

    public static UserPreferencesStore Preferences
    {
        get => Instance.PreferencesInstance;
        set => Instance.PreferencesInstance = value;
    }

    public static string ConfigPath => Instance.ConfigPathInstance;

    // ── Instance setters that raise PropertyChanged ──

    private void SetCurrentPlan(ClosePlan value) => SetProperty(ref _currentPlan, value);
    private void SetLastScanTime(DateTime value) => SetProperty(ref _lastScanTime, value);
    private void SetIsExecuting(bool value) => SetProperty(ref _isExecuting, value);
    private void SetCandidateRows(List<ProcessGroupRow> value) => SetProperty(ref _candidateRows, value);
    private void SetProtectedRows(List<ProcessGroupRow> value) => SetProperty(ref _protectedRows, value);
    private void SetLastResult(CloseResult value) => SetProperty(ref _lastResult, value);

    // ── Operations ──

    public static void Scan(string configPath)
    {
        var plan = ProcessPlanner.GetClosePlan(configPath);
        CurrentPlan = plan;
        LastScanTime = DateTime.Now;
        CandidateRows = ProcessPlanner.GroupRows(plan.Candidates ?? new List<ProcessRecord>());
        ProtectedRows = ProcessPlanner.GroupRows(plan.Protected ?? new List<ProcessRecord>());
    }

    public static async Task ScanAsync(string configPath = null)
    {
        configPath = configPath ?? Instance.ConfigPathInstance;
        var plan = await ProcessPlanner.GetClosePlanAsync(configPath);
        CurrentPlan = plan;
        LastScanTime = DateTime.Now;
        CandidateRows = ProcessPlanner.GroupRows(plan.Candidates ?? new List<ProcessRecord>());
        ProtectedRows = ProcessPlanner.GroupRows(plan.Protected ?? new List<ProcessRecord>());
    }

    public static void LoadPreferences()
    {
        var _ = Instance.PreferencesInstance;
    }

    /// <summary>日志保留上限，超出后丢弃最旧行，避免长会话无界增长。</summary>
    public const int MaxLogLines = 500;

    public static void AddLog(string line)
    {
        var logs = Instance._logLines;
        logs.Add(line);
        // 触发 Remove 事件，订阅页面据此同步移除其镜像集合的最旧行
        while (logs.Count > MaxLogLines)
        {
            logs.RemoveAt(0);
        }
    }

    public static void ClearLog()
    {
        Instance._logLines.Clear();
    }

    // ── INotifyPropertyChanged ──

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private static string GetConfigPath()
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        return System.IO.Path.Combine(appDir, "close-user-apps.config.json");
    }
}
