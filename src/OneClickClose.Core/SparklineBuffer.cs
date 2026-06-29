using System;
using System.Collections.Generic;

namespace OneClickClose.Core;

/// <summary>
/// 环形缓冲区，存储最近 N 个采样值供波形图（sparkline）使用。
/// </summary>
public class SparklineBuffer
{
    private readonly double[] _buffer;
    private int _head;
    private int _count;

    // ── 缓存：避免每次读 Data/MaxValue 都重新分配/扫描 ──
    private double[] _cachedData;
    private double _cachedMax;
    private bool _dirty = true;

    /// <summary>缓冲区容量。</summary>
    public int Capacity { get; }

    /// <summary>当前已存入的采样数（未达容量时 < Capacity）。</summary>
    public int Count => _count;

    /// <summary>
    /// 创建指定容量的环形缓冲区。
    /// </summary>
    public SparklineBuffer(int capacity = 30)
    {
        Capacity = capacity;
        _buffer = new double[capacity];
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// 追加一个采样值。缓冲区满后覆盖最旧值。
    /// </summary>
    public void Add(double value)
    {
        _buffer[_head] = value;
        _head = (_head + 1) % Capacity;
        if (_count < Capacity) _count++;
        _dirty = true;
    }

    /// <summary>
    /// 清空缓冲区。
    /// </summary>
    public void Clear()
    {
        _head = 0;
        _count = 0;
        _dirty = true;
    }

    /// <summary>
    /// 重建缓存的有序数组与最大值（仅在脏时调用，单次遍历同时算出两者）。
    /// 脏时分配新数组，确保消费方（DependencyProperty）能检测到引用变化触发重绘；
    /// 未变更时复用，同一采样内重复读取零分配。
    /// </summary>
    private void RebuildCache()
    {
        var result = new double[_count];
        double max = double.MinValue;
        int start = (_head - _count + Capacity) % Capacity;
        for (int i = 0; i < _count; i++)
        {
            double v = _buffer[(start + i) % Capacity];
            result[i] = v;
            if (v > max) max = v;
        }
        _cachedData = result;
        _cachedMax = _count == 0 ? 0 : (max > 0 ? max : 1);
        _dirty = false;
    }

    /// <summary>
    /// 以时间顺序返回当前缓冲区中的所有值（最旧→最新）。
    /// 返回缓存数组，未变更时零分配。
    /// </summary>
    public double[] Data
    {
        get
        {
            if (_dirty) RebuildCache();
            return _cachedData;
        }
    }

    /// <summary>
    /// 返回缓冲区中的最大值，用于 Y 轴归一化。空缓冲区返回 0。
    /// </summary>
    public double MaxValue
    {
        get
        {
            if (_dirty) RebuildCache();
            return _cachedMax;
        }
    }
}
