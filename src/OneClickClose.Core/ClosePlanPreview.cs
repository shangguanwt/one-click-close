using System;
using System.Collections.Generic;
using System.Linq;

namespace OneClickClose.Core;

public sealed class ClosePlanPreview
{
    public int TotalCandidates { get; private set; }
    public int GracefulCount { get; private set; }
    public int ExplicitForceCount { get; private set; }
    public int PossibleAutoForceCount { get; private set; }
    public int ReportOnlyCount { get; private set; }
    public int HighRiskCount { get; private set; }
    public long MemoryEstimateMb { get; private set; }
    public List<string> SampleProcessNames { get; private set; } = new();

    public bool HasForceRisk => ExplicitForceCount > 0 || PossibleAutoForceCount > 0;

    public static ClosePlanPreview FromPlan(ClosePlan plan, int sampleLimit = 5)
    {
        var preview = new ClosePlanPreview();
        if (plan?.Candidates == null || plan.Candidates.Count == 0)
        {
            return preview;
        }

        HashSet<string> forceAllowed = plan.Config?.ForceSet() ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        preview.TotalCandidates = plan.Candidates.Count;
        preview.GracefulCount = plan.Candidates.Count(p => p.Action == ProcessPlanner.ActionGraceful);
        preview.ExplicitForceCount = plan.Candidates.Count(p => p.Action == ProcessPlanner.ActionForce || forceAllowed.Contains(p.ProcessName ?? string.Empty));
        preview.PossibleAutoForceCount = plan.Candidates.Count(p => IsPossibleAutoForce(plan, p, forceAllowed));
        preview.ReportOnlyCount = plan.Candidates.Count(p => p.Action == ProcessPlanner.ActionReport);
        preview.HighRiskCount = plan.Candidates.Count(p => p.IsHighRisk || p.RiskScore >= RiskCalculator.HighRiskScoreThreshold);
        preview.MemoryEstimateMb = plan.Candidates.Sum(p => Math.Max(0, p.MemoryMb));
        preview.SampleProcessNames = plan.Candidates
            .Select(p => p.ProcessName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, sampleLimit))
            .ToList();
        return preview;
    }

    public string ToDialogMessage()
    {
        if (TotalCandidates == 0)
        {
            return "\u5f53\u524d\u6ca1\u6709\u53ef\u6267\u884c\u7684\u5019\u9009\u8fdb\u7a0b\u3002";
        }

        var lines = new List<string>
        {
            $"\u5019\u9009\u8fdb\u7a0b\uff1a{TotalCandidates} \u4e2a",
            $"\u6e29\u548c\u5173\u95ed\uff1a{GracefulCount} \u4e2a",
            $"\u660e\u786e\u5f3a\u5236\uff1a{ExplicitForceCount} \u4e2a",
            $"\u53ef\u80fd\u81ea\u52a8\u5f3a\u5236\uff1a{PossibleAutoForceCount} \u4e2a",
            $"\u4ec5\u63d0\u793a\u4e0d\u5173\u95ed\uff1a{ReportOnlyCount} \u4e2a",
            $"\u9ad8\u98ce\u9669\u9879\uff1a{HighRiskCount} \u4e2a",
            $"\u9884\u8ba1\u91ca\u653e\uff1a{FormatMemory(MemoryEstimateMb)}"
        };

        if (SampleProcessNames.Count > 0)
        {
            lines.Add("\u793a\u4f8b\uff1a" + string.Join("\u3001", SampleProcessNames));
        }

        if (HasForceRisk)
        {
            lines.Add("\u8bf7\u786e\u8ba4\uff1a\u5f3a\u5236\u5173\u95ed\u53ef\u80fd\u5bfc\u81f4\u672a\u4fdd\u5b58\u6570\u636e\u4e22\u5931\u3002\u4f4e\u98ce\u9669\u9879\u4f1a\u5148\u5c1d\u8bd5\u6e29\u548c\u5173\u95ed\u3002");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsPossibleAutoForce(ClosePlan plan, ProcessRecord item, HashSet<string> forceAllowed)
    {
        if (item == null || forceAllowed.Contains(item.ProcessName ?? string.Empty))
        {
            return false;
        }

        return plan.Config?.ForceAfterGracefulFailure == true
            && item.IsAutoDetected
            && item.Action == ProcessPlanner.ActionGraceful
            && !item.IsHighRisk
            && item.RiskScore < RiskCalculator.HighRiskScoreThreshold
            && item.IsUserPath
            && !item.IsSystemPath
            && !string.IsNullOrWhiteSpace(item.Path);
    }

    private static string FormatMemory(long mb)
    {
        if (mb >= 1024)
        {
            return ((double)mb / 1024).ToString("F1") + " GB";
        }

        return mb + " MB";
    }
}
