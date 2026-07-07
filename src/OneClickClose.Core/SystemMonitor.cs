using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OneClickClose.Core;

/// <summary>
/// 系统资源快照，记录某一时刻的系统内存、CPU 及进程信息。
/// </summary>
public class SystemSnapshot
{
    /// <summary>
    /// 系统总物理内存（MB）。
    /// </summary>
    public long TotalMemoryMb { get; set; }

    /// <summary>
    /// 所有进程已使用的物理内存总和（MB）。
    /// </summary>
    public long UsedMemoryMb { get; set; }

    /// <summary>
    /// 当前 CPU 总体使用率（0 ~ 100）。首次采样时返回 0。
    /// </summary>
    public float CpuUsagePercent { get; set; }

    /// <summary>
    /// 当前系统中运行的进程总数。
    /// </summary>
    public int ProcessCount { get; set; }

    /// <summary>
    /// 按内存占用降序排列的前 10 个进程资源记录。
    /// </summary>
    public List<ProcessResourceRecord> TopProcesses { get; set; } = [];

    /// <summary>GPU 利用率（0~100），无法获取时为 0。</summary>
    public float GpuUsagePercent { get; set; }

    /// <summary>磁盘活跃率（0~100）。</summary>
    public float DiskUsagePercent { get; set; }

    /// <summary>网络总吞吐（Mbps）。</summary>
    public float NetworkMbps { get; set; }

    /// <summary>系统温度（摄氏度），无法获取时为 null。</summary>
    public float? TemperatureC { get; set; }

    public float? CpuTemperatureC { get; set; }

    public float? GpuTemperatureC { get; set; }

    public float? MotherboardTemperatureC { get; set; }

    public string TemperatureSource { get; set; }

    public string TemperatureUnavailableReason { get; set; }

    /// <summary>电池剩余电量百分比，无电池时为 null。</summary>
    public float? BatteryPercent { get; set; }

    /// <summary>是否存在电池。</summary>
    public bool BatteryPresent { get; set; }
}

/// <summary>
/// 单个进程的资源使用记录。
/// </summary>
public class ProcessResourceRecord
{
    /// <summary>
    /// 进程 ID。
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 进程名称。
    /// </summary>
    public string ProcessName { get; set; } = string.Empty;

    /// <summary>
    /// 该进程占用的物理内存（MB）。
    /// </summary>
    public long MemoryMb { get; set; }

    /// <summary>
    /// 该进程的 CPU 使用率（0 ~ 100）。
    /// </summary>
    public float CpuPercent { get; set; }

    /// <summary>
    /// 进程可执行文件路径。
    /// </summary>
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// 系统资源监控器，提供轻量级的 CPU / 内存 / 进程快照采集。
/// </summary>
public class SystemMonitor
{
    private readonly IHardwareTemperatureProvider _temperatureProvider;
    private readonly Func<float?> _wmiTemperatureReader;

    // ---------- CPU 两次采样所需的前一次状态 ----------
    private DateTime _previousSampleTime;
    private TimeSpan _previousTotalProcessorTime;
    private bool _firstSample = true;

    // ---------- 进程级 CPU 采样缓存（PID → 上一次 TotalProcessorTime） ----------
    private Dictionary<int, TimeSpan> _previousProcessCpuTime = [];

    // ---------- 网络两次采样所需的前一次状态 ----------
    private long _previousNetworkBytes;
    private DateTime _previousNetworkTime;
    private bool _firstNetworkSample = true;

    // ---------- GPU 性能计数器缓存 ----------
    private List<System.Diagnostics.PerformanceCounter> _gpuCounters;
    private bool _gpuCountersInitialized;

    // ---------- 线程安全锁 ----------
    private readonly object _lock = new();

    public SystemMonitor()
        : this(new LibreHardwareTemperatureProvider(), GetTemperatureC)
    {
    }

    internal SystemMonitor(IHardwareTemperatureProvider temperatureProvider)
        : this(temperatureProvider, GetTemperatureC)
    {
    }

