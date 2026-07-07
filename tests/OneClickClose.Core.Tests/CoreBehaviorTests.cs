using OneClickClose.Core;

namespace OneClickClose.Core.Tests;

public sealed class AppConfigTests
{
    [Fact]
    public void NormalizeThroughSaveAndLoad_FillsSafeDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), "oneclickclose-tests", Guid.NewGuid().ToString("N"), "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        AppConfig.Save(path, new AppConfig
        {
            waitSeconds = 0,
            gracefulTimeoutSeconds = 0,
            queryTimeoutSeconds = 0,
            candidateMemoryThresholdMb = 0,
            targetNames = null,
            protectedNames = null,
            forceAllowedNames = null
        });

        AppConfig loaded = AppConfig.Load(path);

        Assert.Equal(5, loaded.waitSeconds);
        Assert.Equal(5, loaded.gracefulTimeoutSeconds);
        Assert.Equal(3, loaded.queryTimeoutSeconds);
        Assert.True(loaded.AutoDetectUserApps);
        Assert.True(loaded.CloseShutdownBlockingApps);
        Assert.True(loaded.ForceAfterGracefulFailure);
        Assert.Equal(128, loaded.candidateMemoryThresholdMb);
        Assert.Empty(loaded.TargetSet());
        Assert.Empty(loaded.ProtectedSet());
        Assert.Empty(loaded.ForceSet());
    }
}

public sealed class RiskCalculatorTests
{
    [Fact]
    public void ComputeRiskScore_UserVisibleApp_IsLowRiskCandidate()
    {
        int score = RiskCalculator.ComputeRiskScore(
            hasWindow: true,
            userLaunched: true,
            userPath: true,
            systemPath: false,
            memoryMb: 768,
            protectedName: false,
            forceAllowed: false);

        Assert.True(score < RiskCalculator.HighRiskScoreThreshold);
    }

    [Fact]
    public void ComputeRiskScore_SystemProtectedProcess_IsHighRisk()
    {
        int score = RiskCalculator.ComputeRiskScore(
            hasWindow: false,
            userLaunched: false,
            userPath: false,
            systemPath: true,
            memoryMb: 32,
            protectedName: true,
            forceAllowed: false);

        Assert.True(score >= RiskCalculator.HighRiskScoreThreshold);
    }
}

public sealed class ProcessPlannerTests
{
    [Fact]
    public void IsAutoDetectedCandidate_ShutdownBlockingModeIncludesLowMemoryUserPathBackgroundApp()
    {
        var config = new AppConfig
        {
            autoDetectUserApps = true,
            closeShutdownBlockingApps = true,
            candidateMemoryThresholdMb = 512
        };

        bool detected = ProcessPlanner.IsAutoDetectedCandidate(
            config,
            isTarget: false,
            isForceAllowed: false,
            hasWindow: false,
            userLaunched: false,
            userPath: true,
            systemPath: false,
            path: @"C:\Program Files\UserApp\helper.exe",
            memoryMb: 16);

        Assert.True(detected);
    }

    [Fact]
    public void IsAutoDetectedCandidate_DisabledShutdownBlockingModeKeepsMemoryThreshold()
    {
        var config = new AppConfig
        {
            autoDetectUserApps = true,
            closeShutdownBlockingApps = false,
            candidateMemoryThresholdMb = 512
        };

        bool detected = ProcessPlanner.IsAutoDetectedCandidate(
            config,
            isTarget: false,
            isForceAllowed: false,
            hasWindow: false,
            userLaunched: false,
            userPath: true,
            systemPath: false,
            path: @"C:\Program Files\UserApp\helper.exe",
            memoryMb: 16);

        Assert.False(detected);
    }

