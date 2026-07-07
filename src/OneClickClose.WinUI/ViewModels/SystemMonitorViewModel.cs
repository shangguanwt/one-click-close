using System;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using OneClickClose.Core;

namespace OneClickClose.WinUI.ViewModels;

/// <summary>
/// 系统资源监控 ViewModel — 每 2 秒采集一次 CPU/内存快照。
/// </summary>
public partial class SystemMonitorViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherQueue _dispatcher;
    private readonly SystemMonitor _monitor;
    private CancellationTokenSource _cancellationTokenSource;
    private Task _monitoringTask;

    private long _totalMemoryMb;
    private long _usedMemoryMb;
    private float _cpuUsagePercent;
    private int _processCount;
    private ObservableCollection<ProcessResourceRecord> _topProcesses = new();
    private bool _isMonitoring;
    private float _gpuUsagePercent;
    private float _diskUsagePercent;
    private float _networkMbps;
    private float? _temperatureC;
    private float? _cpuTemperatureC;
    private float? _gpuTemperatureC;
    private float? _motherboardTemperatureC;
    private string _temperatureSource = string.Empty;
    private string _temperatureUnavailableReason = string.Empty;
    private float? _batteryPercent;
    private bool _batteryPresent;
    private int _sparklineVersion;

    public long TotalMemoryMb { get => _totalMemoryMb; private set => SetProperty(ref _totalMemoryMb, value); }
    public long UsedMemoryMb { get => _usedMemoryMb; private set => SetProperty(ref _usedMemoryMb, value); }
    public float CpuUsagePercent { get => _cpuUsagePercent; private set => SetProperty(ref _cpuUsagePercent, value); }
    public int ProcessCount { get => _processCount; private set => SetProperty(ref _processCount, value); }
    public ObservableCollection<ProcessResourceRecord> TopProcesses { get => _topProcesses; private set => SetProperty(ref _topProcesses, value); }
    public bool IsMonitoring { get => _isMonitoring; private set => SetProperty(ref _isMonitoring, value); }
    public float GpuUsagePercent { get => _gpuUsagePercent; private set => SetProperty(ref _gpuUsagePercent, value); }
    public float DiskUsagePercent { get => _diskUsagePercent; private set => SetProperty(ref _diskUsagePercent, value); }
    public float NetworkMbps { get => _networkMbps; private set => SetProperty(ref _networkMbps, value); }
    public float? TemperatureC { get => _temperatureC; private set => SetProperty(ref _temperatureC, value); }
    public float? CpuTemperatureC { get => _cpuTemperatureC; private set => SetProperty(ref _cpuTemperatureC, value); }
    public float? GpuTemperatureC { get => _gpuTemperatureC; private set => SetProperty(ref _gpuTemperatureC, value); }
    public float? MotherboardTemperatureC { get => _motherboardTemperatureC; private set => SetProperty(ref _motherboardTemperatureC, value); }
    public string TemperatureSource { get => _temperatureSource; private set => SetProperty(ref _temperatureSource, value); }
    public string TemperatureUnavailableReason { get => _temperatureUnavailableReason; private set => SetProperty(ref _temperatureUnavailableReason, value); }
    public float? BatteryPercent { get => _batteryPercent; private set => SetProperty(ref _batteryPercent, value); }
    public bool BatteryPresent { get => _batteryPresent; private set => SetProperty(ref _batteryPresent, value); }
    public int SparklineVersion { get => _sparklineVersion; private set => SetProperty(ref _sparklineVersion, value); }

    // ── 波形图数据缓冲区（最近 30 个采样点） ──
    private readonly SparklineBuffer _cpuSparkline = new(30);
    private readonly SparklineBuffer _memorySparkline = new(30);
    private readonly SparklineBuffer _gpuSparkline = new(30);
    private readonly SparklineBuffer _diskSparkline = new(30);
    private readonly SparklineBuffer _networkSparkline = new(30);
    private readonly SparklineBuffer _temperatureSparkline = new(30);
    private readonly SparklineBuffer _batterySparkline = new(30);

    /// <summary>CPU 波形数据。</summary>
    public double[] CpuSparklineData => _cpuSparkline.Data;
    /// <summary>内存 波形数据。</summary>
    public double[] MemorySparklineData => _memorySparkline.Data;
    /// <summary>GPU 波形数据。</summary>
    public double[] GpuSparklineData => _gpuSparkline.Data;
    /// <summary>磁盘 波形数据。</summary>
    public double[] DiskSparklineData => _diskSparkline.Data;
    /// <summary>网络 波形数据。</summary>
    public double[] NetworkSparklineData => _networkSparkline.Data;
    /// <summary>温度 波形数据。</summary>
    public double[] TemperatureSparklineData => _temperatureSparkline.Data;
    /// <summary>电池 波形数据。</summary>
    public double[] BatterySparklineData => _batterySparkline.Data;

    public SystemMonitorViewModel(DispatcherQueue dispatcher = null)
    {
        _dispatcher = dispatcher ?? DispatcherQueue.GetForCurrentThread();
        _monitor = new SystemMonitor();
    }

    /// <summary>开始监控（每 2 秒刷新）</summary>
    [RelayCommand]
    public void StartMonitoring()
    {
        if (_monitoringTask != null) return;
        IsMonitoring = true;
        _cancellationTokenSource = new CancellationTokenSource();
        _monitoringTask = RunMonitoringLoopAsync(_cancellationTokenSource.Token);
    }

    /// <summary>停止监控</summary>
    [RelayCommand]
    public void StopMonitoring()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _monitoringTask = null;
        IsMonitoring = false;
    }

    private async Task RunMonitoringLoopAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            await RefreshAsync(token);
            while (await timer.WaitForNextTickAsync(token))
            {
                await RefreshAsync(token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on stop
        }
    }

    private async Task RefreshAsync(CancellationToken token)
    {
        try
        {
            var snapshot = await _monitor.CaptureSnapshotAsync(token);
            snapshot = await _monitor.CaptureExtendedMetricsAsync(snapshot, token);

            _dispatcher.TryEnqueue(() =>
            {
                TotalMemoryMb = snapshot.TotalMemoryMb;
                UsedMemoryMb = snapshot.UsedMemoryMb;
                CpuUsagePercent = snapshot.CpuUsagePercent;
                ProcessCount = snapshot.ProcessCount;

                GpuUsagePercent = snapshot.GpuUsagePercent;
                DiskUsagePercent = snapshot.DiskUsagePercent;
                NetworkMbps = snapshot.NetworkMbps;
                TemperatureC = snapshot.TemperatureC;
                CpuTemperatureC = snapshot.CpuTemperatureC;
                GpuTemperatureC = snapshot.GpuTemperatureC;
                MotherboardTemperatureC = snapshot.MotherboardTemperatureC;
                TemperatureSource = snapshot.TemperatureSource ?? string.Empty;
                TemperatureUnavailableReason = snapshot.TemperatureUnavailableReason ?? string.Empty;
                BatteryPercent = snapshot.BatteryPercent;
                BatteryPresent = snapshot.BatteryPresent;

                _cpuSparkline.Add(snapshot.CpuUsagePercent);
                double memoryPercent = snapshot.TotalMemoryMb > 0 ? (double)snapshot.UsedMemoryMb / snapshot.TotalMemoryMb * 100 : 0;
                _memorySparkline.Add(memoryPercent);
                _gpuSparkline.Add(snapshot.GpuUsagePercent);
                _diskSparkline.Add(snapshot.DiskUsagePercent);
                _networkSparkline.Add(snapshot.NetworkMbps);
                _temperatureSparkline.Add(snapshot.TemperatureC ?? 0);
                _batterySparkline.Add(snapshot.BatteryPercent ?? 0);
                SparklineVersion++;

                TopProcesses.Clear();
                if (snapshot.TopProcesses != null)
                {
                    foreach (var p in snapshot.TopProcesses)
                    {
                        TopProcesses.Add(p);
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Expected on stop
        }
        catch
        {
            // Ignore transient metric collection errors
        }
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}
