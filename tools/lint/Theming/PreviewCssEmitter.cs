using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MarkdownViewer.Theming;

/// Turns a resolved theme into what the preview page (and the exported HTML
/// shell, which is the same page without the host) consumes: one `:root`
/// block of CSS variables, the optional theme CSS, and the JSON message that
/// applies both live without a reload.
public static class PreviewCssEmitter
{
    /// A colour as CSS reads it. Opaque values stay `#RRGGBB`; anything with
    /// alpha becomes `rgba()`, because an eight-digit hex means `#RRGGBBAA`
    /// to CSS and `#AARRGGBB` to XAML, and this is the one place the two
    /// grammars meet.
    public static string CssColor(Rgba c)
    {
        if (c.A == 0xFF) return c.ToHex();
        if (c.A == 0x00) return "transparent";
        string alpha = (c.A / 255.0).ToString("0.##", CultureInfo.InvariantCulture);
        return $"rgba({c.R}, {c.G}, {c.B}, {alpha})";
    }

    /// The `:root { … }` block that replaces the per-theme selectors the
    /// preview used to bake in. One theme, one block, every variable set.
    public static string EmitVariables(ResolvedTheme theme)
    {
        var sb = new StringBuilder();
        sb.Append(":root {\n");
        sb.Append("  color-scheme: ").Append(theme.IsDark ? "dark" : "light").Append(";\n");
        foreach (string key in ThemeSchema.PreviewKeys)
        {
            sb.Append("  ").Append(ThemeSchema.PreviewVariableName(key))
              .Append(": ").Append(CssColor(theme.PreviewColors[key])).Append(";\n");
        }
        foreach (var (name, value) in AccentVariables(theme))
            sb.Append("  ").Append(name).Append(": ").Append(value).Append(";\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    /// The accent pair, for the preview's own chrome — the table-of-contents
    /// pill, the selection highlight — which should match the app's accent
    /// rather than the theme's link colour (Catppuccin's links are blue, its
    /// accent mauve).
    public static IEnumerable<(string Name, string Value)> AccentVariables(ResolvedTheme theme)
    {
        yield return ("--accent", CssColor(theme.Accent));
        yield return ("--accent-ink", CssColor(theme.Ink));
        yield return ("--accent-selection", CssColor(ColorMath.WithAlpha(theme.Accent, theme.IsDark ? (byte)0x55 : (byte)0x4D)));
    }

    /// The theme's own CSS, re-checked at the moment of use: the file on
    /// disk may have been edited since it was validated. A block that no
    /// longer passes is dropped, and the caller is told so it can log once.
    public static bool TryEmitThemeCss(ResolvedTheme theme, out string css)
    {
        var result = CssSanitiser.Sanitise(theme.Css);
        css = result.Css;
        return result.IsClean;
    }

    public static string HighlightSheetHref(string assetsBase, string sheet) =>
        $"{assetsBase}/hljs/styles/{sheet}.min.css";

    /// The body of the `setThemePalette` message the preview page handles.
    /// The caller adds the `type` field; everything else is here so the
    /// window never assembles a palette itself.
    public static string PaletteMessageJson(ResolvedTheme theme)
    {
        var vars = new System.Collections.Generic.Dictionary<string, string>();
        foreach (string key in ThemeSchema.PreviewKeys)
            vars[ThemeSchema.PreviewVariableName(key)] = CssColor(theme.PreviewColors[key]);
        foreach (var (name, value) in AccentVariables(theme))
            vars[name] = value;

        TryEmitThemeCss(theme, out string css);

        return JsonSerializer.Serialize(new
        {
            appearance = theme.Appearance,
            vars,
            highlight = theme.HighlightSheet,
            mermaid = theme.MermaidTheme,
            css,
        });
    }
}
