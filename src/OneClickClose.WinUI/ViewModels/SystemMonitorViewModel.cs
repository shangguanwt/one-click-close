using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    private Timer _refreshTimer;
    private readonly SystemMonitor _monitor;
    private CancellationTokenSource _cancellationTokenSource;

    [ObservableProperty]
    private long _totalMemoryMb;

    [ObservableProperty]
    private long _usedMemoryMb;

    [ObservableProperty]
    private float _cpuUsagePercent;

    [ObservableProperty]
    private int _processCount;

    [ObservableProperty]
    private ObservableCollection<ProcessResourceRecord> _topProcesses = new();

    [ObservableProperty]
    private bool _isMonitoring;

    // ── 新增指标 ──
    [ObservableProperty]
    private float _gpuUsagePercent;

    [ObservableProperty]
    private float _diskUsagePercent;

    [ObservableProperty]
    private float _networkMbps;

    [ObservableProperty]
    private float? _temperatureC;

    [ObservableProperty]
    private float? _batteryPercent;

    [ObservableProperty]
    private bool _batteryPresent;

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

    /// <summary>波形数据更新计数器，供 UI 绑定触发刷新。</summary>
    [ObservableProperty]
    private int _sparklineVersion;

    public SystemMonitorViewModel(DispatcherQueue dispatcher = null)
    {
        _dispatcher = dispatcher ?? DispatcherQueue.GetForCurrentThread();
        _monitor = new SystemMonitor();
    }

    /// <summary>开始监控（每 2 秒刷新）</summary>
    [RelayCommand]
    public void StartMonitoring()
    {
        if (_refreshTimer != null) return;
        IsMonitoring = true;
        _cancellationTokenSource = new CancellationTokenSource();
        _refreshTimer = new Timer(_ => _ = RefreshAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    /// <summary>停止监控</summary>
    [RelayCommand]
    public void StopMonitoring()
    {
        _cancellationTokenSource?.Cancel();
        _refreshTimer?.Dispose();
        _refreshTimer = null;
        IsMonitoring = false;
    }

    private async Task RefreshAsync()
    {
        if (_cancellationTokenSource?.Token.IsCancellationRequested ?? true)
            return;

        try
        {
            var snapshot = await _monitor.CaptureSnapshotAsync(_cancellationTokenSource.Token);
            snapshot = await _monitor.CaptureExtendedMetricsAsync(snapshot, _cancellationTokenSource.Token);

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
            // Ignore transient errors
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _refreshTimer?.Dispose();
    }
}

