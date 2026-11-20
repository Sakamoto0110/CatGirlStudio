using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CatgirlStudio.App.Theme;

/// <summary>
/// Derives the accent shades from the one AccentColor in Theme.xaml (or from --accent #RRGGBB),
/// so changing the accent is a one-line change.
/// </summary>
internal static partial class ThemeColors
{
    public static void Apply(ResourceDictionary resources, string? accentOverride)
    {
        var accent = (Color)resources["AccentColor"];
        if (accentOverride is not null && TryParse(accentOverride, out var parsed)) accent = parsed;
        var bg = (Color)resources["BgColor"];
        resources["AccentColor"] = accent;
        resources["Accent"] = Freeze(accent);
        resources["AccentHover"] = Freeze(Mix(accent, Colors.White, 0.22));
        resources["AccentSoft"] = Freeze(Mix(bg, accent, 0.17));
        // text on the accent: dark on a light accent, white on a dark one
        resources["AccentInk"] = Freeze(Luma(accent) > 0.55 ? Mix(Color.FromRgb(0x1C, 0x0D, 0x16), accent, 0.10) : Colors.White);
    }

    static bool TryParse(string hex, out Color color)
    {
        try { color = (Color)ColorConverter.ConvertFromString(hex.StartsWith('#') ? hex : "#" + hex); return true; }
        catch (FormatException) { color = default; return false; }
    }

    static SolidColorBrush Freeze(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));

    static double Luma(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;

    /// <summary>Dark title bar to match the window (Windows 10 2004+; silently skipped before).</summary>
    public static void DarkTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        try
        {
            int on = 1;
            DwmSetWindowAttribute(handle, 20, ref on, sizeof(int));
        }
        catch (DllNotFoundException) { }
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
