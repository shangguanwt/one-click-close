using System;
using System.Runtime.InteropServices;

namespace OneClickClose.Core
{
    internal static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public const uint WmClose = 0x0010;
        public const uint WmQueryEndSession = 0x0011;

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // ── NtQueryInformationProcess — fast parent PID lookup (avoids WMI) ──

        [StructLayout(LayoutKind.Sequential)]
        internal struct NtProcessBasicInformation
        {
            public int ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public int BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        internal static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            out NtProcessBasicInformation processInformation,
            int processInformationLength,
            out int returnLength);

        internal const int ProcessBasicInformation = 0;

        // ── Toolhelp process snapshot — fast parent PID + name for ALL processes
        //    in one call (no handle open, no access-denied, no WMI fallback) ──

        public const uint TH32CS_SNAPPROCESS = 0x00000002;
        public static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct ProcessEntry32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32FirstW", ExactSpelling = true)]
        internal static extern bool Process32First(IntPtr hSnapshot, ref ProcessEntry32 lppe);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "Process32NextW", ExactSpelling = true)]
        internal static extern bool Process32Next(IntPtr hSnapshot, ref ProcessEntry32 lppe);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool CloseHandle(IntPtr hObject);

        // ── Idle detection ──

        [StructLayout(LayoutKind.Sequential)]
        internal struct LastInputInfo
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll", SetLastError = false)]
        internal static extern bool GetLastInputInfo(out LastInputInfo plii);
    }
}