    [Fact]
    public void GroupRows_GroupsByAppIdentity_AndAggregatesRiskMemoryWindowStateAndAction()
    {
        var records = new[]
        {
            Record("chrome", ProcessPlanner.ActionGraceful, memoryMb: 100, riskScore: 20, hasWindow: false, path: @"C:\Apps\Chrome\chrome.exe"),
            Record("chrome", ProcessPlanner.ActionGraceful, memoryMb: 150, riskScore: 35, hasWindow: true, path: @"C:\Apps\Chrome\chrome.exe"),
            Record("chrome", ProcessPlanner.ActionReport, memoryMb: 20, riskScore: 80, hasWindow: false, path: @"C:\Apps\Chrome\chrome.exe"),
            Record("notepad", ProcessPlanner.ActionGraceful, memoryMb: 10, riskScore: 10, hasWindow: true, path: @"C:\Windows\notepad.exe")
        };

        List<ProcessGroupRow> rows = ProcessPlanner.GroupRows(records);

        ProcessGroupRow chrome = Assert.Single(rows, r => r.Process == "chrome");
        Assert.Equal(ProcessPlanner.ActionReport, chrome.Action);
        Assert.Equal(3, chrome.Count);
        Assert.Equal(270, chrome.MemoryMb);
        Assert.Equal(80, chrome.RiskScore);
        Assert.True(chrome.HasWindow);
        Assert.Equal(3, chrome.Children.Count);

        Assert.Contains(rows, r => r.Process == "notepad" && r.Action == ProcessPlanner.ActionGraceful);
    }

    [Fact]
    public void GroupRows_DoesNotMergeSameNameFromDifferentPaths()
    {
        var records = new[]
        {
            Record("helper", ProcessPlanner.ActionGraceful, memoryMb: 50, riskScore: 20, hasWindow: false, path: @"C:\AppA\helper.exe"),
            Record("helper", ProcessPlanner.ActionGraceful, memoryMb: 70, riskScore: 30, hasWindow: false, path: @"D:\AppB\helper.exe")
        };

        List<ProcessGroupRow> rows = ProcessPlanner.GroupRows(records);

        Assert.Equal(2, rows.Count(r => r.Process == "helper"));
        Assert.All(rows, r => Assert.Single(r.Children));
    }

    [Fact]
    public void GroupAllRows_IncludesCandidateProtectedAndSkippedProcesses()
    {
        var plan = new ClosePlan
        {
            Candidates = new List<ProcessRecord>
            {
                Record("Weixin", ProcessPlanner.ActionGraceful, memoryMb: 380, riskScore: 25, hasWindow: true, path: @"D:\微信\Weixin\Weixin.exe", status: "candidate")
            },
            Protected = new List<ProcessRecord>
            {
                Record("System", ProcessPlanner.ActionProtect, memoryMb: 20, riskScore: 95, hasWindow: false, path: @"C:\Windows\System32\System.exe", status: "protected")
            },
            Skipped = new List<ProcessRecord>
            {
                Record("TIM", ProcessPlanner.ActionSkip, memoryMb: 220, riskScore: 30, hasWindow: true, path: @"D:\tim\Bin\TIM.exe", status: "skipped")
            }
        };

        List<ProcessGroupRow> rows = ProcessPlanner.GroupAllRows(plan);

        Assert.Contains(rows, r => r.Process == "Weixin" && r.Children.Any(c => c.Status == "candidate"));
        Assert.Contains(rows, r => r.Process == "System" && r.Children.Any(c => c.Status == "protected"));
        Assert.Contains(rows, r => r.Process == "TIM" && r.Children.Any(c => c.Status == "skipped"));
    }

