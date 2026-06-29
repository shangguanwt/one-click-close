using System;
using System.Collections.Generic;
using System.IO;
using OneClickClose.Core.Helpers;

namespace OneClickClose.Core
{
    public sealed class AppConfig
    {
        public int waitSeconds { get; set; }
        public int gracefulTimeoutSeconds { get; set; }
        public int queryTimeoutSeconds { get; set; }
        public string[] targetNames { get; set; }
        public string[] protectedNames { get; set; }
        public string[] forceAllowedNames { get; set; }

        public static AppConfig Load(string path)
        {
            EnsureConfig(path);
            AppConfig config = JsonFileStore.ReadJson<AppConfig>(path) ?? new AppConfig();
            return Normalize(config);
        }

        public static void Save(string path, AppConfig config)
        {
            AppConfig normalized = Normalize(config);
            JsonFileStore.WriteJson(path, normalized);
        }

        public static void EnsureConfig(string path)
        {
            if (File.Exists(path))
            {
                return;
            }

            JsonFileStore.WriteJson(path, CreateDefault());
        }

        public HashSet<string> TargetSet()
        {
            return JsonFileStore.MakeSet(targetNames);
        }

        public HashSet<string> ProtectedSet()
        {
            return JsonFileStore.MakeSet(protectedNames);
        }

        public HashSet<string> ForceSet()
        {
            return JsonFileStore.MakeSet(forceAllowedNames);
        }

        private static AppConfig Normalize(AppConfig config)
        {
            if (config == null)
            {
                config = CreateDefault();
            }

            if (config.waitSeconds <= 0)
            {
                config.waitSeconds = 5;
            }

            if (config.gracefulTimeoutSeconds <= 0)
            {
                config.gracefulTimeoutSeconds = config.waitSeconds > 0 ? config.waitSeconds : 5;
            }

            if (config.queryTimeoutSeconds <= 0)
            {
                config.queryTimeoutSeconds = 3;
            }

            if (config.targetNames == null)
            {
                config.targetNames = new string[0];
            }

            if (config.protectedNames == null)
            {
                config.protectedNames = new string[0];
            }

            if (config.forceAllowedNames == null)
            {
                config.forceAllowedNames = new string[0];
            }

            return config;
        }

        public static AppConfig CreateDefault()
        {
            return new AppConfig
            {
                waitSeconds = 5,
                gracefulTimeoutSeconds = 5,
                queryTimeoutSeconds = 3,
                targetNames = new[]
                {
                    "chrome", "msedge", "firefox", "Telegram", "Discord",
                    "Spotify", "Slack", "notepad", "Code"
                },
                protectedNames = new[]
                {
                    "ApplicationFrameHost", "audiodg", "conhost", "csrss", "ctfmon",
                    "DataExchangeHost", "dllhost", "dwm", "explorer", "fontdrvhost",
                    "Idle", "LockApp", "LsaIso", "lsass", "Memory Compression",
                    "MoUsoCoreWorker", "msedgewebview2", "Registry", "RuntimeBroker",
                    "SearchFilterHost", "SearchHost", "SearchIndexer",
                    "SearchProtocolHost", "Secure System", "SecurityHealthService",
                    "SecurityHealthSystray", "services", "SgrmBroker",
                    "ShellExperienceHost", "sihost", "smss", "spoolsv",
                    "StartMenuExperienceHost", "svchost", "System", "SystemSettings",
                    "SystemSettingsBroker", "taskhostw", "TextInputHost", "unsecapp",
                    "vmcompute", "vmms", "Widgets", "WidgetService", "wininit",
                    "winlogon", "WmiApSrv", "WmiPrvSE", "WUDFHost"
                },
                forceAllowedNames = new[]
                {
                    "crashpad_handler"
                }
            };
        }
    }
}
