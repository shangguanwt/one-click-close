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
            return await Task.Run(() => GetClosePlan(configPath, token), token);
        }

        public static ClosePlan GetClosePlan(string configPath)
        {
            return GetClosePlan(configPath, CancellationToken.None);
        }

        public static ClosePlan GetClosePlan(string configPath, CancellationToken token)
        {
            AppConfig config = AppConfig.Load(configPath);
            HashSet<string> targets = config.TargetSet();
            HashSet<string> protectedNames = config.ProtectedSet();
            HashSet<string> forceAllowed = config.ForceSet();
            HashSet<int> visibleWindowPids = ProcessCollector.GetVisibleWindowProcessIds();
            HashSet<int> excludedIds = ProcessCollector.GetExcludedProcessIds();
            ProcessCollector.GetProcessSnapshotFast(out Dictionary<int, int> parentMap, out Dictionary<int, string> processNames);
            Dictionary<int, string> processPaths = ProcessCollector.GetProcessPathMap();
            Process[] processes = Process.GetProcesses();

            List<ProcessRecord> candidates = new List<ProcessRecord>();
            List<ProcessRecord> protectedRows = new List<ProcessRecord>();
            List<ProcessRecord> skippedRows = new List<ProcessRecord>();

            foreach (Process process in processes.OrderBy(p => p.SafeName()).ThenBy(p => p.SafeId()))
            {
                token.ThrowIfCancellationRequested();
                int pid = process.SafeId();
                string name = process.SafeName();
                string path = processPaths.ContainsKey(pid) ? processPaths[pid] : "";
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
                bool autoDetected = IsAutoDetectedCandidate(config, isTarget, isForceAllowed, hasWindow, userLaunched, userPath, systemPath, path, memoryMb);
                int riskScore = RiskCalculator.ComputeRiskScore(hasWindow, userLaunched, userPath, systemPath, memoryMb, isProtectedName, isForceAllowed);

                if (excludedIds.Contains(pid))
                {
                    skippedRows.Add(MakeRecord(process, path, hasWindow, ActionSkip, "当前工具进程", "skipped", parentPid, parentName, userLaunched, userPath, systemPath, autoDetected, memoryMb, riskScore));
                    continue;
                }

                if (IsCodexToolProcess(name, path))
                {
                    protectedRows.Add(MakeRecord(process, path, hasWindow, ActionProtect, "Codex 工具子进程", "protected", parentPid, parentName, userLaunched, userPath, systemPath, autoDetected, memoryMb, riskScore));
                    continue;
                }

                if (isProtectedName || systemPath)
                {
                    string reason = systemPath ? "系统路径自动保护" : "保护名单";
                    protectedRows.Add(MakeRecord(process, path, hasWindow, ActionProtect, reason, "protected", parentPid, parentName, userLaunched, userPath, systemPath, autoDetected, memoryMb, riskScore));
                    continue;
                }

                if (!isTarget && !isForceAllowed && !autoDetected)
                {
                    string reason = userPath ? "用户软件路径，未满足自动检测条件" : "不在关闭名单";
                    skippedRows.Add(MakeRecord(process, path, hasWindow, ActionSkip, reason, "skipped", parentPid, parentName, userLaunched, userPath, systemPath, autoDetected, memoryMb, riskScore));
                    continue;
                }

                if (hasWindow)
                {
                    string reason = autoDetected && !isTarget ? "自动发现的用户软件，有可见窗口，优先温和关闭" : "有可见窗口，优先温和关闭";
                    candidates.Add(MakeRecord(process, path, hasWindow, ActionGraceful, reason, "candidate", parentPid, parentName, userLaunched, userPath, systemPath, autoDetected, memoryMb, riskScore));
                }
                else if (isForceAllowed)
                {
                    candidates.Add(MakeRecord(process, path, hasWindow, ActionForce, "后台辅助进程，允许强制结束", "candidate", parentPid, parentName, userLaunched, userPath, systemPath, autoDetected, memoryMb, riskScore));
                }
                else if (IsTerminal(name))
                {
                    protectedRows.Add(MakeRecord(process, path, hasWindow, ActionProtect, "无窗口终端，避免误关脚本/任务", "protected", parentPid, parentName, userLaunched, userPath, systemPath, autoDetected, memoryMb, riskScore));
                }
                else if (autoDetected && config.CloseShutdownBlockingApps && userPath && !systemPath && riskScore < RiskCalculator.HighRiskScoreThreshold)
                {
                    candidates.Add(MakeRecord(process, path, hasWindow, ActionGraceful, "关机清障规则：用户软件可能阻碍关机，先温和关闭，超时后按安全策略处理", "candidate", parentPid, parentName, userLaunched, userPath, systemPath, autoDetected, memoryMb, riskScore));
                }
                else if (riskScore >= RiskCalculator.HighRiskScoreThreshold)
                {
                    candidates.Add(MakeRecord(process, path, hasWindow, ActionReport, "风险评分较高，跳过不关闭", "candidate", parentPid, parentName, userLaunched, userPath, systemPath, autoDetected, memoryMb, riskScore));
                }
                else
                {
                    string reason = autoDetected ? "自动发现的无窗口用户软件，默认只提示" : "无窗口，不在强制名单";
                    candidates.Add(MakeRecord(process, path, hasWindow, ActionReport, reason, "candidate", parentPid, parentName, userLaunched, userPath, systemPath, autoDetected, memoryMb, riskScore));
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

        public static List<ProcessGroupRow> GroupAllRows(ClosePlan plan)
        {
            if (plan == null)
            {
                return new List<ProcessGroupRow>();
            }

            return GroupRows((plan.Candidates ?? new List<ProcessRecord>())
                .Concat(plan.Protected ?? new List<ProcessRecord>())
                .Concat(plan.Skipped ?? new List<ProcessRecord>()));
        }

        public static List<ProcessGroupRow> FilterRows(IEnumerable<ProcessGroupRow> rows, ProcessGroupFilter filter)
        {
            if (rows == null)
            {
                return new List<ProcessGroupRow>();
            }

            IEnumerable<ProcessGroupRow> filtered = rows.Where(r => r != null);
            filtered = filtered.Where(row => MatchesFilter(row, filter));

            return filtered.ToList();
        }

        public static bool MatchesFilter(ProcessGroupRow row, ProcessGroupFilter filter)
        {
            return filter switch
            {
                ProcessGroupFilter.Closable => ContainsClosableCandidate(row),
                ProcessGroupFilter.Protected => ContainsProtectedRecord(row),
                ProcessGroupFilter.Skipped => ContainsSkippedRecord(row),
                ProcessGroupFilter.HighRisk => IsHighRiskRow(row),
                _ => row != null
            };
        }

        public static bool ContainsClosableCandidate(ProcessGroupRow row)
        {
            return (row?.Children ?? new List<ProcessRecord>()).Any(IsClosableCandidate);
        }

        public static bool IsClosableCandidate(ProcessRecord record)
        {
            if (record == null)
            {
                return false;
            }

            bool candidateStatus = string.Equals(record.Status, "candidate", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(record.Status);
            return candidateStatus
                && (record.Action == ActionGraceful || record.Action == ActionForce);
        }

        public static bool IsExecutableTarget(ProcessRecord record, AppConfig config)
        {
            if (!IsClosableCandidate(record))
            {
                return false;
            }

            if (config == null)
            {
                return true;
            }

            string name = record.ProcessName ?? string.Empty;
            if (config.ProtectedSet().Contains(name))
            {
                return false;
            }

            return config.TargetSet().Contains(name) || config.ForceSet().Contains(name);
        }

        public static List<ProcessRecord> FilterExecutableTargets(IEnumerable<ProcessRecord> records, AppConfig config)
        {
            return (records ?? Enumerable.Empty<ProcessRecord>())
                .Where(record => IsExecutableTarget(record, config))
                .ToList();
        }

        public static bool MatchesPrimaryIdentity(ProcessGroupRow row, string query)
        {
            if (row == null)
            {
                return false;
            }

            string needle = (query ?? string.Empty).Trim().ToLowerInvariant();
            if (needle.Length == 0)
            {
                return true;
            }

            if (ContainsNormalized(row.Process, needle))
            {
                return true;
            }

            return (row.Children ?? new List<ProcessRecord>())
                .Any(child => ContainsNormalized(child.ProcessName, needle));
        }

        private static bool ContainsNormalized(string value, string normalizedNeedle)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.ToLowerInvariant().Contains(normalizedNeedle);
        }

        private static bool ContainsProtectedRecord(ProcessGroupRow row)
        {
            return string.Equals(row?.Status, "protected", StringComparison.OrdinalIgnoreCase)
                || row?.Action == ActionProtect
                || (row?.Children ?? new List<ProcessRecord>()).Any(r =>
                    string.Equals(r.Status, "protected", StringComparison.OrdinalIgnoreCase)
                    || r.Action == ActionProtect);
        }

        private static bool ContainsSkippedRecord(ProcessGroupRow row)
        {
            return string.Equals(row?.Status, "skipped", StringComparison.OrdinalIgnoreCase)
                || row?.Action == ActionSkip
                || (row?.Children ?? new List<ProcessRecord>()).Any(r =>
                    string.Equals(r.Status, "skipped", StringComparison.OrdinalIgnoreCase)
                    || r.Action == ActionSkip);
        }

        private static bool IsHighRiskRow(ProcessGroupRow row)
        {
            return row != null
                && (row.IsHighRisk
                    || row.RiskScore >= RiskCalculator.HighRiskScoreThreshold
                    || row.Action == ActionReport
                    || (row.Children ?? new List<ProcessRecord>()).Any(r =>
                        r.IsHighRisk
                        || r.RiskScore >= RiskCalculator.HighRiskScoreThreshold
                        || r.Action == ActionReport));
        }

        public static List<ProcessGroupRow> GroupRows(IEnumerable<ProcessRecord> records)
        {
            return records
                .Where(r => r != null)
                .GroupBy(r => BuildAppGroupKey(r))
                .Select(g =>
                {
                    List<ProcessRecord> children = g
                        .OrderByDescending(r => r.MemoryMb)
                        .ThenBy(r => r.ProcessName)
                        .ThenBy(r => r.Id)
                        .ToList();
                    ProcessRecord first = children
                        .OrderByDescending(r => r.RiskScore)
                        .ThenByDescending(r => r.MemoryMb)
                        .First();
                    string usageHint = BuildUsageHint(children);
                    return new ProcessGroupRow
                    {
                        Process = first.ProcessName,
                        Count = g.Count(),
                        Action = first.Action,
                        Status = BuildGroupStatus(children),
                        Note = first.Reason,
                        Path = first.Path,
                        HasWindow = g.Any(r => r.HasWindow),
                        MemoryMb = g.Sum(r => r.MemoryMb),
                        RiskScore = g.Max(r => r.RiskScore),
                        IsHighRisk = g.Any(r => r.IsHighRisk),
                        AppKey = g.Key,
                        UsageHint = usageHint,
                        HabitHint = children.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.HabitHint))?.HabitHint,
                        Children = children
                    };
                })
                .OrderBy(r => r.RiskScore)
                .ThenByDescending(r => r.MemoryMb)
                .ThenBy(r => r.Process)
                .ThenBy(r => r.Action)
                .ToList();
        }

        private static string BuildGroupStatus(List<ProcessRecord> children)
        {
            if (children == null || children.Count == 0)
            {
                return string.Empty;
            }

            bool hasCandidate = children.Any(r => string.Equals(r.Status, "candidate", StringComparison.OrdinalIgnoreCase));
            bool hasProtected = children.Any(r => string.Equals(r.Status, "protected", StringComparison.OrdinalIgnoreCase));
            bool hasSkipped = children.Any(r => string.Equals(r.Status, "skipped", StringComparison.OrdinalIgnoreCase));

            if (hasCandidate && !hasProtected && !hasSkipped)
            {
                return "candidate";
            }

            if (hasProtected && !hasCandidate && !hasSkipped)
            {
                return "protected";
            }

            if (hasSkipped && !hasCandidate && !hasProtected)
            {
                return "skipped";
            }

            if (hasCandidate || hasProtected || hasSkipped)
            {
                return "mixed";
            }

            return children[0].Status ?? string.Empty;
        }

        internal static string BuildAppGroupKey(ProcessRecord record)
        {
            string path = NormalizePath(record.Path);
            if (!string.IsNullOrWhiteSpace(path))
            {
                return "path|" + path;
            }

            string parent = record.ParentProcessName ?? string.Empty;
            string name = record.ProcessName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(parent) && !string.Equals(parent, "explorer", StringComparison.OrdinalIgnoreCase))
            {
                return "parent|" + parent.ToLowerInvariant() + "|" + name.ToLowerInvariant();
            }

            return "name|" + name.ToLowerInvariant();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return System.IO.Path.GetFullPath(path.Trim()).TrimEnd('\\').ToLowerInvariant();
            }
            catch
            {
                return path.Trim().TrimEnd('\\').ToLowerInvariant();
            }
        }

        public static string BuildUsageHint(IEnumerable<ProcessRecord> records)
        {
            List<ProcessRecord> items = records?.Where(r => r != null).ToList() ?? new List<ProcessRecord>();
            if (items.Count == 0)
            {
                return "后台驻留";
            }

            string name = items[0].ProcessName ?? string.Empty;
            long memory = items.Sum(r => r.MemoryMb);
            bool hasWindow = items.Any(r => r.HasWindow);
            bool manyInstances = items.Count >= 4;
            bool userPath = items.Any(r => r.IsUserPath);

            if (IsBrowserLike(name) && (manyInstances || memory >= 1024))
            {
                return "浏览器多标签/扩展占用";
            }

            if (IsChatOrSyncLike(name))
            {
                return "聊天/同步工具后台驻留";
            }

            if (memory >= 1024)
            {
                return "内存占用明显偏高";
            }

            if (!hasWindow && userPath)
            {
                return "用户软件后台常驻";
            }

            if (manyInstances)
            {
                return "多实例后台进程";
            }

            return hasWindow ? "可见应用窗口" : "低风险优化候选";
        }

        private static bool IsBrowserLike(string name)
        {
            return ContainsAny(name, "chrome", "msedge", "edge", "firefox", "brave", "opera", "browser", "chromium");
        }

        private static bool IsChatOrSyncLike(string name)
        {
            return ContainsAny(name, "wechat", "weixin", "qq", "telegram", "discord", "slack", "teams", "onedrive", "dropbox", "baidu", "sync");
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            foreach (string needle in needles)
            {
                if (value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
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

        private static ProcessRecord MakeRecord(Process process, string path, bool hasWindow, string action, string reason, string status, int parentPid, string parentName, bool isUserLaunched, bool isUserPath, bool isSystemPath, bool isAutoDetected, long memoryMb, int riskScore)
        {
            return new ProcessRecord
            {
                Id = process.SafeId(),
                ProcessName = process.SafeName(),
                MainWindowTitle = process.SafeWindowTitle(),
                Path = path ?? "",
                HasWindow = hasWindow,
                ParentProcessId = parentPid,
                ParentProcessName = parentName,
                IsUserLaunched = isUserLaunched,
                IsUserPath = isUserPath,
                IsSystemPath = isSystemPath,
                IsAutoDetected = isAutoDetected,
                MemoryMb = memoryMb,
                RiskScore = riskScore,
                IsHighRisk = riskScore >= RiskCalculator.HighRiskScoreThreshold,
                Action = action,
                Reason = reason,
                Status = status
            };
        }

        internal static bool IsAutoDetectedCandidate(AppConfig config, bool isTarget, bool isForceAllowed, bool hasWindow, bool userLaunched, bool userPath, bool systemPath, string path, long memoryMb)
        {
            if (!config.AutoDetectUserApps || isTarget || isForceAllowed || systemPath || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (!userPath)
            {
                return false;
            }

            if (config.CloseShutdownBlockingApps)
            {
                return true;
            }

            if (hasWindow || userLaunched)
            {
                return true;
            }

            return memoryMb >= config.candidateMemoryThresholdMb;
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
