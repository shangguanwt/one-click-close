using System;
using System.IO;

namespace OneClickClose.Core
{
    /// <summary>
    /// Risk scoring and path classification logic extracted from ProcessPlanner.
    /// </summary>
    internal static class RiskCalculator
    {
        // ── Risk scoring constants ───────────────────────────────────
        private const int RiskBaseScore = 55;
        private const int RiskWindowPresentBonus = -25;
        private const int RiskNoWindowPenalty = 15;
        private const int RiskUserLaunchedBonus = -15;
        private const int RiskUserPathBonus = -20;
        private const int RiskSystemPathPenalty = 45;
        private const int RiskHighMemoryBonus = -10;
        private const int RiskLowMemoryPenalty = 10;
        private const int RiskProtectedNamePenalty = 25;
        private const int RiskForceAllowedBonus = -10;
        private const int RiskScoreMin = 0;
        private const int RiskScoreMax = 100;
        private const int HighMemoryThresholdMb = 512;
        private const int LowMemoryThresholdMb = 64;

        /// <summary>
        /// Processes with a risk score at or above this threshold are considered high-risk.
        /// </summary>
        internal const int HighRiskScoreThreshold = 75;

        internal static int ComputeRiskScore(bool hasWindow, bool userLaunched, bool userPath, bool systemPath, long memoryMb, bool protectedName, bool forceAllowed)
        {
            int score = RiskBaseScore;
            score += hasWindow ? RiskWindowPresentBonus : RiskNoWindowPenalty;
            score += userLaunched ? RiskUserLaunchedBonus : 0;
            score += userPath ? RiskUserPathBonus : 0;
            score += systemPath ? RiskSystemPathPenalty : 0;
            score += memoryMb >= HighMemoryThresholdMb ? RiskHighMemoryBonus : 0;
            score += memoryMb > 0 && memoryMb <= LowMemoryThresholdMb ? RiskLowMemoryPenalty : 0;
            score += protectedName ? RiskProtectedNamePenalty : 0;
            score += forceAllowed ? RiskForceAllowedBonus : 0;
            return Math.Max(RiskScoreMin, Math.Min(RiskScoreMax, score));
        }

        internal static bool IsSystemPath(string path)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return StartsWithFolder(path, windows);
        }

        internal static bool IsUserPath(string path)
        {
            return StartsWithFolder(path, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))
                || StartsWithFolder(path, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86))
                || StartsWithFolder(path, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
                || StartsWithFolder(path, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        }

        private static bool StartsWithFolder(string path, string folder)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(folder))
            {
                return false;
            }

            string fullFolder = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase);
        }
    }
}
