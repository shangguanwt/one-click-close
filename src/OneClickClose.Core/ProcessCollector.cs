using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace OneClickClose.Core
{
    /// <summary>
    /// WMI and Win32 process enumeration utilities extracted from ProcessPlanner.
    /// Handles parent-process discovery, visible-window enumeration, and message sending.
    /// </summary>
    internal static class ProcessCollector
    {
        internal static HashSet<int> GetExcludedProcessIds()
        {
            HashSet<int> ids = new HashSet<int>();
            ids.Add(Process.GetCurrentProcess().Id);
            int parent = GetParentProcessId(Process.GetCurrentProcess().Id);
            if (parent > 0)
            {
                ids.Add(parent);
            }
            return ids;
        }

        internal static int GetParentProcessId(int pid)
        {
            // Fast path: NtQueryInformationProcess (no WMI overhead)
            try
            {
                using (Process process = Process.GetProcessById(pid))
                {
                    NativeMethods.NtProcessBasicInformation pbi;
                    int retLen;
                    int status = NativeMethods.NtQueryInformationProcess(
                        process.Handle, NativeMethods.ProcessBasicInformation,
                        out pbi, Marshal.SizeOf<NativeMethods.NtProcessBasicInformation>(), out retLen);
                    if (status == 0)
                    {
                        return pbi.InheritedFromUniqueProcessId.ToInt32();
                    }
                }
            }
            catch
            {
            }

            // Fallback: WMI
            return GetParentProcessIdWmi(pid);
        }

        /// <summary>
        /// Builds parent-PID and process-name maps for ALL processes from a single
        /// Toolhelp snapshot. Unlike the handle-based path this never hits "access denied"
        /// on system processes and never falls back to per-process WMI queries, so the
        /// whole scan stays in the low-millisecond range instead of timing out.
        /// </summary>
        internal static void GetProcessSnapshotFast(out Dictionary<int, int> parentMap, out Dictionary<int, string> nameMap)
        {
            parentMap = new Dictionary<int, int>();
            nameMap = new Dictionary<int, string>();

            IntPtr snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.TH32CS_SNAPPROCESS, 0);
            if (snapshot == NativeMethods.InvalidHandleValue || snapshot == IntPtr.Zero)
            {
                return;
            }

            try
            {
                NativeMethods.ProcessEntry32 entry = new NativeMethods.ProcessEntry32();
                entry.dwSize = (uint)Marshal.SizeOf<NativeMethods.ProcessEntry32>();
                if (NativeMethods.Process32First(snapshot, ref entry))
                {
                    do
                    {
                        int pid = (int)entry.th32ProcessID;
                        parentMap[pid] = (int)entry.th32ParentProcessID;
                        // Strip ".exe" so names match Process.ProcessName semantics
                        // (e.g. parent-name comparisons against "explorer").
                        nameMap[pid] = StripExeExtension(entry.szExeFile);
                    }
                    while (NativeMethods.Process32Next(snapshot, ref entry));
                }
            }
            catch
            {
            }
            finally
            {
                NativeMethods.CloseHandle(snapshot);
            }
        }

        private static string StripExeExtension(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }
            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? name.Substring(0, name.Length - 4)
                : name;
        }

        /// <summary>
        /// Fast batch parent-PID map via Toolhelp snapshot (covers system processes too).
        /// </summary>
        internal static Dictionary<int, int> GetParentProcessMapFast()
        {
            GetProcessSnapshotFast(out Dictionary<int, int> parentMap, out _);
            return parentMap;
        }

        private static int GetParentProcessIdWmi(int pid)
        {
            try
            {
                using (ManagementObject mo = new ManagementObject("win32_process.handle='" + pid + "'"))
                {
                    mo.Get();
                    object parent = mo["ParentProcessId"];
                    return parent == null ? 0 : Convert.ToInt32(parent);
                }
            }
            catch
            {
                return 0;
            }
        }

        internal static Dictionary<int, int> GetParentProcessMap()
        {
            Dictionary<int, int> map = new Dictionary<int, int>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId FROM Win32_Process"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        using (mo)
                        {
                            int pid = Convert.ToInt32(mo["ProcessId"]);
                            int parent = Convert.ToInt32(mo["ParentProcessId"]);
                            map[pid] = parent;
                        }
                    }
                }
            }
            catch
            {
            }
            return map;
        }

        internal static Dictionary<int, string> GetProcessNameMap()
        {
            GetProcessSnapshotFast(out _, out Dictionary<int, string> nameMap);
            return nameMap;
        }

        internal static Dictionary<int, string> GetProcessPathMap()
        {
            Dictionary<int, string> map = new Dictionary<int, string>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT ProcessId, ExecutablePath FROM Win32_Process"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        using (mo)
                        {
                            object pidValue = mo["ProcessId"];
                            if (pidValue == null)
                            {
                                continue;
                            }

                            object pathValue = mo["ExecutablePath"];
                            string path = pathValue == null ? "" : Convert.ToString(pathValue) ?? "";
                            map[Convert.ToInt32(pidValue)] = path;
                        }
                    }
                }
            }
            catch
            {
            }
            return map;
        }
        internal static HashSet<int> GetVisibleWindowProcessIds()
        {
            HashSet<int> ids = new HashSet<int>();
            NativeMethods.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (NativeMethods.IsWindowVisible(hWnd))
                {
                    uint pid;
                    NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
                    if (pid > 0)
                    {
                        ids.Add((int)pid);
                    }
                }
                return true;
            }, IntPtr.Zero);
            return ids;
        }

        internal static int SendMessageToProcessWindows(int processId, uint message)
        {
            int sent = 0;
            NativeMethods.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                uint pid;
                NativeMethods.GetWindowThreadProcessId(hWnd, out pid);
                if ((int)pid == processId && NativeMethods.IsWindowVisible(hWnd))
                {
                    NativeMethods.PostMessage(hWnd, message, IntPtr.Zero, IntPtr.Zero);
                    sent++;
                }
                return true;
            }, IntPtr.Zero);
            return sent;
        }

        internal static bool IsAlive(int processId)
        {
            return TryGetProcess(processId) != null;
        }

        internal static Process TryGetProcess(int processId)
        {
            try
            {
                return Process.GetProcessById(processId);
            }
            catch
            {
                return null;
            }
        }
    }
}