    [Fact]
    public void FilterRows_AppliesAllProcessViewSemantics()
    {
        var rows = ProcessPlanner.GroupAllRows(new ClosePlan
        {
            Candidates = new List<ProcessRecord>
            {
                Record("Weixin", ProcessPlanner.ActionGraceful, memoryMb: 380, riskScore: 25, hasWindow: true, status: "candidate"),
                Record("risk_report", ProcessPlanner.ActionReport, memoryMb: 10, riskScore: 90, hasWindow: false, status: "candidate")
            },
            Protected = new List<ProcessRecord>
            {
                Record("explorer", ProcessPlanner.ActionProtect, memoryMb: 90, riskScore: 80, hasWindow: true, status: "protected")
            },
            Skipped = new List<ProcessRecord>
            {
                Record("TIM", ProcessPlanner.ActionSkip, memoryMb: 220, riskScore: 30, hasWindow: true, status: "skipped")
            }
        });

        Assert.Equal(4, ProcessPlanner.FilterRows(rows, ProcessGroupFilter.All).Count);
        Assert.Equal(new[] { "Weixin" }, ProcessPlanner.FilterRows(rows, ProcessGroupFilter.Closable).Select(r => r.Process).ToArray());
        Assert.Equal(new[] { "explorer" }, ProcessPlanner.FilterRows(rows, ProcessGroupFilter.Protected).Select(r => r.Process).ToArray());
        Assert.Equal(new[] { "TIM" }, ProcessPlanner.FilterRows(rows, ProcessGroupFilter.Skipped).Select(r => r.Process).ToArray());
        Assert.Contains(ProcessPlanner.FilterRows(rows, ProcessGroupFilter.HighRisk), r => r.Process == "risk_report");
        Assert.Contains(ProcessPlanner.FilterRows(rows, ProcessGroupFilter.HighRisk), r => r.Process == "explorer");
    }

    [Fact]
    public void DefaultConfig_IncludesCommonChineseChatProcesses()
    {
        HashSet<string> targets = AppConfig.CreateDefault().TargetSet();

        Assert.Contains("Weixin", targets);
        Assert.Contains("TIM", targets);
        Assert.Contains("QQExternal", targets);
        Assert.Contains("TXPlatform", targets);
        Assert.Contains("WeChatAppEx", targets);
    }

    [Fact]
    public void FilterExecutableTargets_UsesCurrentConfigToBlockProtectedOrRemovedTargets()
    {
        var records = new[]
        {
            Record("Weixin", ProcessPlanner.ActionGraceful, memoryMb: 380, riskScore: 25, hasWindow: true, status: "candidate"),
            Record("TIM", ProcessPlanner.ActionGraceful, memoryMb: 220, riskScore: 30, hasWindow: true, status: "candidate"),
            Record("helper", ProcessPlanner.ActionForce, memoryMb: 20, riskScore: 20, hasWindow: false, status: "candidate")
        };
        var config = new AppConfig
        {
            targetNames = new[] { "Weixin" },
            protectedNames = new[] { "Weixin" },
            forceAllowedNames = new[] { "helper" }
        };

        List<ProcessRecord> executable = ProcessPlanner.FilterExecutableTargets(records, config);

        Assert.DoesNotContain(executable, r => r.ProcessName == "Weixin");
        Assert.DoesNotContain(executable, r => r.ProcessName == "TIM");
        Assert.Contains(executable, r => r.ProcessName == "helper");
    }

    [Fact]
    public void MatchesPrimaryIdentity_DoesNotTreatPathOnlyChineseMatchAsPreviewMatch()
    {
        ProcessGroupRow crashpad = ProcessPlanner.GroupRows(new[]
        {
            Record("crashpad_handler", ProcessPlanner.ActionForce, memoryMb: 20, riskScore: 20, hasWindow: false, path: @"D:\微信\Weixin\crashpad_handler.exe")
        }).Single();
        ProcessGroupRow weixin = ProcessPlanner.GroupRows(new[]
        {
            Record("Weixin", ProcessPlanner.ActionReport, memoryMb: 320, riskScore: 80, hasWindow: true, path: @"D:\微信\Weixin\Weixin.exe")
        }).Single();

        Assert.False(ProcessPlanner.MatchesPrimaryIdentity(crashpad, "微信"));
        Assert.True(ProcessPlanner.MatchesPrimaryIdentity(crashpad, "crashpad"));
        Assert.True(ProcessPlanner.MatchesPrimaryIdentity(weixin, "Weixin"));
    }

    private static ProcessRecord Record(string name, string action, long memoryMb, int riskScore, bool hasWindow, string path = "", string status = "candidate")
    {
        return new ProcessRecord
        {
            Id = Math.Abs(HashCode.Combine(name, action, memoryMb, riskScore, hasWindow)),
            ProcessName = name,
            Action = action,
            Reason = "test",
            MemoryMb = memoryMb,
            RiskScore = riskScore,
            HasWindow = hasWindow,
            Path = path,
            IsHighRisk = riskScore >= RiskCalculator.HighRiskScoreThreshold,
            Status = status
        };
    }
}

