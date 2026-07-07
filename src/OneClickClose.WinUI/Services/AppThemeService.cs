using System;
using System.Collections.Generic;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace OneClickClose.WinUI.Services;

public static class AppThemeService
{
    public static event EventHandler ThemeChanged;

    public static AppThemePalette Apply(bool lightTheme, AppWindow appWindow)
    {
        AppThemePalette palette = AppThemePalette.ForTheme(lightTheme);
        ApplyResources(palette);
        ConfigureTitleBarButtons(appWindow, palette);
        NotifyThemeResourcesChanged();
        return palette;
    }

    private static void ApplyResources(AppThemePalette palette)
    {
        foreach (ThemeBrushToken token in palette.Brushes)
        {
            SetBrushResource(token.Key, token.Color);
        }

        foreach (ThemeGradientToken token in palette.Gradients)
        {
            SetGradientResource(token.Key, token.Stops);
        }
    }

    private static void ConfigureTitleBarButtons(AppWindow appWindow, AppThemePalette palette)
    {
        var titleBar = appWindow.TitleBar;
        titleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonForegroundColor = palette.TitleBar.Foreground;
        titleBar.ButtonInactiveForegroundColor = palette.TitleBar.InactiveForeground;
        titleBar.ButtonHoverBackgroundColor = palette.TitleBar.HoverBackground;
        titleBar.ButtonHoverForegroundColor = palette.TitleBar.HoverForeground;
        titleBar.ButtonPressedBackgroundColor = palette.TitleBar.PressedBackground;
        titleBar.ButtonPressedForegroundColor = palette.TitleBar.PressedForeground;
    }

    internal static void NotifyThemeResourcesChanged()
    {
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    internal static void SetBrushResource(string key, Color color)
    {
        object resource;
        try
        {
            resource = Application.Current.Resources[key];
        }
        catch
        {
            return;
        }

        if (resource is SolidColorBrush brush)
        {
            brush.Color = color;
            return;
        }

        if (Application.Current.Resources.ContainsKey(key))
        {
            Application.Current.Resources[key] = new SolidColorBrush(color);
        }
    }

    internal static void SetGradientResource(string key, IReadOnlyList<Color> colors)
    {
        object resource;
        try
        {
            resource = Application.Current.Resources[key];
        }
        catch
        {
            return;
        }

        if (resource is not LinearGradientBrush brush || colors.Count == 0)
        {
            return;
        }

        while (brush.GradientStops.Count < colors.Count)
        {
            brush.GradientStops.Add(new GradientStop());
        }

        for (int i = 0; i < colors.Count; i++)
        {
            brush.GradientStops[i].Color = colors[i];
        }
    }
}
