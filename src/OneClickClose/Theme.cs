using System.Drawing;

namespace OneClickClose
{
    /// <summary>
    /// Centralized theme colors for the entire application.
    /// Values synced with WinUI Theme.xaml — change both files together.
    /// </summary>
    internal static class Theme
    {
        // ── Backgrounds (synced with Theme.xaml BaseBrush/CardBrush/SurfaceBrush) ──
        public static readonly Color Background     = Color.FromArgb(15, 17, 19);   // #0F1113 BaseBrush
        public static readonly Color Sidebar        = Color.FromArgb(15, 17, 19);   // #0F1113 PaneBrush
        public static readonly Color SecondaryPanel = Color.FromArgb(30, 35, 41);   // #1E2329 HoverBrush
        public static readonly Color Card           = Color.FromArgb(26, 30, 34);   // #1A1E22 CardBrush
        public static readonly Color CardSoft       = Color.FromArgb(23, 26, 30);   // #171A1E SurfaceBrush

        // ── Row Colors ────────────────────────────────────────────
        public static readonly Color RowAlt  = Color.FromArgb(23, 26, 30);          // #171A1E SurfaceBrush
        public static readonly Color RowHover = Color.FromArgb(30, 35, 41);         // #1E2329 HoverBrush

        // ── Borders & Dividers ────────────────────────────────────
        public static readonly Color Border       = Color.FromArgb(38, 43, 48);     // #262B30 StrokeBrush
        public static readonly Color ButtonBorder = Color.FromArgb(58, 64, 71);     // #3A4047 StrokeHoverBrush
        public static readonly Color Divider      = Color.FromArgb(38, 43, 48);     // #262B30 DividerSolidBrush

        // ── Text (synced with Theme.xaml TitleTextBrush/BodyTextBrush/SubtleTextBrush) ──
        public static readonly Color Text      = Color.FromArgb(168, 172, 180);     // #A8ACB4 BodyTextBrush
        public static readonly Color Muted     = Color.FromArgb(107, 112, 121);     // #6B7079 SubtleTextBrush
        public static readonly Color TitleText = Color.FromArgb(240, 241, 243);     // #F0F1F3 TitleTextBrush

        // ── Accent (synced with Theme.xaml PrimaryBrush) ──────────
        public static readonly Color Primary     = Color.FromArgb(74, 144, 217);    // #4A90D9 PrimaryBrush
        public static readonly Color PrimaryHover = Color.FromArgb(107, 174, 236);  // #6BAEEC AccentLightBrush

        // ── Semantic Colors (synced with Theme.xaml) ──────────────
        public static readonly Color Danger      = Color.FromArgb(224, 80, 74);     // #E0504A DangerBrush
        public static readonly Color DangerHover = Color.FromArgb(192, 64, 58);     // #C0403A pressed
        public static readonly Color Protect     = Color.FromArgb(63, 191, 127);    // #3FBF7F SafeBrush
        public static readonly Color Force       = Color.FromArgb(224, 144, 64);    // #E09040 CopperBrush
        public static readonly Color Purple      = Color.FromArgb(145, 123, 218);   // kept (no WinUI equivalent)

        // ── Gradient (MiniMetric cards) ───────────────────────────
        public static readonly Color CardGradientTop    = Color.FromArgb(30, 35, 41);  // #1E2329 HoverBrush
        public static readonly Color CardGradientBottom = Color.FromArgb(23, 26, 30);  // #171A1E SurfaceBrush

        // ── NavItem ──────────────────────────────────────────────
        public static readonly Color NavHover   = Color.FromArgb(30, 35, 41);       // #1E2329 HoverBrush
        public static readonly Color NavActive  = Color.FromArgb(26, 30, 34);       // #1A1E22 CardBrush
        public static readonly Color NavPressed = Color.FromArgb(23, 26, 30);       // #171A1E SurfaceBrush
        public static readonly Color NavText    = Color.FromArgb(168, 172, 180);    // #A8ACB4 BodyTextBrush
        public static readonly Color NavIcon    = Color.FromArgb(107, 112, 121);    // #6B7079 SubtleTextBrush

        // ── TabBar ───────────────────────────────────────────────
        public static readonly Color TabIdle   = Color.FromArgb(23, 26, 30);        // #171A1E SurfaceBrush
        public static readonly Color TabActive = Color.FromArgb(26, 30, 34);        // #1A1E22 CardBrush
        public static readonly Color TabHover  = Color.FromArgb(30, 35, 41);        // #1E2329 HoverBrush
    }
}
