using System;
using System.Collections.Generic;
using System.Linq;
using OneClickClose.Core;

namespace OneClickClose.Core
{
    /// <summary>
    /// 进程树节点 — 表示一个进程及其子进程。
    /// </summary>
    public sealed class ProcessTreeNode
    {
        public int Id { get; set; }
        public string ProcessName { get; set; }
        public string Path { get; set; }
        public long MemoryMb { get; set; }
        public string Action { get; set; }
        public int RiskScore { get; set; }
        public bool IsHighRisk { get; set; }
        public List<ProcessTreeNode> Children { get; } = new();
        public long TotalMemoryMb => MemoryMb + Children.Sum(c => c.TotalMemoryMb);
    }

    /// <summary>
    /// 从 ClosePlan 的结果构建进程树。
    /// 以 explorer.exe、services.exe、svchost.exe 等系统进程为根节点，
    /// 候选/保护/跳过的进程挂载到对应的父节点下。
    /// </summary>
    public static class ProcessTreeBuilder
    {
        private static readonly HashSet<string> RootProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "services", "svchost", "csrss", "wininit", "smss", "lsass", "winlogon", "System"
        };

        /// <summary>
        /// 从扫描结果构建进程树。
        /// </summary>
        public static List<ProcessTreeNode> BuildTree(ClosePlan plan)
        {
            var allRecords = new List<ProcessRecord>();
            if (plan.Candidates != null) allRecords.AddRange(plan.Candidates);
            if (plan.Protected != null) allRecords.AddRange(plan.Protected);

            var parentMap = ProcessCollector.GetParentProcessMapFast();
            var nodeMap = new Dictionary<int, ProcessTreeNode>();

            // Create nodes for all records
            foreach (var record in allRecords)
            {
                nodeMap[record.Id] = new ProcessTreeNode
                {
                    Id = record.Id,
                    ProcessName = record.ProcessName,
                    Path = record.Path,
                    MemoryMb = record.MemoryMb,
                    Action = record.Action,
                    RiskScore = record.RiskScore,
                    IsHighRisk = record.IsHighRisk
                };
            }

            // Build tree structure
            var roots = new List<ProcessTreeNode>();
            var assigned = new HashSet<int>();

            foreach (var kvp in nodeMap)
            {
                if (assigned.Contains(kvp.Key)) continue;
                int pid = kvp.Key;
                var node = kvp.Value;

                if (RootProcessNames.Contains(node.ProcessName) || !parentMap.ContainsKey(pid) || !parentMap.TryGetValue(pid, out int parentPid) || parentPid <= 0 || !nodeMap.ContainsKey(parentPid))
                {
                    roots.Add(node);
                }
                else
                {
                    var parent = nodeMap[parentPid];
                    parent.Children.Add(node);
                    assigned.Add(pid);
                }
            }

            // Any remaining unassigned nodes become roots
            foreach (var kvp in nodeMap)
            {
                if (!assigned.Contains(kvp.Key) && !roots.Contains(kvp.Value))
                {
                    roots.Add(kvp.Value);
                }
            }

            return roots.OrderByDescending(r => r.TotalMemoryMb).ToList();
        }
    }
}
