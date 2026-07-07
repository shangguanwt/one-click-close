using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneClickClose.Core.Helpers;
using Windows.UI;

namespace OneClickClose.WinUI.Services;

public static class AppAccentColorService
{
    private const string DefaultAccentId = "blue";
    private static string _currentAccentId;

    public static IReadOnlyList<AccentColorOption> Options { get; } = new[]
    {
        Option("blue", "蓝色", 0xFF, 0x2F, 0x7B, 0xFF, 0xFF, 0x55, 0xC7, 0xFF, 0xFF, 0x8B, 0x5C, 0xF6),
        Option("purple", "紫色", 0xFF, 0x8B, 0x5C, 0xF6, 0xFF, 0xB7, 0x8C, 0xFF, 0xFF, 0x55, 0xC7, 0xFF),
        Option("cyan", "青色", 0xFF, 0x38, 0xBD, 0xF8, 0xFF, 0x67, 0xE8, 0xF9, 0xFF, 0x2F, 0x7B, 0xFF),
        Option("green", "绿色", 0xFF, 0x38, 0xE0, 0x7B, 0xFF, 0x74, 0xF2, 0xA8, 0xFF, 0x22, 0xC5, 0x5E),
        Option("orange", "橙色", 0xFF, 0xF5, 0x9E, 0x0B, 0xFF, 0xFB, 0xC2, 0x48, 0xFF, 0xFF, 0x7A, 0x3D),
        Option("red", "红色", 0xFF, 0xFF, 0x52, 0x52, 0xFF, 0xFF, 0x8A, 0x8A, 0xFF, 0xF5, 0x9E, 0x0B),
    };

    public static string CurrentAccentId
    {
        get
        {
            EnsureLoaded();
            return _currentAccentId;
        }
    }

    public static AccentColorOption CurrentOption => FindOption(CurrentAccentId);

    public static void ApplyCurrent(bool persist = false)
    {
        Apply(CurrentAccentId, persist);
    }

    public static AccentColorOption Apply(string accentId, bool persist = true)
    {
        AccentColorOption option = FindOption(accentId);
        _currentAccentId = option.Id;

        ApplyResources(option);

        if (persist)
        {
            SaveSettings(option.Id);
        }

        AppThemeService.NotifyThemeResourcesChanged();
        return option;
    }

    private static void EnsureLoaded()
    {
        if (!string.IsNullOrWhiteSpace(_currentAccentId))
        {
            return;
        }

        UiSettingsData settings = JsonFileStore.ReadJson<UiSettingsData>(SettingsPath);
        _currentAccentId = FindOption(settings?.AccentColor).Id;
    }

    private static AccentColorOption FindOption(string accentId)
    {
        return Options.FirstOrDefault(option => string.Equals(option.Id, accentId, StringComparison.OrdinalIgnoreCase))
            ?? Options.First(option => option.Id == DefaultAccentId);
    }

    private static void ApplyResources(AccentColorOption option)
    {
        Color primary = option.Primary;
        Color light = option.Light;
        Color secondary = option.Secondary;
        Color soft = WithAlpha(0x1A, primary);
        Color pressed = Blend(primary, Color(0xFF, 0x00, 0x00, 0x00), 0.24);

        SetBrush("AccentBrush", primary);
        SetBrush("PrimaryBrush", primary);
        SetBrush("AccentLightBrush", light);
        SetBrush("CyanSoftBrush", soft);
        SetBrush("OnAccentSoftBrush", WithAlpha(0x40, 0xFF, 0xFF, 0xFF));

        SetBrush("TextControlBorderBrushFocused", primary);
        SetBrush("TextControlSelectionHighlightColor", primary);
        SetBrush("TextControlSelectionHighlightColors", primary);
        SetBrush("ContentDialogButtonBackground", primary);
        SetBrush("ToggleSwitchOnForeground", primary);
        SetBrush("ToggleSwitchTrackOnForeground", primary);
        SetBrush("ToggleSwitchFillOn", primary);
        SetBrush("ToggleSwitchFillOnPointerOver", light);
        SetBrush("ToggleSwitchFillOnPressed", pressed);
        SetBrush("ToggleSwitchStrokeOn", primary);
        SetBrush("ToggleSwitchStrokeOnPointerOver", light);
        SetBrush("ToggleSwitchStrokeOnPressed", pressed);
        SetBrush("ToggleSwitchKnobFillOn", WithAlpha(0xFF, 0xFF, 0xFF, 0xFF));
        SetBrush("ToggleSwitchKnobFillOnPointerOver", WithAlpha(0xFF, 0xFF, 0xFF, 0xFF));
        SetBrush("ToggleSwitchKnobFillOnPressed", WithAlpha(0xFF, 0xFF, 0xFF, 0xFF));
        SetBrush("NavigationViewSelectionIndicatorForeground", primary);
        SetBrush("ScrollBarThumbHoverBackground", primary);
        SetBrush("ScrollBarThumbPressedBackground", pressed);
        SetBrush("AccentButtonBackground", primary);
        SetBrush("AccentButtonBackgroundPointerOver", light);
        SetBrush("AccentButtonBackgroundPressed", pressed);
        SetBrush("AccentButtonBorderBrush", primary);
        SetBrush("AccentButtonBorderBrushPointerOver", light);
        SetBrush("AccentButtonBorderBrushPressed", pressed);

        SetGradient("PrimaryGradientBrush", light, primary, secondary);
        SetGradient("MemoryRingGradientBrush", light, primary, secondary);
        SetGradient("SidebarSelectedBrush", primary, Blend(primary, Color(0xFF, 0x03, 0x08, 0x12), 0.38));

    }

    private static void SetBrush(string key, Color color)
    {
        AppThemeService.SetBrushResource(key, color);
    }

    private static void SetGradient(string key, params Color[] colors)
    {
        AppThemeService.SetGradientResource(key, colors);
    }

    private static void SaveSettings(string accentId)
    {
        JsonFileStore.WriteJson(SettingsPath, new UiSettingsData { AccentColor = accentId });
    }

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OneClickClose", "ui-settings.json");

    private static AccentColorOption Option(
        string id,
        string name,
        byte primaryA,
        byte primaryR,
        byte primaryG,
        byte primaryB,
        byte lightA,
        byte lightR,
        byte lightG,
        byte lightB,
        byte secondaryA,
        byte secondaryR,
        byte secondaryG,
        byte secondaryB)
    {
        return new AccentColorOption(
            id,
            name,
            Color(primaryA, primaryR, primaryG, primaryB),
            Color(lightA, lightR, lightG, lightB),
            Color(secondaryA, secondaryR, secondaryG, secondaryB));
    }

    private static Color Color(byte a, byte r, byte g, byte b)
    {
        return Windows.UI.Color.FromArgb(a, r, g, b);
    }

    private static Color WithAlpha(byte alpha, Color color)
    {
        return Windows.UI.Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static Color WithAlpha(byte alpha, byte r, byte g, byte b)
    {
        return Windows.UI.Color.FromArgb(alpha, r, g, b);
    }

    private static Color Blend(Color from, Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return Windows.UI.Color.FromArgb(
            (byte)Math.Round(from.A + (to.A - from.A) * amount),
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount));
    }

    private sealed class UiSettingsData
    {
        public string AccentColor { get; set; }
    }
}

public sealed record AccentColorOption(string Id, string Name, Color Primary, Color Light, Color Secondary);