    internal SystemMonitor(IHardwareTemperatureProvider temperatureProvider, Func<float?> wmiTemperatureReader)
    {
        _temperatureProvider = temperatureProvider;
        _wmiTemperatureReader = wmiTemperatureReader ?? GetTemperatureC;
    }

    /// <summary>
    /// 同步采集当前系统资源快照。保留给 WinUI 壳和兼容调用路径使用。
    /// </summary>
    public SystemSnapshot CaptureSnapshot()
    {
        return CaptureSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// 异步采集当前系统资源快照。
    /// </summary>
    public async Task<SystemSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            lock (_lock)
            {
                var snapshot = new SystemSnapshot();
                var processes = Process.GetProcesses();
                snapshot.ProcessCount = processes.Length;

                // —— 内存 ——
                snapshot.TotalMemoryMb = GetTotalPhysicalMemoryMb();
                snapshot.UsedMemoryMb = SumWorkingSetMb(processes);

                // —— CPU（两次采样） ——
                var now = DateTime.UtcNow;
                var totalProcessorTime = SumTotalProcessorTime(processes);

                var elapsed = _firstSample ? TimeSpan.Zero : now - _previousSampleTime;

                if (_firstSample)
                {
                    snapshot.CpuUsagePercent = 0f;
                    _previousSampleTime = now;
                    _previousTotalProcessorTime = totalProcessorTime;
                    CacheProcessCpuTime(processes);
                    _firstSample = false;
                }
                else
                {
                    if (elapsed.TotalMilliseconds > 0)
                    {
                        var usedDelta = (totalProcessorTime - _previousTotalProcessorTime).TotalMilliseconds;
                        var totalAvailable = elapsed.TotalMilliseconds * Environment.ProcessorCount;
                        snapshot.CpuUsagePercent = Math.Clamp(
                            (float)(usedDelta / totalAvailable * 100.0), 0f, 100f);
                    }
                    else
                    {
                        snapshot.CpuUsagePercent = 0f;
                    }

                    _previousSampleTime = now;
                    _previousTotalProcessorTime = totalProcessorTime;
                    CacheProcessCpuTime(processes);
                }

                // —— Top 进程 ——
                snapshot.TopProcesses = BuildTopProcesses(processes, elapsed);

                // —— GPU（同步，性能计数器本地） ——
                snapshot.GpuUsagePercent = GetGpuUsage();

                return snapshot;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// 异步获取扩展指标（磁盘/网络/温度/电池）。在 CaptureSnapshotAsync 后调用。
    /// </summary>
    public async Task<SystemSnapshot> CaptureExtendedMetricsAsync(SystemSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var tasks = new Task[]
        {
            Task.Run(() => { snapshot.DiskUsagePercent = GetDiskUsage(); }, cancellationToken),
            Task.Run(() => { snapshot.NetworkMbps = GetNetworkMbps(DateTime.UtcNow); }, cancellationToken),
            Task.Run(() => { ApplyTemperatureReading(snapshot); }, cancellationToken),
            Task.Run(() => { snapshot.BatteryPercent = GetBatteryPercent(); }, cancellationToken)
        };

        await Task.WhenAll(tasks);
        snapshot.BatteryPresent = snapshot.BatteryPercent.HasValue;
        return snapshot;
    }

    // ==================== 内部辅助方法 ====================

    /// <summary>
    /// 获取系统总物理内存（MB），优先通过 WMI 查询，失败时回退到 GC 估算值。
    /// </summary>
    private static long GetTotalPhysicalMemoryMb()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var kb = Convert.ToUInt64(obj["TotalVisibleMemorySize"]);
                return (long)(kb / 1024);
            }
        }
        catch
        {
            // WMI 不可用时回退到 GC 报告值
        }

        try
        {
            var info = GC.GetGCMemoryInfo();
            return (long)(info.TotalAvailableMemoryBytes / 1024 / 1024);
        }
        catch
        {
            // 最终回退
        }

        return 16384L; // 默认 16 GB
    }