public sealed class ProcessGroupSearchTextTests
{
    [Fact]
    public void Build_UsesRawChildFields()
    {
        var row = new ProcessGroupRow
        {
            Process = "Weixin",
            Count = 1,
            Action = ProcessPlanner.ActionGraceful,
            Status = "candidate",
            Note = "target",
            Path = @"D:\Weixin\Weixin.exe",
            Children = new List<ProcessRecord>
            {
                new ProcessRecord
                {
                    Id = 1234,
                    ProcessName = "Weixin",
                    Action = ProcessPlanner.ActionGraceful,
                    Status = "candidate",
                    MainWindowTitle = "Wechat Main",
                    Path = @"D:\Weixin\Weixin.exe",
                    MemoryMb = 256,
                    RiskScore = 20
                }
            }
        };

        string searchText = ProcessGroupSearchText.Build(row);

        Assert.Contains("wechat main", searchText);
        Assert.Contains("1234", searchText);
    }
}

public sealed class AppConfigPathResolverTests
{
    [Fact]
    public void EnsureUserConfig_CopiesTemplateToLocalAppDataDirectory()
    {
        string temp = Path.Combine(Path.GetTempPath(), "oneclickclose-tests", Guid.NewGuid().ToString("N"));
        string appDir = Path.Combine(temp, "app");
        string localRoot = Path.Combine(temp, "local");
        Directory.CreateDirectory(appDir);

        string templatePath = Path.Combine(appDir, AppConfigPathResolver.DefaultConfigFileName);
        AppConfig.Save(templatePath, new AppConfig
        {
            targetNames = new[] { "Weixin" },
            protectedNames = Array.Empty<string>(),
            forceAllowedNames = Array.Empty<string>()
        });

        string resolved = AppConfigPathResolver.EnsureUserConfig(appDir, localRoot);

        Assert.Equal(Path.Combine(localRoot, "OneClickClose", AppConfigPathResolver.DefaultConfigFileName), resolved);
        Assert.True(File.Exists(resolved));
        Assert.Contains("Weixin", AppConfig.Load(resolved).TargetSet());
    }

    [Fact]
    public void EnsureUserConfig_DoesNotOverwriteExistingUserConfig()
    {
        string temp = Path.Combine(Path.GetTempPath(), "oneclickclose-tests", Guid.NewGuid().ToString("N"));
        string appDir = Path.Combine(temp, "app");
        string localRoot = Path.Combine(temp, "local");
        string userDir = Path.Combine(localRoot, "OneClickClose");
        Directory.CreateDirectory(appDir);
        Directory.CreateDirectory(userDir);

        AppConfig.Save(Path.Combine(appDir, AppConfigPathResolver.DefaultConfigFileName), new AppConfig
        {
            targetNames = new[] { "TemplateOnly" },
            protectedNames = Array.Empty<string>(),
            forceAllowedNames = Array.Empty<string>()
        });
        string existingPath = Path.Combine(userDir, AppConfigPathResolver.DefaultConfigFileName);
        AppConfig.Save(existingPath, new AppConfig
        {
            targetNames = new[] { "UserOnly" },
            protectedNames = Array.Empty<string>(),
            forceAllowedNames = Array.Empty<string>()
        });

        string resolved = AppConfigPathResolver.EnsureUserConfig(appDir, localRoot);

        Assert.Equal(existingPath, resolved);
        HashSet<string> targets = AppConfig.Load(resolved).TargetSet();
        Assert.Contains("UserOnly", targets);
        Assert.DoesNotContain("TemplateOnly", targets);
    }
}

