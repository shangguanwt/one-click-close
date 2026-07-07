using OneClickClose.Core;

namespace OneClickClose.Core.Tests;

public sealed class ThemePaletteTests
{
    [Fact]
    public void ForTheme_ReturnsDistinctWindowBorderColors()
    {
        AppThemePalette light = AppThemePalette.ForTheme(lightTheme: true);
        AppThemePalette dark = AppThemePalette.ForTheme(lightTheme: false);

        Assert.Equal(0x00FBF7F4u, light.WindowBorderColorBgr);
        Assert.Equal(0x00120803u, dark.WindowBorderColorBgr);
    }

    [Fact]
    public void ForTheme_ContainsStableUniqueBrushKeys()
    {
        AppThemePalette light = AppThemePalette.ForTheme(lightTheme: true);
        AppThemePalette dark = AppThemePalette.ForTheme(lightTheme: false);

        Assert.Equal(dark.Brushes.Count, dark.Brushes.Select(b => b.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(light.Brushes, b => b.Key == "SidebarNavPressedBrush" && b.Color.R == 0xC0 && b.Color.G == 0xD5 && b.Color.B == 0xEA);
        Assert.Contains(light.Brushes, b => b.Key == "SidebarNavPressedForegroundBrush" && b.Color.R == 0x14 && b.Color.G == 0x20 && b.Color.B == 0x33);
        Assert.Contains(dark.Brushes, b => b.Key == "BaseBrush" && b.Color.R == 0x03 && b.Color.G == 0x08 && b.Color.B == 0x12);
        Assert.Contains(dark.Brushes, b => b.Key == "ContentDialogBackground" && b.Color.R == 0x1A && b.Color.G == 0x1E && b.Color.B == 0x22);
        Assert.Contains(dark.Brushes, b => b.Key == "ScrollBarTrackBackground" && b.Color.A == 0x00);
        Assert.Contains(dark.Brushes, b => b.Key == "SidebarNavHoverBrush" && b.Color.R == 0x13 && b.Color.G == 0x2A && b.Color.B == 0x44);
        Assert.Contains(dark.Brushes, b => b.Key == "SidebarNavSelectedForegroundBrush" && b.Color.R == 0xFF && b.Color.G == 0xFF && b.Color.B == 0xFF);
    }

    [Fact]
    public void ForTheme_ContainsStableGradientKeys()
    {
        AppThemePalette light = AppThemePalette.ForTheme(lightTheme: true);

        Assert.Contains(light.Gradients, g => g.Key == "WindowGradientBrush" && g.Stops.Count == 3);
        Assert.Contains(light.Gradients, g => g.Key == "CardGradientBrush" && g.Stops.Count == 2);
    }
}
