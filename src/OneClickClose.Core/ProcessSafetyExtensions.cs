using System;
using System.Diagnostics;

namespace OneClickClose.Core
{
    /// <summary>
    /// Safe accessor extension methods on <see cref="Process"/> that never throw.
    /// Extracted from ProcessPlanner to allow reuse across planning and execution paths.
    /// </summary>
    public static class ProcessSafetyExtensions
    {
        public static int SafeId(this Process process)
        {
            try
            {
                return process.Id;
            }
            catch
            {
                return 0;
            }
        }

        public static string SafeName(this Process process)
        {
            try
            {
                return process.ProcessName ?? "";
            }
            catch
            {
                return "";
            }
        }

        public static string SafeWindowTitle(this Process process)
        {
            try
            {
                return process.MainWindowTitle ?? "";
            }
            catch
            {
                return "";
            }
        }

        public static string SafePath(this Process process)
        {
            try
            {
                return process.MainModule == null ? "" : (process.MainModule.FileName ?? "");
            }
            catch
            {
                return "";
            }
        }

        public static long SafeWorkingSetMb(this Process process)
        {
            try
            {
                return Math.Max(0, process.WorkingSet64 / 1024 / 1024);
            }
            catch
            {
                return 0;
            }
        }
    }
}