public sealed class TemperatureProviderTests
{
    [Fact]
    public async Task CaptureExtendedMetrics_UsesLibreHardwareTemperatureWhenAvailable()
    {
        var monitor = new SystemMonitor(new FakeTemperatureProvider(new HardwareTemperatureReading
        {
            CpuTemperatureC = 61,
            GpuTemperatureC = 55,
            MotherboardTemperatureC = 42,
            Source = "LibreHardwareMonitor"
        }), () => null);

        SystemSnapshot snapshot = await monitor.CaptureExtendedMetricsAsync(new SystemSnapshot());

        Assert.Equal(61, snapshot.CpuTemperatureC);
        Assert.Equal(55, snapshot.GpuTemperatureC);
        Assert.Equal(42, snapshot.MotherboardTemperatureC);
        Assert.Equal(61, snapshot.TemperatureC);
        Assert.Equal("LibreHardwareMonitor", snapshot.TemperatureSource);
        Assert.Null(snapshot.TemperatureUnavailableReason);
    }

    [Fact]
    public async Task CaptureExtendedMetrics_FallsBackToWmiWhenLibreHasNoSensors()
    {
        var monitor = new SystemMonitor(new FakeTemperatureProvider(new HardwareTemperatureReading
        {
            Source = "LibreHardwareMonitor",
            UnavailableReason = "未检测到温度传感器"
        }), () => 44);

        SystemSnapshot snapshot = await monitor.CaptureExtendedMetricsAsync(new SystemSnapshot());

        Assert.Equal(44, snapshot.CpuTemperatureC);
        Assert.Equal(44, snapshot.TemperatureC);
        Assert.Equal("WMI", snapshot.TemperatureSource);
        Assert.Null(snapshot.TemperatureUnavailableReason);
    }

    [Fact]
    public async Task CaptureExtendedMetrics_ReturnsUnavailableReasonWhenNoProviderHasData()
    {
        var monitor = new SystemMonitor(new FakeTemperatureProvider(new HardwareTemperatureReading
        {
            Source = "LibreHardwareMonitor",
            UnavailableReason = "未授权，可能需要管理员权限"
        }), () => null);

        SystemSnapshot snapshot = await monitor.CaptureExtendedMetricsAsync(new SystemSnapshot());

        Assert.Null(snapshot.TemperatureC);
        Assert.Equal("LibreHardwareMonitor", snapshot.TemperatureSource);
        Assert.Equal("未授权，可能需要管理员权限", snapshot.TemperatureUnavailableReason);
    }

    [Fact]
    public async Task CaptureExtendedMetrics_HandlesProviderExceptionAsUnavailable()
    {
        var monitor = new SystemMonitor(new ThrowingTemperatureProvider(), () => null);

        SystemSnapshot snapshot = await monitor.CaptureExtendedMetricsAsync(new SystemSnapshot());

        Assert.Null(snapshot.TemperatureC);
        Assert.Equal("LibreHardwareMonitor", snapshot.TemperatureSource);
        Assert.Contains("boom", snapshot.TemperatureUnavailableReason);
    }

    private sealed class FakeTemperatureProvider : IHardwareTemperatureProvider
    {
        private readonly HardwareTemperatureReading _reading;

        public FakeTemperatureProvider(HardwareTemperatureReading reading)
        {
            _reading = reading;
        }

        public HardwareTemperatureReading ReadTemperatures() => _reading;
    }

    private sealed class ThrowingTemperatureProvider : IHardwareTemperatureProvider
    {
        public HardwareTemperatureReading ReadTemperatures() => throw new InvalidOperationException("boom");
    }
}

public sealed class UserPreferencesStoreTests
{
    [Fact]
    public void BuildSuggestions_RepeatedConfirmedCloseCreatesCloseSuggestion()
    {
        var store = NewStore();
        var record = PreferenceRecord("browser");

        store.RecordCloseConfirmed(new[] { record, record, record });

        var suggestion = Assert.Single(store.BuildSuggestions(EmptyConfig()), s => s.Type == "习惯关闭");
        Assert.Equal("browser", suggestion.ProcessName);
    }