    /// <summary>
    /// 对所有进程的 WorkingSet64 求和，转换为 MB。
    /// </summary>
    private static long SumWorkingSetMb(Process[] processes)
    {
        long totalBytes = 0;
        foreach (var p in processes)
        {
            try { totalBytes += p.WorkingSet64; }
            catch { /* 已终止进程忽略 */ }
        }
        return totalBytes / 1024 / 1024;
    }

    /// <summary>
    /// 对所有进程的 TotalProcessorTime 求和。
    /// </summary>
    private static TimeSpan SumTotalProcessorTime(Process[] processes)
    {
        var total = TimeSpan.Zero;
        foreach (var p in processes)
        {
            try { total += p.TotalProcessorTime; }
            catch { /* 忽略 */ }
        }
        return total;
    }

    /// <summary>
    /// 缓存每个进程当前的 TotalProcessorTime，供下一次采样计算进程级 CPU 使用率。
    /// </summary>
    private void CacheProcessCpuTime(Process[] processes)
    {
        var cache = new Dictionary<int, TimeSpan>(processes.Length);
        foreach (var p in processes)
        {
            try { cache[p.Id] = p.TotalProcessorTime; }
            catch { /* 忽略 */ }
        }
        _previousProcessCpuTime = cache;
    }

    /// <summary>
    /// 构建按内存降序排列的前 10 个进程资源记录。
    /// </summary>
    private List<ProcessResourceRecord> BuildTopProcesses(Process[] processes, TimeSpan elapsed)
    {
        var list = new List<ProcessResourceRecord>(processes.Length);

        foreach (var p in processes)
        {
            try
            {
                var record = new ProcessResourceRecord
                {
                    Id = p.Id,
                    ProcessName = p.ProcessName,
                    MemoryMb = p.WorkingSet64 / 1024 / 1024,
                    CpuPercent = 0f,
                    Path = string.Empty,
                };

                try { record.Path = p.MainModule?.FileName ?? string.Empty; }
                catch { record.Path = string.Empty; /* 无法访问时留空 */ }

                // 计算进程级 CPU 使用率
                if (elapsed.TotalMilliseconds > 0
                    && _previousProcessCpuTime.TryGetValue(p.Id, out var prevCpu))
                {
                    try
                    {
                        var delta = (p.TotalProcessorTime - prevCpu).TotalMilliseconds;
                        var available = elapsed.TotalMilliseconds * Environment.ProcessorCount;
                        record.CpuPercent = Math.Clamp((float)(delta / available * 100.0), 0f, 100f);
                    }
                    catch { record.CpuPercent = 0f; }
                }

                list.Add(record);
            }
            catch
            {
                // 已终止或无权限的进程直接跳过
            }
        }

        // 按内存降序，取前 10
        list.Sort((a, b) => b.MemoryMb.CompareTo(a.MemoryMb));
        if (list.Count > 10)
            list.RemoveRange(10, list.Count - 10);

        return list;
    }

    // ==================== 新增指标采集方法 ====================

    /// <summary>
    /// 获取 GPU 利用率（0~100），通过性能计数器查询。
    /// </summary>
    private float GetGpuUsage()
    {
        try
        {
            if (!_gpuCountersInitialized)
            {
                _gpuCountersInitialized = true;
                var category = new System.Diagnostics.PerformanceCounterCategory("GPU Engine");
                var instanceNames = category.GetInstanceNames();
                var counters = new List<System.Diagnostics.PerformanceCounter>();
                foreach (var inst in instanceNames)
                {
                    if (inst.Contains("engtype_3D"))
                    {
                        counters.Add(new System.Diagnostics.PerformanceCounter("GPU Engine", "Utilization Percentage", inst));
                    }
                }
                _gpuCounters = counters.Count > 0 ? counters : null;
            }

            if (_gpuCounters == null || _gpuCounters.Count == 0)
                return 0f;

            float sum = 0f;
            foreach (var c in _gpuCounters)
            {
                try { sum += c.NextValue(); }
                catch { /* 忽略已失效的计数器 */ }
            }
            return Math.Clamp(sum, 0f, 100f);
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>
    /// 获取磁盘活跃率（0~100），通过 WMI 查询。
    /// </summary>
    private static float GetDiskUsage()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT PercentDiskTime FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name='_Total'");
            foreach (var obj in searcher.Get())
            {
                return Convert.ToSingle(obj["PercentDiskTime"]);
            }
        }
        catch
        {
            // WMI 不可用时返回 0
        }
        return 0f;
    }

