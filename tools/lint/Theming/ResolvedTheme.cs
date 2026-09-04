using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MarkdownViewer.Theming;

/// A theme after validation: every colour parsed once, every default filled
/// in, the editor and preview payloads serialised once. This is what the
/// app applies; nothing downstream parses a string or consults a table.
public sealed class ResolvedTheme
{
    public ThemeDefinition Definition { get; }

    public string Id { get; }
    public string Name { get; }
    public string? NameResource { get; }
    public IReadOnlyList<string> LegacyNames { get; }
    public string Version { get; }
    public string? Author { get; }
    public string? Description { get; }
    public string? Homepage { get; }
    public string? License { get; }
    public string? MinMarknoteVersion { get; }
    public int Order { get; }

    /// `light` or `dark`, as validated.
    public string Appearance { get; }
    public bool IsDark { get; }
    public string Material { get; }

    public Rgba Window { get; }
    public Rgba Panel { get; }
    public Rgba Pane { get; }
    public Rgba Dialog { get; }
    public Rgba? Flyout { get; }

    public Rgba Accent { get; }
    public Rgba Ink { get; }
    /// The pair as `#RRGGBB`, which is how the accent gate compares them.
    public string AccentHex { get; }
    public string InkHex { get; }

    public Rgba CaptionForeground { get; }
    public Rgba CaptionInactiveForeground { get; }
    public Rgba CaptionHoverBackground { get; }
    public Rgba CaptionPressedBackground { get; }

    public bool EditorIsStock { get; }
    public bool BrandCaret { get; }
    /// Empty when <see cref="EditorIsStock"/>.
    public IReadOnlyDictionary<string, Rgba> EditorColors { get; }
    /// The `setThemePalette` payload for editor.js: the flat palette plus
    /// `isDark`, `stock` and `brandCaret`, colours as CSS strings.
    public string EditorPaletteJson { get; }

    public IReadOnlyDictionary<string, Rgba> PreviewColors { get; }
    public string HighlightSheet { get; }
    public string MermaidTheme { get; }
    /// Sanitised. Empty when the theme carries none.
    public string Css { get; }

    private ResolvedTheme(ThemeDefinition def, string css)
    {
        Definition = def;
        Id = def.Id!;
        Name = def.Name!;
        NameResource = def.NameResource;
        LegacyNames = def.LegacyNames?.ToArray() ?? Array.Empty<string>();
        Version = def.Version!;
        Author = def.Author;
        Description = def.Description;
        Homepage = def.Homepage;
        License = def.License;
        MinMarknoteVersion = def.MinMarknoteVersion;
        Order = def.Order;

        Appearance = def.Appearance!.Trim().ToLowerInvariant();
        IsDark = Appearance == "dark";
        Material = string.IsNullOrWhiteSpace(def.Material) ? "none" : def.Material.Trim().ToLowerInvariant();

        var colors = def.Colors!;
        var chrome = colors.Chrome!;
        Window = Parse(chrome.Window, "colors.chrome.window");
        Panel = Parse(chrome.Panel, "colors.chrome.panel");
        Pane = Parse(chrome.Pane, "colors.chrome.pane");
        Dialog = Parse(chrome.Dialog, "colors.chrome.dialog");
        Flyout = string.IsNullOrWhiteSpace(chrome.Flyout) ? null : Parse(chrome.Flyout, "colors.chrome.flyout");

        Accent = Parse(colors.Accent!.Color, "colors.accent.color");
        Ink = Parse(colors.Accent.Ink, "colors.accent.ink");
        AccentHex = Accent.ToHex();
        InkHex = Ink.ToHex();

        // The caption defaults are what UpdateCaptionButtonColors has always
        // painted: white or black glyphs, #999999 when the window is inactive,
        // translucent hover fills of the glyph's own colour.
        var caption = colors.Caption;
        CaptionForeground = ParseOr(caption?.Foreground, "colors.caption.foreground",
            IsDark ? Rgba.White : Rgba.Black);
        CaptionInactiveForeground = ParseOr(caption?.InactiveForeground, "colors.caption.inactiveForeground",
            Rgba.Opaque(0x99, 0x99, 0x99));
        CaptionHoverBackground = ParseOr(caption?.HoverBackground, "colors.caption.hoverBackground",
            IsDark ? new Rgba(0x33, 0xFF, 0xFF, 0xFF) : new Rgba(0x14, 0x00, 0x00, 0x00));
        CaptionPressedBackground = ParseOr(caption?.PressedBackground, "colors.caption.pressedBackground",
            IsDark ? new Rgba(0x66, 0xFF, 0xFF, 0xFF) : new Rgba(0x33, 0x00, 0x00, 0x00));

        var editor = colors.Editor!;
        EditorIsStock = editor.Stock;
        BrandCaret = editor.BrandCaret;
        var editorColors = new Dictionary<string, Rgba>(StringComparer.Ordinal);
        if (!editor.Stock)
        {
            foreach (string key in ThemeSchema.EditorKeys)
                editorColors[key] = Parse(editor.Get(key), "colors.editor." + key);
        }
        EditorColors = editorColors;
        EditorPaletteJson = BuildEditorPaletteJson(editorColors);

        var previewColors = new Dictionary<string, Rgba>(StringComparer.Ordinal);
        foreach (string key in ThemeSchema.PreviewKeys)
            previewColors[key] = Parse(Lookup(colors.Preview!, key), "colors.preview." + key);
        PreviewColors = previewColors;

        HighlightSheet = string.IsNullOrWhiteSpace(colors.Code?.Highlight)
            ? ThemeSchema.DefaultHighlightSheet(IsDark)
            : colors.Code.Highlight.Trim().ToLowerInvariant();
        MermaidTheme = string.IsNullOrWhiteSpace(colors.Code?.Mermaid)
            ? ThemeSchema.DefaultMermaidTheme(IsDark)
            : colors.Code.Mermaid.Trim().ToLowerInvariant();

        Css = css;
    }

    /// Builds from a definition that <see cref="ThemeValidator"/> has passed
    /// and the CSS it sanitised. Throws on anything invalid — that is the
    /// validator's job, and reaching here with a bad file is a bug, not a
    /// user error.
    public static ResolvedTheme From(ThemeDefinition def, string sanitisedCss) => new(def, sanitisedCss);

    private static Rgba Parse(string? text, string path) =>
        ThemeSchema.TryParseColor(text, out var c)
            ? c
            : throw new InvalidOperationException($"{path} is not a colour: '{text}'. Validate before resolving.");

    private static Rgba ParseOr(string? text, string path, Rgba fallback) =>
        string.IsNullOrWhiteSpace(text) ? fallback : Parse(text, path);

    private static string? Lookup(Dictionary<string, string?> map, string key)
    {
        foreach (var (k, v) in map)
            if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) return v;
        return null;
    }

    private string BuildEditorPaletteJson(Dictionary<string, Rgba> editorColors)
    {
        var payload = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["isDark"] = IsDark,
            ["stock"] = EditorIsStock,
            ["brandCaret"] = BrandCaret,
        };
        foreach (var (key, colour) in editorColors)
            payload[key] = PreviewCssEmitter.CssColor(colour);
        return JsonSerializer.Serialize(payload);
    }
}
