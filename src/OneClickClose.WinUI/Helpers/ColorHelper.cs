using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OneClickClose.WinUI.Helpers;

/// <summary>
/// C# side color helpers backed by Theme.xaml resources.
/// </summary>
public static class ColorHelper
{
    public static SolidColorBrush Safe =>
        (SolidColorBrush)Application.Current.Resources["SafeBrush"];

    public static SolidColorBrush SafeGreenSoft =>
        (SolidColorBrush)Application.Current.Resources["SafeSoftBrush"];

    public static SolidColorBrush Copper =>
        (SolidColorBrush)Application.Current.Resources["CopperBrush"];

    public static SolidColorBrush CopperSoft =>
        (SolidColorBrush)Application.Current.Resources["CopperSoftBrush"];

    public static SolidColorBrush Danger =>
        (SolidColorBrush)Application.Current.Resources["DangerBrush"];

    public static SolidColorBrush DangerSoft =>
        (SolidColorBrush)Application.Current.Resources["DangerSoftBrush"];

    public static SolidColorBrush MutedSoft =>
        (SolidColorBrush)Application.Current.Resources["MutedSoftBrush"];

    public static SolidColorBrush Accent =>
        (SolidColorBrush)Application.Current.Resources["AccentBrush"];

    public static SolidColorBrush AccentLight =>
        (SolidColorBrush)Application.Current.Resources["AccentLightBrush"];

    public static SolidColorBrush Purple =>
        (SolidColorBrush)Application.Current.Resources["PurpleBrush"];

    public static SolidColorBrush PurpleSoft =>
        (SolidColorBrush)Application.Current.Resources["PurpleSoftBrush"];

    public static SolidColorBrush CyanSoft =>
        (SolidColorBrush)Application.Current.Resources["CyanSoftBrush"];

    public static SolidColorBrush Info =>
        (SolidColorBrush)Application.Current.Resources["SubtleTextBrush"];

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
}
