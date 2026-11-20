using System.Globalization;
using System.Text.RegularExpressions;

namespace CatgirlStudio.Core;

/// <summary>
/// A color picked by hand ("#RRGGBB") as the prompt writes it: a plain English name the image
/// model understands, plus the hex for the exact shade ("pastel pink (#F5B1C8)"). Opaque only:
/// a color is the color of the thing, never how see-through it is.
/// </summary>
public static partial class ColorWords
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")] private static partial Regex HexRx();

    public static bool IsHex(string? value) => value is not null && HexRx().IsMatch(value);

    /// <summary>"#f5b1c8" -> "#F5B1C8".</summary>
    public static string Canonical(string hex) => "#" + hex[1..].ToUpperInvariant();

    public static (byte R, byte G, byte B) Rgb(string hex)
    {
        var v = int.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public static string Hex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    /// <summary>What goes into the prompt for a picked color.</summary>
    public static string Phrase(string hex) => $"{Name(hex)} ({Canonical(hex)})";

    /// <summary>A short English name: hue family with lightness and saturation words.</summary>
    public static string Name(string hex)
    {
        var (r8, g8, b8) = Rgb(hex);
        double r = r8 / 255.0, g = g8 / 255.0, b = b8 / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        var chroma = max - min;
        var s = chroma == 0 ? 0 : chroma / (1 - Math.Abs(2 * l - 1));
        double h = 0;
        if (chroma > 0)
        {
            if (max == r) h = 60 * (((g - b) / chroma) % 6);
            else if (max == g) h = 60 * ((b - r) / chroma + 2);
            else h = 60 * ((r - g) / chroma + 4);
            if (h < 0) h += 360;
        }

        if (chroma < 0.07 || s < 0.1)
            return l switch
            {
                > 0.95 => "white",
                > 0.85 => "very light gray",
                > 0.68 => "light gray",
                > 0.45 => "gray",
                > 0.22 => "charcoal gray",
                _ => "black"
            };

        // warm and dark, or warm and grayish-light: browns and beiges read better than "dark orange"
        if (h is >= 8 and < 50)
        {
            if (l < 0.42 && s < 0.85) return l < 0.22 ? "dark brown" : "brown";
            if (l > 0.62 && s < 0.55) return l > 0.82 ? "light beige" : "beige";
            if (l is >= 0.42 and <= 0.62 && s < 0.45) return "camel brown";
        }

        var family = h switch
        {
            < 10 or >= 345 => "red",
            < 35 => "orange",
            < 50 => "golden yellow",
            < 68 => "yellow",
            < 95 => "yellow-green",
            < 155 => "green",
            < 172 => "sea green",
            < 192 => "teal",
            < 208 => "sky blue",
            < 245 => "blue",
            < 265 => "blue-violet",
            < 292 => "purple",
            < 320 => "magenta",
            _ => "rose pink"
        };
        // light reds and magentas are pinks; light purples are lavender
        if (l > 0.66 && family == "magenta") family = "lilac";
        if (l > 0.66 && family is "red" or "rose pink") family = "pink";
        if (l > 0.7 && family is "purple" or "blue-violet") family = "lavender";

        var words = new List<string>();
        if (l > 0.86) words.Add("pale");
        else if (l > 0.7) words.Add(s > 0.45 ? "pastel" : "soft");
        else if (l > 0.6) words.Add("light");
        else if (l < 0.18) words.Add("very dark");
        else if (l < 0.33) words.Add("dark");
        if (s < 0.3 && l is >= 0.18 and <= 0.7) words.Add("muted");
        else if (s > 0.85 && l is > 0.38 and < 0.62) words.Add("vivid");
        words.Add(family);
        return string.Join(" ", words);
    }
}
