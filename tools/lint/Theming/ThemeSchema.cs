using System;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownViewer.Theming;

/// What a `schemaVersion: 1` theme must contain and may say. The required
/// key lists are the whole reason themes are data: a missing key fails
/// validation with its path, where a missing switch arm used to paint half
/// the app and say nothing.
public static class ThemeSchema
{
    public const int CurrentSchemaVersion = 1;

    /// Built-in ids live here; a submitted theme may not.
    public const string ReservedIdPrefix = "uk.marknote.";

    /// Theme CSS is small by construction — a few overrides, not a
    /// stylesheet. A human reviews every submission's CSS, and 64 KB is
    /// more than a human reads.
    public const int MaxCssBytes = 64 * 1024;

    /// The keys `buildTheme()` in editor.js reads, in the order it lists
    /// them. Every one is required unless the editor block is `stock`.
    public static readonly string[] EditorKeys =
    [
        "bg", "activeLine", "selection", "gutter",
        "fg", "fgStrong",
        "comment", "string", "regexp", "number",
        "keyword", "operator", "func", "type",
        "heading", "link",
    ];

    /// The preview's CSS variables, as camel-case keys. All required — a
    /// theme that leaves one out inherits nothing (the variables are set on
    /// `:root`, there is nothing above it) and the preview would fall back
    /// to whatever the stylesheet's `var(--x, fallback)` says, which is the
    /// `--scroll-thumb` class of bug this list exists to close.
    public static readonly string[] PreviewKeys =
    [
        "fg", "fgSecondary", "bg",
        "codeBg", "codeBorder",
        "quoteBorder", "quoteBg",
        "link", "rule",
        "tocBg", "headerBg",
        "scrollThumb", "scrollThumbHover",
    ];

    public static readonly string[] Appearances = ["light", "dark"];

    public static readonly string[] Materials = ["none", "mica", "mica-alt", "acrylic"];

    /// The highlight.js stylesheets the app bundles under
    /// Assets/web/hljs/styles, by file stem. A theme names one; nothing is
    /// fetched.
    public static readonly string[] HighlightSheets = ["github", "github-dark-dimmed"];

    public static readonly string[] MermaidThemes = ["default", "dark", "neutral", "forest", "base"];

    public static readonly Rgba Transparent = new(0x00, 0x00, 0x00, 0x00);

    /// The colour grammar: `#RRGGBB`, `#AARRGGBB`, or the word `transparent`.
    /// Nothing else — no CSS names, no `rgb()`, no three-digit shorthand —
    /// because the same value is painted by XAML and by CSS, and this is the
    /// intersection both read the same way (alpha aside, which the CSS
    /// emitter spells out as `rgba()` for exactly that reason).
    public static bool TryParseColor(string? text, out Rgba color)
    {
        if (text is not null && string.Equals(text.Trim(), "transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = Transparent;
            return true;
        }
        return ColorMath.TryParseHex(text, out color);
    }

    private static readonly Regex IdPattern = new(
        @"^[a-z0-9]+(-[a-z0-9]+)*(\.[a-z0-9]+(-[a-z0-9]+)*)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// Reverse-DNS: at least two dot-separated segments of lower-case
    /// letters, digits and single hyphens. Doubles as a safe folder name.
    public static bool IsValidId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 128 && IdPattern.IsMatch(id);

    public static bool IsReservedId(string? id) =>
        id is not null && id.StartsWith(ReservedIdPrefix, StringComparison.Ordinal);

    /// `fgSecondary` → `--fg-secondary`.
    public static string PreviewVariableName(string key)
    {
        var sb = new StringBuilder("--", key.Length + 4);
        foreach (char c in key)
        {
            if (char.IsUpper(c)) { sb.Append('-'); sb.Append(char.ToLowerInvariant(c)); }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    public static string DefaultHighlightSheet(bool dark) => dark ? "github-dark-dimmed" : "github";
    public static string DefaultMermaidTheme(bool dark) => dark ? "dark" : "default";
}
