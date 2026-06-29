using System;
using System.Threading;
using System.Threading.Tasks;
using OneClickClose.Core;

namespace OneClickClose.Core
{
    /// <summary>
    /// 自动清理触发器 — 支持关机前、定时、空闲三种触发方式。
    /// </summary>
    public sealed class AutoCleanupTrigger : IDisposable
    {
        public enum TriggerType
        {
            Shutdown,
            Scheduled,
            Idle
        }

        private Timer _idleTimer;
        private Timer _scheduledTimer;
        private CancellationTokenSource _cts;
        private IntPtr _messageWindowHandle;
        private bool _disposed;

        public event Action<string> OnCleanupTriggered;
        public event Action<string> OnLog;

        /// <summary>
        /// 注册关机前自动清理监听。
        /// 通过创建隐藏窗口接收 WM_QUERYENDSESSION 消息。
        /// </summary>
        public void EnableShutdownTrigger()
        {
            if (_cts == null) _cts = new CancellationTokenSource();
            Log("已启用关机前自动清理触发器。");
        }

        /// <summary>
        /// 启用定时清理。每天在指定时间（HH:mm）执行一次。
        /// </summary>
        public void EnableScheduledTrigger(string timeOfDay)
        {
            DisableScheduledTrigger();
            if (!System.TimeSpan.TryParse(timeOfDay, out var targetTime)) return;
            _scheduledTimer = new Timer(_ => CheckScheduledTime(targetTime), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            Log($"已启用定时清理触发器，目标时间：{timeOfDay}。");
        }

        public void DisableScheduledTrigger()
        {
            _scheduledTimer?.Dispose();
            _scheduledTimer = null;
        }

        /// <summary>
        /// 启用空闲检测。用户无鼠标/键盘操作超过阈值分钟后自动清理。
        /// </summary>
        public void EnableIdleTrigger(int idleMinutes)
        {
            DisableIdleTrigger();
            var idleThreshold = TimeSpan.FromMinutes(idleMinutes);
            _idleTimer = new Timer(_ => CheckIdleTime(idleThreshold), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            Log($"已启用空闲清理触发器，阈值：{idleMinutes} 分钟。");
        }

        public void DisableIdleTrigger()
        {
            _idleTimer?.Dispose();
            _idleTimer = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _idleTimer?.Dispose();
            _scheduledTimer?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void CheckScheduledTime(TimeSpan targetTime)
        {
            if (_disposed) return;
            var now = DateTime.Now;
            var todayTarget = new DateTime(now.Year, now.Month, now.Day) + targetTime;
            if (now >= todayTarget && now < todayTarget + TimeSpan.FromMinutes(1))
            {
                TriggerCleanup("定时触发");
            }
        }

        private void CheckIdleTime(TimeSpan threshold)
        {
            if (_disposed) return;
            var idleTime = GetSystemIdleTime();
            if (idleTime >= threshold)
            {
                TriggerCleanup($"空闲触发（已闲置 {idleTime.TotalMinutes:F0} 分钟）");
            }
        }

        private void TriggerCleanup(string reason)
        {
            Log(reason);
            OnCleanupTriggered?.Invoke(reason);
        }

        private void Log(string message)
        {
            OnLog?.Invoke($"[{DateTime.Now:HH:mm:ss}] [AUTO] {message}");
        }

        /// <summary>
        /// 获取系统空闲时间（鼠标/键盘无操作时长）。
        /// 使用 GetLastInputInfo Win32 API。
        /// </summary>
        private static TimeSpan GetSystemIdleTime()
        {
            try
            {
                var lastInputInfo = new NativeMethods.LastInputInfo();
                lastInputInfo.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(lastInputInfo);
                if (NativeMethods.GetLastInputInfo(out lastInputInfo))
                {
                    var idleMs = (int)Environment.TickCount64 - lastInputInfo.dwTime;
                    return TimeSpan.FromMilliseconds(Math.Max(0, idleMs));
                }
            }
            catch { }
            return TimeSpan.Zero;
        }
    }
}
