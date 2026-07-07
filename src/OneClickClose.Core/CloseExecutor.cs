using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace OneClickClose.Core
{
    /// <summary>
    /// Three-stage process close pipeline extracted from ProcessPlanner.
    /// Stage 1: WM_CLOSE (graceful), Stage 2: WM_QUERYENDSESSION, Stage 3: force kill.
    /// </summary>
    public static class CloseExecutor
    {
        public static CloseResult Execute(ClosePlan plan, Action<string> log, CancellationToken token = default(CancellationToken))
        {
            if (log == null)
            {
                log = delegate { };
            }

            var ctx = new CloseExecutionContext
            {
                Result = new CloseResult(),
                Plan = plan,
                Log = log,
                Token = token,
                StageTargets = new List<ProcessRecord>(),
                StageOneAlive = new List<ProcessRecord>(),
                StageTwoAlive = new List<ProcessRecord>()
            };

            log(LogLine("[INFO]", "开始处理，候选 " + plan.Candidates.Count + " 个。"));
            log(LogLine("[SKIP]", "跳过保护 " + plan.Protected.Count + " 个进程。"));

            SeparateReportOnly(ctx);

            if (ctx.StageTargets.Count > 0)
            {
                ExecuteGracefulStage(ctx);
            }

            List<ProcessRecord> afterStageOne = CollectSurvivors(ctx.StageOneAlive, ctx.Result, ctx.Log, "阶段 1", "[CLEAN EXIT]", "后已干净退出。");
            log(LogLine("[INFO]", "阶段 1 完成：已退出 " + ctx.Result.GracefulClosed.Count + " 个，仍需处理 " + afterStageOne.Count + " 个。"));

            if (afterStageOne.Count > 0)
            {
                ExecuteQueryEndSessionStage(ctx, afterStageOne);
            }

            List<ProcessRecord> afterStageTwo = CollectSurvivors(ctx.StageTwoAlive, ctx.Result, ctx.Log, "阶段 2", "[OK]", "后已退出。");
            log(LogLine("[INFO]", "阶段 2 完成：仍需判断强制清理 " + afterStageTwo.Count + " 个。"));

            token.ThrowIfCancellationRequested();
            ExecuteForceStage(ctx, afterStageTwo);
            CollectRemaining(ctx);
            LogFinalSummary(ctx);

            return ctx.Result;
        }

        private static void SeparateReportOnly(CloseExecutionContext ctx)
        {
            foreach (ProcessRecord item in ctx.Plan.Candidates)
            {
                if (item.Action == ProcessPlanner.ActionReport)
                {
                    ctx.Result.ReportOnly.Add(item);
                    ctx.Log(LogLine("[SKIP]", item.ProcessName + " (" + item.Id + ") 只提示，不执行关闭。原因：" + item.Reason));
                }
                else
                {
                    ctx.StageTargets.Add(item);
                }
            }
        }

        private static void ExecuteGracefulStage(CloseExecutionContext ctx)
        {
            ctx.Token.ThrowIfCancellationRequested();

            foreach (ProcessRecord item in ctx.StageTargets)
            {
                Process process = ProcessCollector.TryGetProcess(item.Id);
                if (process == null)
                {
                    ctx.Result.GracefulClosed.Add(item);
                    ctx.Log(LogLine("[CLEAN EXIT]", item.ProcessName + " (" + item.Id + ") 在阶段 1 前已经退出。"));
                    continue;
                }

                int sent = ProcessCollector.SendMessageToProcessWindows(item.Id, NativeMethods.WmClose);
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    try
                    {
                        if (process.CloseMainWindow())
                        {
                            sent++;
                        }
                    }
                    catch (Exception ex)
                    {
                        ctx.Log(LogLine("[ERROR]", item.ProcessName + " (" + item.Id + ") CloseMainWindow 失败：" + ex.Message));
                    }
                }

                ctx.StageOneAlive.Add(item);
                if (sent > 0)
                {
                    ctx.Log(LogLine("[INFO]", "阶段 1 已发送 WM_CLOSE：" + item.ProcessName + " (" + item.Id + ")。"));
                }
                else
                {
                    ctx.Log(LogLine("[INFO]", "阶段 1 未找到可见窗口：" + item.ProcessName + " (" + item.Id + ")。"));
                }
            }

            ctx.Log(LogLine("[INFO]", "阶段 1 等待 " + ctx.Plan.Config.gracefulTimeoutSeconds + " 秒。"));
            CancellableSleep(TimeSpan.FromSeconds(ctx.Plan.Config.gracefulTimeoutSeconds), ctx.Token);
        }

        private static List<ProcessRecord> CollectSurvivors(List<ProcessRecord> alive, CloseResult result, Action<string> log, string stageName, string exitLevel, string exitSuffix)
        {
            List<ProcessRecord> survivors = new List<ProcessRecord>();
            foreach (ProcessRecord item in alive)
            {
                if (!ProcessCollector.IsAlive(item.Id))
                {
                    result.GracefulClosed.Add(item);
                    log(LogLine(exitLevel, item.ProcessName + " (" + item.Id + ") " + stageName + exitSuffix));
                }
                else
                {
                    survivors.Add(item);
                }
            }
            return survivors;
        }

        private static void ExecuteQueryEndSessionStage(CloseExecutionContext ctx, List<ProcessRecord> afterStageOne)
        {
            ctx.Token.ThrowIfCancellationRequested();

            foreach (ProcessRecord item in afterStageOne)
            {
                int sent = ProcessCollector.SendMessageToProcessWindows(item.Id, NativeMethods.WmQueryEndSession);
                ctx.StageTwoAlive.Add(item);
                if (sent > 0)
                {
                    ctx.Log(LogLine("[INFO]", "阶段 2 已发送 WM_QUERYENDSESSION：" + item.ProcessName + " (" + item.Id + ")。"));
                }
                else
                {
                    ctx.Log(LogLine("[INFO]", "阶段 2 未找到可提示窗口：" + item.ProcessName + " (" + item.Id + ")。"));
                }
            }

            ctx.Log(LogLine("[INFO]", "阶段 2 等待 " + ctx.Plan.Config.queryTimeoutSeconds + " 秒。"));
            CancellableSleep(TimeSpan.FromSeconds(ctx.Plan.Config.queryTimeoutSeconds), ctx.Token);
        }

        private static void ExecuteForceStage(CloseExecutionContext ctx, List<ProcessRecord> afterStageTwo)
        {
            HashSet<string> forceAllowed = ctx.Plan.Config.ForceSet();
            foreach (ProcessRecord item in afterStageTwo)
            {
                if (!ProcessCollector.IsAlive(item.Id))
                {
                    continue;
                }

                bool explicitForce = forceAllowed.Contains(item.ProcessName);
                bool safeAutoForce = ctx.Plan.Config.ForceAfterGracefulFailure
                    && item.IsAutoDetected
                    && item.Action == ProcessPlanner.ActionGraceful
                    && !item.IsHighRisk
                    && item.RiskScore < RiskCalculator.HighRiskScoreThreshold
                    && item.IsUserPath
                    && !item.IsSystemPath
                    && !string.IsNullOrWhiteSpace(item.Path);

                if (!explicitForce && !safeAutoForce)
                {
                    AddUnique(ctx, item);
                    string reason = item.IsAutoDetected
                        ? "未满足安全强制条件，保留运行。"
                        : "不在强制清理名单，保留运行。";
                    ctx.Log(LogLine("[SKIP]", item.ProcessName + " (" + item.Id + ") " + reason));
                    continue;
                }

                try
                {
                    Process process = Process.GetProcessById(item.Id);
                    KillProcess(process, out string mode);
                    ctx.Result.Forced.Add(item);
                    ctx.ForcedIds.Add(item.Id);
                    ctx.Log(LogLine("[FORCE]", "阶段 3 已强制关闭" + mode + "：" + item.ProcessName + " (" + item.Id + ")。"));
                }
                catch (Exception ex)
                {
                    AddUnique(ctx, item);
                    ctx.Log(LogLine("[ERROR]", item.ProcessName + " (" + item.Id + ") 强制关闭失败：" + ex.Message));
                }
            }
        }

        private static void KillProcess(Process process, out string mode)
        {
            try
            {
                process.Kill(true);
                mode = "进程树";
            }
            catch (Exception treeEx)
            {
                try
                {
                    process.Kill();
                    mode = "单进程（进程树失败：" + treeEx.Message + "）";
                }
                catch (Exception singleEx)
                {
                    throw new InvalidOperationException("进程树失败：" + treeEx.Message + "；单进程失败：" + singleEx.Message, singleEx);
                }
            }
        }

        private static void CollectRemaining(CloseExecutionContext ctx)
        {
            foreach (ProcessRecord item in ctx.Plan.Candidates)
            {
                if (ProcessCollector.IsAlive(item.Id) && !ctx.RemainingIds.Contains(item.Id) && !ctx.ForcedIds.Contains(item.Id))
                {
                    AddUnique(ctx, item);
                }
            }
        }

        private static void LogFinalSummary(CloseExecutionContext ctx)
        {
            ctx.Log(LogLine("[OK]", string.Format("结果：已温和关闭 {0} 个；已强制关闭 {1} 个；跳过保护 {2} 个；仍在运行 {3} 个。",
                ctx.Result.GracefulClosed.Count, ctx.Result.Forced.Count, ctx.Plan.Protected.Count, ctx.Result.Remaining.Count)));

            if (ctx.Result.Remaining.Count > 0)
            {
                foreach (ProcessRecord item in ctx.Result.Remaining.OrderBy(r => r.ProcessName).ThenBy(r => r.Id))
                {
                    ctx.Log(LogLine("[INFO]", "仍在运行：" + item.ProcessName + " (" + item.Id + ") - " + item.Reason));
                }
            }
        }

        private sealed class CloseExecutionContext
        {
            public CloseResult Result;
            public ClosePlan Plan;
            public Action<string> Log;
            public CancellationToken Token;
            public List<ProcessRecord> StageTargets;
            public List<ProcessRecord> StageOneAlive;
            public List<ProcessRecord> StageTwoAlive;

            // O(1) 去重跟踪，避免 AddUnique 在循环中做 O(n²) 线性扫描
            public readonly HashSet<int> RemainingIds = new();
            public readonly HashSet<int> ForcedIds = new();
        }

        private static string LogLine(string level, string message)
        {
            return DateTime.Now.ToString("HH:mm:ss") + " " + level + " " + message;
        }

        private static void CancellableSleep(TimeSpan duration, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow + duration;
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                Thread.Sleep(Math.Min(200, Math.Max(0, remaining)));
            }
        }

        private static void AddUnique(CloseExecutionContext ctx, ProcessRecord item)
        {
            if (ctx.RemainingIds.Add(item.Id))
            {
                ctx.Result.Remaining.Add(item);
            }
        }
    }
}
