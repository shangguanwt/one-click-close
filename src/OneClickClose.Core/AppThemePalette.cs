using System.Collections.Generic;

namespace OneClickClose.Core;

public sealed class AppThemePalette
{
    public bool IsLightTheme { get; }
    public uint WindowBorderColorBgr { get; }
    public IReadOnlyList<ThemeBrushToken> Brushes { get; }
    public IReadOnlyList<ThemeGradientToken> Gradients { get; }
    public TitleBarPalette TitleBar { get; }

    private AppThemePalette(
        bool isLightTheme,
        uint windowBorderColorBgr,
        IReadOnlyList<ThemeBrushToken> brushes,
        IReadOnlyList<ThemeGradientToken> gradients,
        TitleBarPalette titleBar)
    {
        IsLightTheme = isLightTheme;
        WindowBorderColorBgr = windowBorderColorBgr;
        Brushes = brushes;
        Gradients = gradients;
        TitleBar = titleBar;
    }

    public static AppThemePalette ForTheme(bool lightTheme)
    {
        return lightTheme ? Light() : Dark();
    }

    private static AppThemePalette Light()
    {
        return new AppThemePalette(
            isLightTheme: true,
            windowBorderColorBgr: 0x00FBF7F4,
            brushes: new[]
            {
                Brush("BaseBrush", 0xFF, 0xF4, 0xF7, 0xFB),
                Brush("ContentBrush", 0xFF, 0xEE, 0xF3, 0xF8),
                Brush("SurfaceBrush", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("CardBrush", 0xFF, 0xF8, 0xFB, 0xFF),
                Brush("SurfaceElevatedBrush", 0xFF, 0xED, 0xF4, 0xFC),
                Brush("PaneBrush", 0xFF, 0xEA, 0xF1, 0xF8),
                Brush("HoverBrush", 0xFF, 0xE1, 0xEC, 0xF8),
                Brush("StrokeBrush", 0xFF, 0xC8, 0xD7, 0xE7),
                Brush("StrokeHoverBrush", 0xFF, 0xA9, 0xC0, 0xD8),
                Brush("DividerBrush", 0xFF, 0xD8, 0xE3, 0xEF),
                Brush("DividerSolidBrush", 0xFF, 0xC8, 0xD7, 0xE7),
                Brush("MutedSoftBrush", 0xFF, 0xE4, 0xEC, 0xF5),
                Brush("TitleTextBrush", 0xFF, 0x14, 0x20, 0x33),
                Brush("BodyTextBrush", 0xFF, 0x2C, 0x3A, 0x4E),
                Brush("MutedTextBrush", 0xFF, 0x58, 0x6A, 0x82),
                Brush("SubtleTextBrush", 0xFF, 0x74, 0x85, 0x9A),
                Brush("DisabledSurfaceBrush", 0xFF, 0xE3, 0xEB, 0xF5),
                Brush("DisabledStrokeBrush", 0xFF, 0xC8, 0xD7, 0xE7),
                Brush("DisabledTextBrush", 0xFF, 0x8A, 0x9B, 0xB1),
                Brush("ButtonPressedBrush", 0xFF, 0xE4, 0xEC, 0xF5),
                Brush("SidebarNavBackgroundBrush", 0x00, 0x00, 0x00, 0x00),
                Brush("SidebarNavHoverBrush", 0xFF, 0xDD, 0xEA, 0xF7),
                Brush("SidebarNavPressedBrush", 0xFF, 0xC0, 0xD5, 0xEA),
                Brush("SidebarNavForegroundBrush", 0xFF, 0x14, 0x20, 0x33),
                Brush("SidebarNavHoverForegroundBrush", 0xFF, 0x14, 0x20, 0x33),
                Brush("SidebarNavPressedForegroundBrush", 0xFF, 0x14, 0x20, 0x33),
                Brush("SidebarNavSelectedForegroundBrush", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("HeaderBarBrush", 0xFF, 0xF8, 0xFB, 0xFF),
                Brush("RowItemBrush", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("RowHoverBrush", 0xFF, 0xE1, 0xEC, 0xF8),
                Brush("InlineMutedBrush", 0xFF, 0x74, 0x85, 0x9A),
                Brush("EmptyIconBrush", 0xFF, 0xC8, 0xD7, 0xE7),
                Brush("ContentAreaBrush", 0xFF, 0xEE, 0xF3, 0xF8),
                Brush("PaneAreaBrush", 0xFF, 0xEA, 0xF1, 0xF8),
                Brush("TextControlBackground", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("TextControlBackgroundPointerOver", 0xFF, 0xF4, 0xF8, 0xFC),
                Brush("TextControlBackgroundFocused", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("TextControlForeground", 0xFF, 0x14, 0x20, 0x33),
                Brush("TextControlForegroundPointerOver", 0xFF, 0x14, 0x20, 0x33),
                Brush("TextControlForegroundFocused", 0xFF, 0x14, 0x20, 0x33),
                Brush("TextControlPlaceholderForeground", 0xFF, 0x74, 0x85, 0x9A),
                Brush("TextControlPlaceholderForegroundPointerOver", 0xFF, 0x58, 0x6A, 0x82),
                Brush("TextControlPlaceholderForegroundFocused", 0xFF, 0x58, 0x6A, 0x82),
                Brush("TextControlBorderBrush", 0xFF, 0xC8, 0xD7, 0xE7),
                Brush("TextControlBorderBrushPointerOver", 0xFF, 0xA9, 0xC0, 0xD8),
                Brush("TextControlBorderBrushFocused", 0xFF, 0x2F, 0x7B, 0xFF),
                Brush("ListViewItemBackgroundPointerOver", 0xFF, 0xE1, 0xEC, 0xF8),
                Brush("ListViewItemBackgroundSelected", 0xFF, 0xE8, 0xF0, 0xFA),
                Brush("ListViewItemBackgroundSelectedPointerOver", 0xFF, 0xDF, 0xEA, 0xF7),
                Brush("ListViewItemForeground", 0xFF, 0x2C, 0x3A, 0x4E),
                Brush("ListViewItemForegroundPointerOver", 0xFF, 0x14, 0x20, 0x33),
                Brush("ComboBoxBackground", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("ComboBoxBackgroundPointerOver", 0xFF, 0xF4, 0xF8, 0xFC),
                Brush("ComboBoxBorderBrush", 0xFF, 0xC8, 0xD7, 0xE7),
                Brush("ComboBoxForeground", 0xFF, 0x2C, 0x3A, 0x4E),
                Brush("ButtonBackground", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("ButtonBackgroundPointerOver", 0xFF, 0xEA, 0xF1, 0xF8),
                Brush("ButtonBackgroundPressed", 0xFF, 0xE4, 0xEC, 0xF5),
                Brush("ButtonBackgroundDisabled", 0xFF, 0xE3, 0xEB, 0xF5),
                Brush("ButtonBorderBrush", 0xFF, 0xC8, 0xD7, 0xE7),
                Brush("ButtonBorderBrushPointerOver", 0xFF, 0xA9, 0xC0, 0xD8),
                Brush("ButtonBorderBrushPressed", 0xFF, 0x9B, 0xB4, 0xD0),
                Brush("ButtonBorderBrushDisabled", 0xFF, 0xD6, 0xE1, 0xEC),
                Brush("ButtonForeground", 0xFF, 0x2C, 0x3A, 0x4E),
                Brush("ButtonForegroundPointerOver", 0xFF, 0x14, 0x20, 0x33),
                Brush("ButtonForegroundPressed", 0xFF, 0x14, 0x20, 0x33),
                Brush("ButtonForegroundDisabled", 0xFF, 0x8A, 0x9B, 0xB1),
                Brush("AccentButtonForegroundDisabled", 0xFF, 0x6B, 0x7C, 0x92),
                Brush("AccentButtonBackgroundDisabled", 0xFF, 0xD6, 0xE1, 0xEC),
                Brush("AccentButtonBorderBrushDisabled", 0xFF, 0xD6, 0xE1, 0xEC),
                Brush("ExpanderHeaderBackground", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("ExpanderHeaderBackgroundPointerOver", 0xFF, 0xED, 0xF4, 0xFC),
                Brush("ExpanderHeaderBackgroundPressed", 0xFF, 0xE4, 0xEC, 0xF5),
                Brush("ExpanderHeaderForeground", 0xFF, 0x14, 0x20, 0x33),
                Brush("ExpanderHeaderBorderBrush", 0xFF, 0xC8, 0xD7, 0xE7),
                Brush("ExpanderContentBackground", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("ExpanderContentForeground", 0xFF, 0x2C, 0x3A, 0x4E),
                Brush("ContentDialogBackground", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("ContentDialogBorderBrush", 0xFF, 0xC8, 0xD7, 0xE7),
                Brush("ContentDialogTitleForeground", 0xFF, 0x14, 0x20, 0x33),
                Brush("ContentDialogForeground", 0xFF, 0x2C, 0x3A, 0x4E),
                Brush("ContentDialogSeparatorBrush", 0xFF, 0xD8, 0xE3, 0xEF),
                Brush("ContentDialogCloseButtonBackground", 0xFF, 0xEA, 0xF1, 0xF8),
                Brush("ContentDialogCloseButtonForeground", 0xFF, 0x58, 0x6A, 0x82),
                Brush("ContentDialogButtonBackground", 0xFF, 0x2F, 0x7B, 0xFF),
                Brush("ContentDialogButtonForeground", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("ContentDialogButtonBorderBrush", 0x00, 0x00, 0x00, 0x00),
                Brush("ToggleSwitchOnForeground", 0xFF, 0x2F, 0x7B, 0xFF),
                Brush("ToggleSwitchForeground", 0xFF, 0x74, 0x85, 0x9A),
                Brush("ToggleSwitchTrackOnForeground", 0xFF, 0x2F, 0x7B, 0xFF),
                Brush("ToggleSwitchTrackForeground", 0xFF, 0xD8, 0xE3, 0xEF),
                Brush("ToggleSwitchThumbForeground", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("ToggleSwitchThumbOnForeground", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("NavigationViewItemBackground", 0x00, 0x00, 0x00, 0x00),
                Brush("NavigationViewItemBackgroundPointerOver", 0xFF, 0xE1, 0xEC, 0xF8),
                Brush("NavigationViewItemBackgroundPressed", 0xFF, 0xD8, 0xE3, 0xEF),
                Brush("NavigationViewItemBackgroundSelected", 0xFF, 0xE8, 0xF0, 0xFA),
                Brush("NavigationViewItemForeground", 0xFF, 0x58, 0x6A, 0x82),
                Brush("NavigationViewItemForegroundPointerOver", 0xFF, 0x14, 0x20, 0x33),
                Brush("NavigationViewItemForegroundSelected", 0xFF, 0x14, 0x20, 0x33),
                Brush("NavigationViewSelectionIndicatorForeground", 0xFF, 0x2F, 0x7B, 0xFF),
                Brush("NavigationViewContentBackground", 0xFF, 0xEE, 0xF3, 0xF8),
                Brush("NavigationViewContentGridBorderBrush", 0x00, 0x00, 0x00, 0x00),
                Brush("ScrollBarButtonBackground", 0x00, 0x00, 0x00, 0x00),
                Brush("ScrollBarButtonBorderBrush", 0x00, 0x00, 0x00, 0x00),
                Brush("ScrollBarButtonForeground", 0xFF, 0x74, 0x85, 0x9A),
                Brush("ScrollBarButtonHoverBackground", 0xFF, 0xE1, 0xEC, 0xF8),
                Brush("ScrollBarThumbBackground", 0xFF, 0xC8, 0xD7, 0xE7),
                Brush("ScrollBarThumbHoverBackground", 0xFF, 0x2F, 0x7B, 0xFF),
                Brush("ScrollBarThumbPressedBackground", 0xFF, 0x1D, 0x62, 0xD6),
                Brush("ScrollBarTrackBackground", 0x00, 0x00, 0x00, 0x00),
                Brush("OnAccentSoftBrush", 0x40, 0xFF, 0xFF, 0xFF),
            },
            gradients: new[]
            {
                Gradient("WindowGradientBrush", Color(0xFF, 0xF7, 0xFA, 0xFE), Color(0xFF, 0xEE, 0xF5, 0xFC), Color(0xFF, 0xE9, 0xF0, 0xF8)),
                Gradient("CardGradientBrush", Color(0xFF, 0xFF, 0xFF, 0xFF), Color(0xFF, 0xED, 0xF4, 0xFC)),
                Gradient("MemoryRingGradientBrush", Color(0xFF, 0x35, 0xA6, 0xFF), Color(0xFF, 0x2F, 0x7B, 0xFF), Color(0xFF, 0x8B, 0x5C, 0xF6)),
            },
            titleBar: new TitleBarPalette(
                Foreground: Color(0xFF, 0x14, 0x20, 0x33),
                InactiveForeground: Color(0xAA, 0x14, 0x20, 0x33),
                HoverBackground: Color(0x22, 0x2F, 0x7B, 0xFF),
                HoverForeground: Color(0xFF, 0x14, 0x20, 0x33),
                PressedBackground: Color(0x36, 0x2F, 0x7B, 0xFF),
                PressedForeground: Color(0xFF, 0x14, 0x20, 0x33)));
    }

    private static AppThemePalette Dark()
    {
        return new AppThemePalette(
            isLightTheme: false,
            windowBorderColorBgr: 0x00120803,
            brushes: new[]
            {
                Brush("BaseBrush", 0xFF, 0x03, 0x08, 0x12),
                Brush("ContentBrush", 0xFF, 0x07, 0x10, 0x1B),
                Brush("SurfaceBrush", 0xFF, 0x0B, 0x17, 0x26),
                Brush("CardBrush", 0xFF, 0x0E, 0x1A, 0x2A),
                Brush("SurfaceElevatedBrush", 0xFF, 0x12, 0x24, 0x3A),
                Brush("PaneBrush", 0xFF, 0x05, 0x0B, 0x14),
                Brush("HoverBrush", 0xFF, 0x13, 0x2A, 0x44),
                Brush("StrokeBrush", 0xFF, 0x1D, 0x31, 0x4A),
                Brush("StrokeHoverBrush", 0xFF, 0x2F, 0x50, 0x75),
                Brush("DividerBrush", 0xFF, 0x10, 0x22, 0x35),
                Brush("DividerSolidBrush", 0xFF, 0x1D, 0x31, 0x4A),
                Brush("MutedSoftBrush", 0xFF, 0x15, 0x1F, 0x2D),
                Brush("TitleTextBrush", 0xFF, 0xF5, 0xF8, 0xFF),
                Brush("BodyTextBrush", 0xFF, 0xC8, 0xD3, 0xE0),
                Brush("MutedTextBrush", 0xFF, 0x92, 0xA1, 0xB4),
                Brush("SubtleTextBrush", 0xFF, 0x63, 0x73, 0x8A),
                Brush("DisabledSurfaceBrush", 0xFF, 0x1B, 0x2A, 0x3D),
                Brush("DisabledStrokeBrush", 0xFF, 0x26, 0x38, 0x4E),
                Brush("DisabledTextBrush", 0xFF, 0x70, 0x84, 0x9D),
                Brush("ButtonPressedBrush", 0xFF, 0x0D, 0x1D, 0x30),
                Brush("SidebarNavBackgroundBrush", 0x00, 0x00, 0x00, 0x00),
                Brush("SidebarNavHoverBrush", 0xFF, 0x13, 0x2A, 0x44),
                Brush("SidebarNavPressedBrush", 0xFF, 0x0D, 0x1D, 0x30),
                Brush("SidebarNavForegroundBrush", 0xFF, 0xC8, 0xD3, 0xE0),
                Brush("SidebarNavHoverForegroundBrush", 0xFF, 0xF5, 0xF8, 0xFF),
                Brush("SidebarNavPressedForegroundBrush", 0xFF, 0xF5, 0xF8, 0xFF),
                Brush("SidebarNavSelectedForegroundBrush", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("HeaderBarBrush", 0xFF, 0x0B, 0x17, 0x26),
                Brush("RowItemBrush", 0xFF, 0x0B, 0x17, 0x26),
                Brush("RowHoverBrush", 0xFF, 0x13, 0x2A, 0x44),
                Brush("InlineMutedBrush", 0xFF, 0x63, 0x73, 0x8A),
                Brush("EmptyIconBrush", 0xFF, 0x1D, 0x31, 0x4A),
                Brush("ContentAreaBrush", 0xFF, 0x07, 0x10, 0x1B),
                Brush("PaneAreaBrush", 0xFF, 0x05, 0x0B, 0x14),
                Brush("TextControlBackground", 0xFF, 0x0B, 0x17, 0x26),
                Brush("TextControlBackgroundPointerOver", 0xFF, 0x10, 0x22, 0x38),
                Brush("TextControlBackgroundFocused", 0xFF, 0x0B, 0x17, 0x26),
                Brush("TextControlForeground", 0xFF, 0xF5, 0xF8, 0xFF),
                Brush("TextControlForegroundPointerOver", 0xFF, 0xF5, 0xF8, 0xFF),
                Brush("TextControlForegroundFocused", 0xFF, 0xF5, 0xF8, 0xFF),
                Brush("TextControlPlaceholderForeground", 0xFF, 0x63, 0x73, 0x8A),
                Brush("TextControlPlaceholderForegroundPointerOver", 0xFF, 0x63, 0x73, 0x8A),
                Brush("TextControlPlaceholderForegroundFocused", 0xFF, 0x63, 0x73, 0x8A),
                Brush("TextControlBorderBrush", 0xFF, 0x1D, 0x31, 0x4A),
                Brush("TextControlBorderBrushPointerOver", 0xFF, 0x2F, 0x50, 0x75),
                Brush("TextControlBorderBrushFocused", 0xFF, 0x2F, 0x7B, 0xFF),
                Brush("ListViewItemBackgroundPointerOver", 0xFF, 0x13, 0x2A, 0x44),
                Brush("ListViewItemBackgroundSelected", 0xFF, 0x10, 0x22, 0x38),
                Brush("ListViewItemBackgroundSelectedPointerOver", 0xFF, 0x13, 0x2A, 0x44),
                Brush("ListViewItemForeground", 0xFF, 0xC8, 0xD3, 0xE0),
                Brush("ListViewItemForegroundPointerOver", 0xFF, 0xF5, 0xF8, 0xFF),
                Brush("ComboBoxBackground", 0xFF, 0x0B, 0x17, 0x26),
                Brush("ComboBoxBackgroundPointerOver", 0xFF, 0x10, 0x22, 0x38),
                Brush("ComboBoxBorderBrush", 0xFF, 0x1D, 0x31, 0x4A),
                Brush("ComboBoxForeground", 0xFF, 0xC8, 0xD3, 0xE0),
                Brush("ButtonBackground", 0xFF, 0x0B, 0x17, 0x26),
                Brush("ButtonBackgroundPointerOver", 0xFF, 0x13, 0x2A, 0x44),
                Brush("ButtonBackgroundPressed", 0xFF, 0x0D, 0x1D, 0x30),
                Brush("ButtonBackgroundDisabled", 0xFF, 0x1B, 0x2A, 0x3D),
                Brush("ButtonBorderBrush", 0xFF, 0x1D, 0x31, 0x4A),
                Brush("ButtonBorderBrushPointerOver", 0xFF, 0x2F, 0x50, 0x75),
                Brush("ButtonBorderBrushPressed", 0xFF, 0x2F, 0x50, 0x75),
                Brush("ButtonBorderBrushDisabled", 0xFF, 0x26, 0x38, 0x4E),
                Brush("ButtonForeground", 0xFF, 0xC8, 0xD3, 0xE0),
                Brush("ButtonForegroundPointerOver", 0xFF, 0xF5, 0xF8, 0xFF),
                Brush("ButtonForegroundPressed", 0xFF, 0xF5, 0xF8, 0xFF),
                Brush("ButtonForegroundDisabled", 0xFF, 0x70, 0x84, 0x9D),
                Brush("AccentButtonForegroundDisabled", 0xFF, 0x9E, 0xB1, 0xC8),
                Brush("AccentButtonBackgroundDisabled", 0xFF, 0x52, 0x65, 0x7B),
                Brush("AccentButtonBorderBrushDisabled", 0xFF, 0x52, 0x65, 0x7B),
                Brush("ExpanderHeaderBackground", 0xFF, 0x0B, 0x17, 0x26),
                Brush("ExpanderHeaderBackgroundPointerOver", 0xFF, 0x13, 0x2A, 0x44),
                Brush("ExpanderHeaderBackgroundPressed", 0xFF, 0x10, 0x22, 0x38),
                Brush("ExpanderHeaderForeground", 0xFF, 0xF5, 0xF8, 0xFF),
                Brush("ExpanderHeaderBorderBrush", 0xFF, 0x1D, 0x31, 0x4A),
                Brush("ExpanderContentBackground", 0xFF, 0x0B, 0x17, 0x26),
                Brush("ExpanderContentForeground", 0xFF, 0xC8, 0xD3, 0xE0),
                Brush("ContentDialogBackground", 0xFF, 0x1A, 0x1E, 0x22),
                Brush("ContentDialogBorderBrush", 0xFF, 0x26, 0x2B, 0x30),
                Brush("ContentDialogTitleForeground", 0xFF, 0xF0, 0xF1, 0xF3),
                Brush("ContentDialogForeground", 0xFF, 0xA8, 0xAC, 0xB4),
                Brush("ContentDialogSeparatorBrush", 0xFF, 0x26, 0x2B, 0x30),
                Brush("ContentDialogCloseButtonBackground", 0xFF, 0x1E, 0x23, 0x29),
                Brush("ContentDialogCloseButtonForeground", 0xFF, 0xA8, 0xAC, 0xB4),
                Brush("ContentDialogButtonBackground", 0xFF, 0x4A, 0x90, 0xD9),
                Brush("ContentDialogButtonForeground", 0xFF, 0xFF, 0xFF, 0xFF),
                Brush("ContentDialogButtonBorderBrush", 0x00, 0x00, 0x00, 0x00),
                Brush("ToggleSwitchOnForeground", 0xFF, 0x4A, 0x90, 0xD9),
                Brush("ToggleSwitchForeground", 0xFF, 0x6B, 0x70, 0x79),
                Brush("ToggleSwitchTrackOnForeground", 0xFF, 0x4A, 0x90, 0xD9),
                Brush("ToggleSwitchTrackForeground", 0xFF, 0x26, 0x2B, 0x30),
                Brush("ToggleSwitchThumbForeground", 0xFF, 0xF0, 0xF1, 0xF3),
                Brush("ToggleSwitchThumbOnForeground", 0xFF, 0xF0, 0xF1, 0xF3),
                Brush("NavigationViewItemBackground", 0x00, 0x00, 0x00, 0x00),
                Brush("NavigationViewItemBackgroundPointerOver", 0xFF, 0x1E, 0x23, 0x29),
                Brush("NavigationViewItemBackgroundPressed", 0xFF, 0x17, 0x1A, 0x1E),
                Brush("NavigationViewItemBackgroundSelected", 0xFF, 0x1A, 0x1E, 0x22),
                Brush("NavigationViewItemForeground", 0xFF, 0xA8, 0xAC, 0xB4),
                Brush("NavigationViewItemForegroundPointerOver", 0xFF, 0xF0, 0xF1, 0xF3),
                Brush("NavigationViewItemForegroundSelected", 0xFF, 0xF0, 0xF1, 0xF3),
                Brush("NavigationViewSelectionIndicatorForeground", 0xFF, 0x4A, 0x90, 0xD9),
                Brush("NavigationViewContentBackground", 0xFF, 0x14, 0x17, 0x1A),
                Brush("NavigationViewContentGridBorderBrush", 0x00, 0x00, 0x00, 0x00),
                Brush("ScrollBarButtonBackground", 0x00, 0x00, 0x00, 0x00),
                Brush("ScrollBarButtonBorderBrush", 0x00, 0x00, 0x00, 0x00),
                Brush("ScrollBarButtonForeground", 0xFF, 0x6B, 0x70, 0x79),
                Brush("ScrollBarButtonHoverBackground", 0xFF, 0x1E, 0x23, 0x29),
                Brush("ScrollBarThumbBackground", 0xFF, 0x26, 0x2B, 0x30),
                Brush("ScrollBarThumbHoverBackground", 0xFF, 0x4A, 0x90, 0xD9),
                Brush("ScrollBarThumbPressedBackground", 0xFF, 0x6B, 0xAE, 0xEC),
                Brush("ScrollBarTrackBackground", 0x00, 0x00, 0x00, 0x00),
                Brush("OnAccentSoftBrush", 0x40, 0xFF, 0xFF, 0xFF),
            },
            gradients: new[]
            {
                Gradient("WindowGradientBrush", Color(0xFF, 0x03, 0x08, 0x12), Color(0xFF, 0x07, 0x11, 0x1F), Color(0xFF, 0x0A, 0x14, 0x24)),
                Gradient("CardGradientBrush", Color(0xFF, 0x14, 0x24, 0x3A), Color(0xFF, 0x0B, 0x17, 0x26)),
                Gradient("MemoryRingGradientBrush", Color(0xFF, 0x55, 0xC7, 0xFF), Color(0xFF, 0x2F, 0x7B, 0xFF), Color(0xFF, 0x8B, 0x5C, 0xF6)),
            },
            titleBar: new TitleBarPalette(
                Foreground: Color(0xFF, 0xFF, 0xFF, 0xFF),
                InactiveForeground: Color(0xB4, 0xFF, 0xFF, 0xFF),
                HoverBackground: Color(0x2A, 0x55, 0xC7, 0xFF),
                HoverForeground: Color(0xFF, 0xFF, 0xFF, 0xFF),
                PressedBackground: Color(0x46, 0x2F, 0x7B, 0xFF),
                PressedForeground: Color(0xFF, 0xFF, 0xFF, 0xFF)));
    }

    private static ThemeBrushToken Brush(string key, byte a, byte r, byte g, byte b)
    {
        return new ThemeBrushToken(key, Color(a, r, g, b));
    }

    private static ThemeGradientToken Gradient(string key, params ThemeColor[] stops)
    {
        return new ThemeGradientToken(key, stops);
    }

    private static ThemeColor Color(byte a, byte r, byte g, byte b)
    {
        return new ThemeColor(a, r, g, b);
    }
}

public readonly record struct ThemeColor(byte A, byte R, byte G, byte B);

public readonly record struct ThemeBrushToken(string Key, ThemeColor Color);

public readonly record struct ThemeGradientToken(string Key, IReadOnlyList<ThemeColor> Stops);

public readonly record struct TitleBarPalette(
    ThemeColor Foreground,
    ThemeColor InactiveForeground,
    ThemeColor HoverBackground,
    ThemeColor HoverForeground,
    ThemeColor PressedBackground,
    ThemeColor PressedForeground);
