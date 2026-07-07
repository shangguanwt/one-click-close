using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace OneClickClose.WinUI.Helpers;

/// <summary>
/// C# side color helpers backed by Theme.xaml resources.
/// </summary>
public static class ColorHelper
{
    public static SolidColorBrush Safe =>
        Brush("SafeBrush", Color.FromArgb(255, 40, 165, 110));

    public static SolidColorBrush SafeGreenSoft =>
        Brush("SafeSoftBrush", Color.FromArgb(38, 40, 165, 110));

    public static SolidColorBrush Copper =>
        Brush("CopperBrush", Color.FromArgb(255, 190, 118, 48));

    public static SolidColorBrush CopperSoft =>
        Brush("CopperSoftBrush", Color.FromArgb(38, 190, 118, 48));

    public static SolidColorBrush Danger =>
        Brush("DangerBrush", Color.FromArgb(255, 210, 80, 80));

    public static SolidColorBrush DangerSoft =>
        Brush("DangerSoftBrush", Color.FromArgb(38, 210, 80, 80));

    public static SolidColorBrush MutedSoft =>
        Brush("MutedSoftBrush", Color.FromArgb(34, 100, 116, 139));

    public static SolidColorBrush BodyText =>
        Brush("BodyTextBrush", Color.FromArgb(255, 34, 43, 58));

    public static SolidColorBrush Accent =>
        Brush("AccentBrush", Color.FromArgb(255, 49, 122, 204));

    public static SolidColorBrush AccentLight =>
        Brush("AccentLightBrush", Color.FromArgb(255, 82, 151, 224));

    public static SolidColorBrush Purple =>
        Brush("PurpleBrush", Color.FromArgb(255, 126, 101, 228));

    public static SolidColorBrush PurpleSoft =>
        Brush("PurpleSoftBrush", Color.FromArgb(38, 126, 101, 228));

    public static SolidColorBrush CyanSoft =>
        Brush("CyanSoftBrush", Color.FromArgb(38, 46, 168, 216));

    public static SolidColorBrush Info =>
        Brush("SubtleTextBrush", Color.FromArgb(255, 100, 116, 139));

    public static SolidColorBrush GetActionBackground(string action)
    {
        if (action == Core.ProcessPlanner.ActionGraceful)
            return CyanSoft;
        if (action == Core.ProcessPlanner.ActionForce)
            return CopperSoft;
        if (action == Core.ProcessPlanner.ActionReport)
            return DangerSoft;
        return MutedSoft;
    }

    private static SolidColorBrush Brush(string key, Color fallback)
    {
        object value = Application.Current?.Resources?[key];
        return value as SolidColorBrush ?? new SolidColorBrush(fallback);
    }
}
