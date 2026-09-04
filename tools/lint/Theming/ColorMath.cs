using System;
using System.Globalization;

namespace MarkdownViewer.Theming;

/// A colour with no WinUI behind it. Windows.UI.Color is a WinRT struct, and
/// the theme model has to be usable from the theme tests and the theme lint
/// (neither of which can load WindowsAppSDK), so the model carries this and
/// AccentColors converts at the edge.
public readonly record struct Rgba(byte A, byte R, byte G, byte B)
{
    public static Rgba Opaque(byte r, byte g, byte b) => new(0xFF, r, g, b);

    public static readonly Rgba White = Opaque(0xFF, 0xFF, 0xFF);
    public static readonly Rgba Black = Opaque(0x00, 0x00, 0x00);

    /// `#RRGGBB` — what a theme file and a CSS variable want. Alpha is
    /// deliberately not written: every consumer of this string paints an
    /// opaque surface, and an eight-digit form would silently change meaning
    /// between XAML (`#AARRGGBB`) and CSS (`#RRGGBBAA`).
    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public override string ToString() => A == 0xFF ? ToHex() : $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}

/// The colour arithmetic every theme surface shares: the accent shade ramp,
/// the luminance rule that decides which way a hover moves, and the contrast
/// ratio the validator holds theme authors to. Pure functions on Rgba so the
/// same numbers are computed at runtime, in the tests and by the lint.
public static class ColorMath
{
    /// Parses `#RRGGBB` or `#AARRGGBB` (the `#` and surrounding whitespace
    /// optional). Six digits read as opaque. Anything else is a rejection,
    /// not a guess — a theme with a malformed colour must fail validation,
    /// never paint something approximate.
    public static bool TryParseHex(string? hex, out Rgba color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        string s = hex.Trim().TrimStart('#');
        if (s.Length == 6) s = "FF" + s;
        if (s.Length != 8) return false;
        if (!byte.TryParse(s.AsSpan(0, 2), NumberStyles.HexNumber, null, out byte a)) return false;
        if (!byte.TryParse(s.AsSpan(2, 2), NumberStyles.HexNumber, null, out byte r)) return false;
        if (!byte.TryParse(s.AsSpan(4, 2), NumberStyles.HexNumber, null, out byte g)) return false;
        if (!byte.TryParse(s.AsSpan(6, 2), NumberStyles.HexNumber, null, out byte b)) return false;
        color = new Rgba(a, r, g, b);
        return true;
    }

    public static string ToHex(Rgba c) => c.ToHex();

    /// WCAG 2.x relative luminance, 0 (black) to 1 (white). Alpha is ignored:
    /// the callers ask about an opaque fill, and blending against an unknown
    /// backdrop would only make the answer wrong in a different way.
    public static double Luminance(Rgba c)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    /// WCAG contrast ratio, 1 to 21, symmetric in its arguments. AA for body
    /// text is 4.5, for large text and UI components 3.0.
    public static double Contrast(Rgba a, Rgba b)
    {
        double la = Luminance(a), lb = Luminance(b);
        double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// Ink is "dark" when it sits below mid-luminance. This is the single
    /// rule that decides whether a hover moves a fill lighter or darker: away
    /// from the ink, so pointing at a button never makes it harder to read.
    public static bool IsDark(Rgba c) => Luminance(c) < 0.5;

    /// Moves each channel `amount` (0..1) of the way toward white; alpha kept.
    public static Rgba Lighten(Rgba c, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte r = (byte)Math.Round(c.R + (255 - c.R) * amount);
        byte g = (byte)Math.Round(c.G + (255 - c.G) * amount);
        byte b = (byte)Math.Round(c.B + (255 - c.B) * amount);
        return new Rgba(c.A, r, g, b);
    }

    /// Scales each channel by `1 - amount` (0..1) toward black; alpha kept.
    public static Rgba Darken(Rgba c, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte r = (byte)Math.Round(c.R * (1 - amount));
        byte g = (byte)Math.Round(c.G * (1 - amount));
        byte b = (byte)Math.Round(c.B * (1 - amount));
        return new Rgba(c.A, r, g, b);
    }

    public static Rgba WithAlpha(Rgba c, byte alpha) => new(alpha, c.R, c.G, c.B);

    /// The shade ramp App.xaml ships for the brand accent, derived for any
    /// accent: ~8% per light step, ~10% per dark step. This is the one place
    /// the proportions live — AccentColors paints them, ThemeContrast
    /// measures them.
    public static AccentRamp Ramp(Rgba accent) => new(
        accent,
        Lighten(accent, 0.08), Lighten(accent, 0.20), Lighten(accent, 0.36),
        Darken(accent, 0.10), Darken(accent, 0.20), Darken(accent, 0.30));
}

public readonly record struct AccentRamp(
    Rgba Base,
    Rgba Light1, Rgba Light2, Rgba Light3,
    Rgba Dark1, Rgba Dark2, Rgba Dark3)
{
    /// The fills a button shows at rest, pointer-over and pressed. Hover
    /// and pressed move AWAY from the ink: dark ink lightens the fill, white
    /// ink darkens it, so pointing at a button never makes it harder to
    /// read.
    public (Rgba Idle, Rgba Hover, Rgba Pressed) InteractionFills(bool inkIsDark) =>
        inkIsDark ? (Base, Light1, Light2) : (Base, Dark1, Dark2);
}