    [Fact]
    public void BuildSuggestions_RepeatedSkipCreatesProtectionSuggestion()
    {
        var store = NewStore();

        store.IncrementManualRemove("chat");
        store.IncrementManualRemove("chat");
        store.IncrementManualRemove("chat");

        var suggestion = Assert.Single(store.BuildSuggestions(EmptyConfig()), s => s.Type == "保护名单");
        Assert.Equal("chat", suggestion.ProcessName);
    }

    [Fact]
    public void IgnoreSuggestion_PreventsRepeatedCloseSuggestion()
    {
        var store = NewStore();
        var record = PreferenceRecord("browser");
        store.RecordCloseConfirmed(new[] { record, record, record });
        var suggestion = Assert.Single(store.BuildSuggestions(EmptyConfig()), s => s.Type == "习惯关闭");

        store.IgnoreSuggestion(suggestion);

        Assert.DoesNotContain(store.BuildSuggestions(EmptyConfig()), s => s.Type == "习惯关闭" && s.ProcessName == "browser");
    }

    private static UserPreferencesStore NewStore()
    {
        string dir = Path.Combine(Path.GetTempPath(), "oneclickclose-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return new UserPreferencesStore(dir, new UserPreferencesData(), new CleanupHistoryDocument());
    }

    private static AppConfig EmptyConfig()
    {
        return new AppConfig
        {
            targetNames = Array.Empty<string>(),
            protectedNames = Array.Empty<string>(),
            forceAllowedNames = Array.Empty<string>()
        };
    }

    private static ProcessRecord PreferenceRecord(string name)
    {
        return new ProcessRecord
        {
            Id = Math.Abs(HashCode.Combine(name)),
            ProcessName = name,
            Action = ProcessPlanner.ActionGraceful,
            Reason = "test",
            MemoryMb = 128,
            RiskScore = 20
        };
    }
}
public sealed class ClosePlanPreviewTests
{
    [Fact]
    public void FromPlan_SummarizesForceReportAndHighRiskItems()
    {
        var plan = new ClosePlan
        {
            Config = new AppConfig
            {
                forceAfterGracefulFailure = true,
                forceAllowedNames = new[] { "helper" }
            },
            Candidates = new List<ProcessRecord>
            {
                Record("browser", ProcessPlanner.ActionGraceful, memoryMb: 512, riskScore: 20, isAutoDetected: true, isUserPath: true, path: @"C:\Users\me\AppData\Local\browser.exe"),
                Record("helper", ProcessPlanner.ActionForce, memoryMb: 64, riskScore: 40),
                Record("systemish", ProcessPlanner.ActionReport, memoryMb: 10, riskScore: 90, isHighRisk: true)
            },
            Protected = new List<ProcessRecord>(),
            Skipped = new List<ProcessRecord>()
        };

        ClosePlanPreview preview = ClosePlanPreview.FromPlan(plan);

        Assert.Equal(3, preview.TotalCandidates);
        Assert.Equal(1, preview.GracefulCount);
        Assert.Equal(1, preview.ExplicitForceCount);
        Assert.Equal(1, preview.PossibleAutoForceCount);
        Assert.Equal(1, preview.ReportOnlyCount);
        Assert.Equal(1, preview.HighRiskCount);
        Assert.True(preview.HasForceRisk);
        Assert.Contains("\u53ef\u80fd\u81ea\u52a8\u5f3a\u5236\uff1a1 \u4e2a", preview.ToDialogMessage());
        Assert.Contains("\u672a\u4fdd\u5b58\u6570\u636e", preview.ToDialogMessage());
    }

    private static ProcessRecord Record(
        string name,
        string action,
        long memoryMb,
        int riskScore,
        bool isAutoDetected = false,
        bool isUserPath = false,
        bool isHighRisk = false,
        string path = "")
    {
        return new ProcessRecord
        {
            Id = Math.Abs(HashCode.Combine(name, action, memoryMb, riskScore)),
            ProcessName = name,
            Action = action,
            MemoryMb = memoryMb,
            RiskScore = riskScore,
            IsAutoDetected = isAutoDetected,
            IsUserPath = isUserPath,
            IsHighRisk = isHighRisk,
            Path = path,
            Reason = "test"
        };
    }
}
