using System;
using System.Runtime.InteropServices;
using OneClickClose.Core;

namespace OneClickClose.WinUI.Services;

public static class WindowChromeService
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmWindowBorderColor = 34;
    private const int DwmRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public static void Apply(IntPtr hwnd, AppThemePalette palette)
    {
        try
        {
            int preference = DwmRound;
            DwmSetWindowAttribute(hwnd, DwmWindowCornerPreference, ref preference, sizeof(int));

            int borderColor = unchecked((int)palette.WindowBorderColorBgr);
            DwmSetWindowAttribute(hwnd, DwmWindowBorderColor, ref borderColor, sizeof(int));
        }
        catch
        {
        }
    }
}
