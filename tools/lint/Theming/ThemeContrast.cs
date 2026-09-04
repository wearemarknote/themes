using System.Collections.Generic;

namespace MarkdownViewer.Theming;

/// One legibility measurement: what was measured, what it came to, and the
/// bar it is held to.
public sealed record ContrastCheck(string Surface, double Ratio, double Minimum)
{
    public bool Passes => Ratio >= Minimum;
    public override string ToString() => $"{Surface}: {Ratio:0.00}:1 (minimum {Minimum:0.0}:1)";
}

/// The contrast bar every theme is measured against — built-ins in the test
/// suite, submissions in the lint, installs in the validator's warnings.
/// Text is held to WCAG AA for body text (4.5:1); links, line numbers and
/// other non-text cues to the 3:1 the standard sets for UI components.
public static class ThemeContrast
{
    public const double Text = 4.5;
    public const double Component = 3.0;
    /// Line numbers. Every palette in the wild sets them in its comment
    /// tone, which lands between 2.5 and 3:1 by design — they are a cue you
    /// find, not text you read. The bar is set where a gutter starts to
    /// vanish rather than where body text becomes legible.
    public const double Muted = 2.5;

    public static IReadOnlyList<ContrastCheck> Measure(ResolvedTheme t)
    {
        var checks = new List<ContrastCheck>();

        var pv = t.PreviewColors;
        checks.Add(new("preview.fg on preview.bg", ColorMath.Contrast(pv["fg"], pv["bg"]), Text));
        checks.Add(new("preview.fgSecondary on preview.bg", ColorMath.Contrast(pv["fgSecondary"], pv["bg"]), Text));
        checks.Add(new("preview.link on preview.bg", ColorMath.Contrast(pv["link"], pv["bg"]), Component));
        checks.Add(new("preview.fg on preview.codeBg", ColorMath.Contrast(pv["fg"], pv["codeBg"]), Text));

        if (!t.EditorIsStock)
        {
            var ed = t.EditorColors;
            checks.Add(new("editor.fg on editor.bg", ColorMath.Contrast(ed["fg"], ed["bg"]), Text));
            checks.Add(new("editor.gutter on editor.bg", ColorMath.Contrast(ed["gutter"], ed["bg"]), Muted));
            checks.Add(new("editor.link on editor.bg", ColorMath.Contrast(ed["link"], ed["bg"]), Component));
            checks.Add(new("editor.heading on editor.bg", ColorMath.Contrast(ed["heading"], ed["bg"]), Component));
        }

        // The ink is measured against the WORST of the three fills a button
        // shows. The ramp moves away from the ink, so for dark ink the rest
        // colour is the worst case and for white ink the pressed shade is —
        // but measuring all three is what makes that a fact rather than an
        // assumption.
        var (rest, hover, pressed) = ColorMath.Ramp(t.Accent).InteractionFills(ColorMath.IsDark(t.Ink));
        double worst = System.Math.Min(
            ColorMath.Contrast(t.Ink, rest),
            System.Math.Min(ColorMath.Contrast(t.Ink, hover), ColorMath.Contrast(t.Ink, pressed)));
        checks.Add(new("accent.ink on the accent's rest, hover and pressed fills", worst, Text));

        return checks;
    }
}