    /// <summary>
    /// 获取网络总吞吐（Mbps），通过 NetworkInterface 两次采样 delta 计算。
    /// </summary>
    private float GetNetworkMbps(DateTime now)
    {
        try
        {
            long totalBytes = 0;
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    continue;
                try
                {
                    var stats = ni.GetIPv4Statistics();
                    totalBytes += stats.BytesSent + stats.BytesReceived;
                }
                catch { /* 忽略无法访问的接口 */ }
            }

            if (_firstNetworkSample)
            {
                _firstNetworkSample = false;
                _previousNetworkBytes = totalBytes;
                _previousNetworkTime = now;
                return 0f;
            }

            var elapsed = now - _previousNetworkTime;
            if (elapsed.TotalSeconds <= 0)
                return 0f;

            var deltaBytes = totalBytes - _previousNetworkBytes;
            _previousNetworkBytes = totalBytes;
            _previousNetworkTime = now;

            // bytes/sec → Mbps
            var bytesPerSec = deltaBytes / elapsed.TotalSeconds;
            return (float)(bytesPerSec * 8 / 1_000_000);
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>
    /// 获取系统温度（摄氏度），通过 WMI ACPI 热区查询。不是所有硬件都支持。
    /// </summary>
    private static float? GetTemperatureC()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "root\\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            foreach (var obj in searcher.Get())
            {
                // 单位：十分之一开尔文
                var tenthsKelvin = Convert.ToSingle(obj["CurrentTemperature"]);
                return tenthsKelvin / 10f - 273.15f;
            }
        }
        catch
        {
            // 不支持时返回 null
        }
        return null;
    }

    private void ApplyTemperatureReading(SystemSnapshot snapshot)
    {
        HardwareTemperatureReading reading = null;
        try
        {
            reading = _temperatureProvider?.ReadTemperatures();
        }
        catch (Exception ex)
        {
            reading = new HardwareTemperatureReading
            {
                Source = "LibreHardwareMonitor",
                UnavailableReason = ex.Message
            };
        }

        if (reading != null && reading.HasAnyTemperature)
        {
            snapshot.CpuTemperatureC = reading.CpuTemperatureC;
            snapshot.GpuTemperatureC = reading.GpuTemperatureC;
            snapshot.MotherboardTemperatureC = reading.MotherboardTemperatureC;
            snapshot.TemperatureC = reading.CpuTemperatureC
                ?? reading.GpuTemperatureC
                ?? reading.MotherboardTemperatureC;
            snapshot.TemperatureSource = string.IsNullOrWhiteSpace(reading.Source)
                ? "LibreHardwareMonitor"
                : reading.Source;
            snapshot.TemperatureUnavailableReason = null;
            return;
        }

        float? wmiTemperature = _wmiTemperatureReader();
        if (wmiTemperature.HasValue)
        {
            snapshot.CpuTemperatureC = wmiTemperature;
            snapshot.TemperatureC = wmiTemperature;
            snapshot.TemperatureSource = "WMI";
            snapshot.TemperatureUnavailableReason = null;
            return;
        }

        snapshot.TemperatureC = null;
        snapshot.TemperatureSource = reading?.Source ?? "Unavailable";
        snapshot.TemperatureUnavailableReason = !string.IsNullOrWhiteSpace(reading?.UnavailableReason)
            ? reading.UnavailableReason
            : "未检测到温度传感器";
    }

    /// <summary>
    /// 获取电池剩余电量百分比，通过 WMI 查询。台式机无电池时返回 null。
    /// </summary>
    private static float? GetBatteryPercent()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT EstimatedChargeRemaining FROM Win32_Battery");
            foreach (var obj in searcher.Get())
            {
                return Convert.ToSingle(obj["EstimatedChargeRemaining"]);
            }
        }
        catch
        {
            // 无电池或 WMI 不可用
        }
        return null;
    }
}
