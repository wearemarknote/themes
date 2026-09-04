using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarkdownViewer.Theming;

/// A theme as it sits in a `theme.json` — the file a built-in ships as, the
/// file the marketplace serves, the file a community author writes. Every
/// surface the app paints reads from one of these; there is no other source
/// of theme colour anywhere in the app.
///
/// This is the raw, unvalidated shape: strings that may or may not be
/// colours, fields that may be missing. <see cref="ThemeValidator"/> turns it
/// into a <see cref="ResolvedTheme"/> or a reason it could not. Property
/// names are the JSON names; the serializer options are case-insensitive and
/// tolerate comments and trailing commas, the same as plugin manifests.
public sealed class ThemeDefinition
{
    /// The one number a reader checks first. A file written for a newer
    /// schema is refused, not half-applied.
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// Reverse-DNS identity, e.g. `uk.marknote.nord-dark`. The on-disk folder
    /// name for an installed theme and the value settings store. Renaming a
    /// published theme's id orphans every install of it.
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// Built-ins only: a resource key whose translation replaces
    /// <see cref="Name"/> in the picker. "Light" and "Dark" are words;
    /// "Nord" and "Dracula" are product names and stay as written.
    [JsonPropertyName("nameResource")]
    public string? NameResource { get; set; }

    /// The names this theme was saved under before themes had ids, so a
    /// settings file from an older build still opens on the same look.
    [JsonPropertyName("legacyNames")]
    public List<string>? LegacyNames { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyName("license")]
    public string? License { get; set; }

    /// `light` or `dark`. Picks the WinUI theme dictionary the stock
    /// controls draw from, the side of the picker the theme is listed on,
    /// and the defaults for the code and diagram styles.
    [JsonPropertyName("appearance")]
    public string? Appearance { get; set; }

    [JsonPropertyName("minMarknoteVersion")]
    public string? MinMarknoteVersion { get; set; }

    /// Window backdrop: `none` (the default), `mica`, `mica-alt`, `acrylic`.
    /// A material only shows through chrome surfaces that are transparent.
    [JsonPropertyName("material")]
    public string? Material { get; set; }

    /// Built-ins only: position within its side of the picker.
    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("colors")]
    public ThemeColors? Colors { get; set; }

    /// Optional extra CSS for the preview and exported HTML. Sanitised
    /// before use — see <see cref="CssSanitiser"/> for what is refused.
    [JsonPropertyName("css")]
    public string? Css { get; set; }
}

public sealed class ThemeColors
{
    [JsonPropertyName("chrome")]
    public ChromeColors? Chrome { get; set; }

    [JsonPropertyName("accent")]
    public AccentPair? Accent { get; set; }

    [JsonPropertyName("caption")]
    public CaptionColors? Caption { get; set; }

    [JsonPropertyName("editor")]
    public EditorPalette? Editor { get; set; }

    /// The preview's CSS variables, keyed by their camel-case names
    /// (`fgSecondary` is `--fg-secondary`). All of
    /// <see cref="ThemeSchema.PreviewKeys"/> are required.
    [JsonPropertyName("preview")]
    public Dictionary<string, string?>? Preview { get; set; }

    [JsonPropertyName("code")]
    public CodeStyles? Code { get; set; }
}

/// The window's own surfaces. `transparent` lets the material show through.
public sealed class ChromeColors
{
    /// The title bar and everything behind the panels.
    [JsonPropertyName("window")]
    public string? Window { get; set; }

    /// The rail and sidebar. Transparent under most themes so the window
    /// colour reads through both.
    [JsonPropertyName("panel")]
    public string? Panel { get; set; }

    /// The editor and preview panes, including the formatting toolbar row
    /// and the selected tab, which sits attached to the note.
    [JsonPropertyName("pane")]
    public string? Pane { get; set; }

    /// Dialogs: Settings, About, Keybindings, every confirmation.
    [JsonPropertyName("dialog")]
    public string? Dialog { get; set; }

    /// Menus and flyouts. Optional; the stock surface when omitted.
    [JsonPropertyName("flyout")]
    public string? Flyout { get; set; }
}

/// The accent and the ink that goes on it. The hover and pressed shades are
/// derived (<see cref="ColorMath.Ramp"/>), moving away from the ink so a
/// button never gets harder to read as you point at it.
public sealed class AccentPair
{
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("ink")]
    public string? Ink { get; set; }
}

/// The window's minimise / maximise / close glyphs. Optional as a block;
/// the defaults are white or black glyphs over translucent hover fills,
/// by appearance.
public sealed class CaptionColors
{
    [JsonPropertyName("foreground")]
    public string? Foreground { get; set; }

    [JsonPropertyName("inactiveForeground")]
    public string? InactiveForeground { get; set; }

    [JsonPropertyName("hoverBackground")]
    public string? HoverBackground { get; set; }

    [JsonPropertyName("pressedBackground")]
    public string? PressedBackground { get; set; }
}

/// The editor palette: the flat set of colours CodeMirror is themed from,
/// plus two switches.
///
/// `stock: true` asks for CodeMirror's own default look instead of a
/// palette — it is what Marknote's Light themes use, because the stock
/// light highlighting is a hand-tuned style with more roles than this
/// palette carries, and transcribing it would only approximate it. With
/// `stock` set the colour keys are not required.
public sealed class EditorPalette
{
    [JsonPropertyName("stock")]
    public bool Stock { get; set; }

    /// Keep the brand-red caret and selection over whatever the palette
    /// says. Marknote's own themes set this; a named palette wants its own.
    [JsonPropertyName("brandCaret")]
    public bool BrandCaret { get; set; }

    /// The colour keys, captured loosely so the schema's required list
    /// (<see cref="ThemeSchema.EditorKeys"/>) is data and not a class shape.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Colors { get; set; }

    /// The value under <paramref name="key"/>, case-insensitively, when it
    /// is a string.
    public string? Get(string key)
    {
        if (Colors is null) return null;
        foreach (var (k, v) in Colors)
        {
            if (string.Equals(k, key, System.StringComparison.OrdinalIgnoreCase))
                return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        return null;
    }
}

/// Which bundled stylesheet colours fenced code, and which mermaid theme
/// draws diagrams. Both default by appearance.
public sealed class CodeStyles
{
    [JsonPropertyName("highlight")]
    public string? Highlight { get; set; }

    [JsonPropertyName("mermaid")]
    public string? Mermaid { get; set; }
}
