using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OneClickClose.Core
{
    /// <summary>
    /// Pure planning logic: scans processes, classifies them into candidates/protected/skipped,
    /// and formats the close plan. Execution is delegated to <see cref="CloseExecutor"/>.
    /// Process enumeration is delegated to <see cref="ProcessCollector"/>.
    /// Risk scoring is delegated to <see cref="RiskCalculator"/>.
    /// Safe Process accessors are in <see cref="ProcessSafetyExtensions"/>.
    /// </summary>
    public static class ProcessPlanner
    {
        public const string ActionGraceful = "温和关闭";
        public const string ActionForce = "强制清理";
        public const string ActionReport = "跳过（高风险）";
        public const string ActionProtect = "保护";
        public const string ActionSkip = "跳过";

        /// <summary>
        /// Async wrapper: runs the full scan on a background thread so the UI is not blocked.
        /// </summary>
        public static async Task<ClosePlan> GetClosePlanAsync(string configPath, CancellationToken token = default(CancellationToken))
        {
            return await Task.Run(() => GetClosePlan(configPath), token);
        }

        public static ClosePlan GetClosePlan(string configPath)
        {
            AppConfig config = AppConfig.Load(configPath);
            HashSet<string> targets = config.TargetSet();
            HashSet<string> protectedNames = config.ProtectedSet();
            HashSet<string> forceAllowed = config.ForceSet();
            HashSet<int> visibleWindowPids = ProcessCollector.GetVisibleWindowProcessIds();
            HashSet<int> excludedIds = ProcessCollector.GetExcludedProcessIds();
            ProcessCollector.GetProcessSnapshotFast(out Dictionary<int, int> parentMap, out Dictionary<int, string> processNames);

            List<ProcessRecord> candidates = new List<ProcessRecord>();
            List<ProcessRecord> protectedRows = new List<ProcessRecord>();
            List<ProcessRecord> skippedRows = new List<ProcessRecord>();

            foreach (Process process in Process.GetProcesses().OrderBy(p => p.SafeName()).ThenBy(p => p.SafeId()))
            {
                int pid = process.SafeId();
                string name = process.SafeName();
                string path = process.SafePath();
                bool hasWindow = process.MainWindowHandle != IntPtr.Zero || visibleWindowPids.Contains(pid);
                int parentPid = parentMap.ContainsKey(pid) ? parentMap[pid] : 0;
                string parentName = processNames.ContainsKey(parentPid) ? processNames[parentPid] : "";
                bool userLaunched = string.Equals(parentName, "explorer", StringComparison.OrdinalIgnoreCase);
                bool userPath = RiskCalculator.IsUserPath(path);
                bool systemPath = RiskCalculator.IsSystemPath(path);
                long memoryMb = process.SafeWorkingSetMb();
                bool isTarget = targets.Contains(name);
                bool isForceAllowed = forceAllowed.Contains(name);
                bool isProtectedName = protectedNames.Contains(name);
                int riskScore = RiskCalculator.ComputeRiskScore(hasWindow, userLaunched, userPath, systemPath, memoryMb, isProtectedName, isForceAllowed);

                if (excludedIds.Contains(pid))
                {
                    skippedRows.Add(MakeRecord(process, hasWindow, ActionSkip, "当前工具进程", "skipped", parentPid, parentName, userLaunched, userPath, systemPath, memoryMb, riskScore));
                    continue;
                }

                if (IsCodexToolProcess(name, path))
                {
                    protectedRows.Add(MakeRecord(process, hasWindow, ActionProtect, "Codex 工具子进程", "protected", parentPid, parentName, userLaunched, userPath, systemPath, memoryMb, riskScore));
                    continue;
                }

                if (isProtectedName || systemPath)
                {
                    string reason = systemPath ? "系统路径自动保护" : "保护名单";
                    protectedRows.Add(MakeRecord(process, hasWindow, ActionProtect, reason, "protected", parentPid, parentName, userLaunched, userPath, systemPath, memoryMb, riskScore));
                    continue;
                }

                if (!isTarget && !isForceAllowed)
                {
                    string reason = userPath ? "用户软件路径，未在关闭名单" : "不在关闭名单";
                    skippedRows.Add(MakeRecord(process, hasWindow, ActionSkip, reason, "skipped", parentPid, parentName, userLaunched, userPath, systemPath, memoryMb, riskScore));
                    continue;
                }

                if (hasWindow)
                {
                    candidates.Add(MakeRecord(process, hasWindow, ActionGraceful, "有可见窗口，优先温和关闭", "candidate", parentPid, parentName, userLaunched, userPath, systemPath, memoryMb, riskScore));
                }
                else if (isForceAllowed)
                {
                    candidates.Add(MakeRecord(process, hasWindow, ActionForce, "后台辅助进程，允许强制结束", "candidate", parentPid, parentName, userLaunched, userPath, systemPath, memoryMb, riskScore));
                }
                else if (IsTerminal(name))
                {
                    protectedRows.Add(MakeRecord(process, hasWindow, ActionProtect, "无窗口终端，避免误关脚本/任务", "protected", parentPid, parentName, userLaunched, userPath, systemPath, memoryMb, riskScore));
                }
                else if (riskScore >= RiskCalculator.HighRiskScoreThreshold)
                {
                    candidates.Add(MakeRecord(process, hasWindow, ActionReport, "风险评分较高，跳过不关闭", "candidate", parentPid, parentName, userLaunched, userPath, systemPath, memoryMb, riskScore));
                }
                else
                {
                    candidates.Add(MakeRecord(process, hasWindow, ActionReport, "无窗口，不在强制名单", "candidate", parentPid, parentName, userLaunched, userPath, systemPath, memoryMb, riskScore));
                }
            }

            return new ClosePlan
            {
                Config = config,
                Candidates = candidates.OrderBy(r => r.RiskScore).ThenBy(r => r.ProcessName).ThenBy(r => r.Id).ToList(),
                Protected = protectedRows.OrderByDescending(r => r.RiskScore).ThenBy(r => r.ProcessName).ThenBy(r => r.Id).ToList(),
                Skipped = skippedRows.OrderByDescending(r => r.RiskScore).ThenBy(r => r.ProcessName).ThenBy(r => r.Id).ToList()
            };
        }

        public static List<ProcessGroupRow> GroupRows(IEnumerable<ProcessRecord> records)
        {
            return records
                .GroupBy(r => (r.ProcessName ?? "") + "|" + (r.Action ?? ""))
                .Select(g =>
                {
                    ProcessRecord first = g.OrderByDescending(r => r.RiskScore).First();
                    return new ProcessGroupRow
                    {
                        Process = first.ProcessName,
                        Count = g.Count(),
                        Action = first.Action,
                        Note = first.Reason,
                        Path = first.Path,
                        HasWindow = g.Any(r => r.HasWindow),
                        MemoryMb = g.Sum(r => r.MemoryMb),
                        RiskScore = g.Max(r => r.RiskScore),
                        IsHighRisk = g.Any(r => r.IsHighRisk)
                    };
                })
                .OrderBy(r => r.RiskScore)
                .ThenBy(r => r.Process)
                .ThenBy(r => r.Action)
                .ToList();
        }

        public static string Summary(ClosePlan plan)
        {
            int graceful = plan.Candidates.Count(r => r.Action == ActionGraceful);
            int force = plan.Candidates.Count(r => r.Action == ActionForce);
            int report = plan.Candidates.Count(r => r.Action == ActionReport);
            return string.Format("温和关闭 {0} 个，强制清理 {1} 个，只提示 {2} 个", graceful, force, report);
        }

        public static string FormatPlan(ClosePlan plan)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[INFO] 一键关闭后台软件 - 预览");
            sb.AppendLine(string.Format("[INFO] 待处理进程：{0}；保护进程：{1}", plan.Candidates.Count, plan.Protected.Count));
            sb.AppendLine("[INFO] " + Summary(plan));
            sb.AppendLine();
            sb.AppendLine("[INFO] 将处理：");
            foreach (ProcessGroupRow row in GroupRows(plan.Candidates))
            {
                sb.AppendLine(string.Format("  {0,-28} x{1,-3} {2,-8} 风险 {3,3} - {4}", row.Process, row.Count, row.Action, row.RiskScore, row.Note));
            }

            sb.AppendLine();
            sb.AppendLine("[SKIP] 已保护：");
            foreach (ProcessGroupRow row in GroupRows(plan.Protected).Take(80))
            {
                sb.AppendLine(string.Format("  {0,-28} x{1,-3} 风险 {2,3} - {3}", row.Process, row.Count, row.RiskScore, row.Note));
            }
            return sb.ToString();
        }

        private static ProcessRecord MakeRecord(Process process, bool hasWindow, string action, string reason, string status, int parentPid, string parentName, bool isUserLaunched, bool isUserPath, bool isSystemPath, long memoryMb, int riskScore)
        {
            return new ProcessRecord
            {
                Id = process.SafeId(),
                ProcessName = process.SafeName(),
                MainWindowTitle = process.SafeWindowTitle(),
                Path = process.SafePath(),
                HasWindow = hasWindow,
                ParentProcessId = parentPid,
                ParentProcessName = parentName,
                IsUserLaunched = isUserLaunched,
                IsUserPath = isUserPath,
                IsSystemPath = isSystemPath,
                MemoryMb = memoryMb,
                RiskScore = riskScore,
                IsHighRisk = riskScore >= RiskCalculator.HighRiskScoreThreshold,
                Action = action,
                Reason = reason,
                Status = status
            };
        }

        private static bool IsTerminal(string name)
        {
            return string.Equals(name, "powershell", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "pwsh", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "cmd", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCodexToolProcess(string name, string path)
        {
            if (!string.Equals(name, "codex", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(path)
                && path.IndexOf("\\AppData\\Local\\OpenAI\\Codex\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
