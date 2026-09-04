using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MarkdownViewer.Theming;

/// Why a theme file did or did not validate. Surfaced as-is in Settings →
/// Themes for an installed theme, and by the lint for a submission.
public enum ThemeStatus
{
    Ok,
    /// Not JSON, or empty.
    InvalidJson,
    /// A required field or colour key is absent; Detail names it.
    MissingRequiredField,
    /// Written for a schema this Marknote does not read.
    UnsupportedSchemaVersion,
    /// `minMarknoteVersion` is newer than the running app.
    UnsupportedAppVersion,
    /// `id` is not reverse-DNS.
    InvalidId,
    /// A colour that is not `#RRGGBB`, `#AARRGGBB` or `transparent`; Detail names it.
    InvalidColour,
    /// `appearance`, `material`, `code.highlight` or `code.mermaid` is not an allowed value.
    UnknownValue,
    /// The css block failed <see cref="CssSanitiser"/>; Detail lists why.
    CssRejected,
    /// An installed theme's folder is not named for its id.
    IdMismatch,
    /// The id is under `uk.marknote.`, which only built-ins may use.
    IdReserved,
}

public sealed class ThemeValidation
{
    public required ThemeStatus Status { get; init; }
    /// Human-readable, renderable as-is. Null when Ok.
    public string? Detail { get; init; }
    /// Things worth telling an author that do not stop the theme applying:
    /// unknown keys, and surfaces under the contrast bar.
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    /// Whatever parsed, even when validation failed, so a status card can
    /// still show the theme's name.
    public ThemeDefinition? Definition { get; init; }
    /// Set when and only when <see cref="Status"/> is Ok.
    public ResolvedTheme? Theme { get; init; }

    public bool IsOk => Status == ThemeStatus.Ok;
}

/// Reads a theme file and either resolves it or says exactly what is wrong.
/// Never throws: a broken file on disk or a hostile download is an ordinary
/// outcome here, reported as a status, not an exception.
public static class ThemeValidator
{
    /// Case-insensitive, comments and trailing commas tolerated — the same
    /// leniency plugin manifests get.
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ThemeValidation Parse(string? json, string runningAppVersion)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Fail(ThemeStatus.InvalidJson, "The theme file is empty.", null);

        ThemeDefinition? def;
        try
        {
            def = JsonSerializer.Deserialize<ThemeDefinition>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            return Fail(ThemeStatus.InvalidJson, $"The theme file is not valid JSON: {ex.Message}", null);
        }
        return Validate(def, runningAppVersion);
    }

    public static ThemeValidation Validate(ThemeDefinition? def, string runningAppVersion)
    {
        if (def is null)
            return Fail(ThemeStatus.InvalidJson, "The theme file is empty.", null);

        if (def.SchemaVersion != ThemeSchema.CurrentSchemaVersion)
            return Fail(ThemeStatus.UnsupportedSchemaVersion,
                $"schemaVersion {def.SchemaVersion} is not supported; this version of Marknote reads schemaVersion {ThemeSchema.CurrentSchemaVersion}.", def);

        foreach (var (field, value) in new[] { ("id", def.Id), ("name", def.Name), ("version", def.Version), ("appearance", def.Appearance) })
        {
            if (string.IsNullOrWhiteSpace(value))
                return Fail(ThemeStatus.MissingRequiredField, field, def);
        }

        if (!ThemeSchema.IsValidId(def.Id))
            return Fail(ThemeStatus.InvalidId,
                $"id '{def.Id}' must be reverse-DNS: lower-case letters, digits and hyphens in dot-separated segments, such as com.example.my-theme.", def);

        string appearance = def.Appearance!.Trim().ToLowerInvariant();
        if (!ThemeSchema.Appearances.Contains(appearance))
            return Fail(ThemeStatus.UnknownValue, $"appearance '{def.Appearance}' — expected light or dark.", def);

        string material = string.IsNullOrWhiteSpace(def.Material) ? "none" : def.Material.Trim().ToLowerInvariant();
        if (!ThemeSchema.Materials.Contains(material))
            return Fail(ThemeStatus.UnknownValue, $"material '{def.Material}' — expected one of {string.Join(", ", ThemeSchema.Materials)}.", def);

        if (!string.IsNullOrWhiteSpace(def.MinMarknoteVersion)
            && AppVersion.Compare(def.MinMarknoteVersion, runningAppVersion) > 0)
            return Fail(ThemeStatus.UnsupportedAppVersion,
                $"This theme requires Marknote {def.MinMarknoteVersion} or newer. You're on {runningAppVersion}.", def);

        var colors = def.Colors;
        if (colors is null) return Fail(ThemeStatus.MissingRequiredField, "colors", def);

        var warnings = new List<string>();

        // Chrome.
        var chrome = colors.Chrome;
        if (chrome is null) return Fail(ThemeStatus.MissingRequiredField, "colors.chrome", def);
        foreach (var (path, value) in new[]
        {
            ("colors.chrome.window", chrome.Window),
            ("colors.chrome.panel", chrome.Panel),
            ("colors.chrome.pane", chrome.Pane),
            ("colors.chrome.dialog", chrome.Dialog),
        })
        {
            if (ColourProblem(path, value, required: true) is { } fail) return Fail(fail.Status, fail.Detail, def);
        }
        if (ColourProblem("colors.chrome.flyout", chrome.Flyout, required: false) is { } flyoutFail)
            return Fail(flyoutFail.Status, flyoutFail.Detail, def);

        // Accent.
        var accent = colors.Accent;
        if (accent is null) return Fail(ThemeStatus.MissingRequiredField, "colors.accent", def);
        if (ColourProblem("colors.accent.color", accent.Color, required: true) is { } accentFail) return Fail(accentFail.Status, accentFail.Detail, def);
        if (ColourProblem("colors.accent.ink", accent.Ink, required: true) is { } inkFail) return Fail(inkFail.Status, inkFail.Detail, def);
        if (ThemeSchema.TryParseColor(accent.Color, out var accentColour) && accentColour.A != 0xFF)
            return Fail(ThemeStatus.InvalidColour, "colors.accent.color must be opaque.", def);

        // Caption (optional block, but what is there must parse).
        if (colors.Caption is { } caption)
        {
            foreach (var (path, value) in new[]
            {
                ("colors.caption.foreground", caption.Foreground),
                ("colors.caption.inactiveForeground", caption.InactiveForeground),
                ("colors.caption.hoverBackground", caption.HoverBackground),
                ("colors.caption.pressedBackground", caption.PressedBackground),
            })
            {
                if (ColourProblem(path, value, required: false) is { } fail) return Fail(fail.Status, fail.Detail, def);
            }
        }

        // Editor.
        var editor = colors.Editor;
        if (editor is null) return Fail(ThemeStatus.MissingRequiredField, "colors.editor", def);
        if (!editor.Stock)
        {
            foreach (string key in ThemeSchema.EditorKeys)
            {
                if (ColourProblem("colors.editor." + key, editor.Get(key), required: true) is { } fail)
                    return Fail(fail.Status, fail.Detail, def);
            }
        }
        if (editor.Colors is not null)
        {
            foreach (string key in editor.Colors.Keys)
            {
                if (!ThemeSchema.EditorKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                    warnings.Add($"colors.editor.{key} is not a key the editor reads and is ignored.");
            }
        }

        // Preview.
        var preview = colors.Preview;
        if (preview is null) return Fail(ThemeStatus.MissingRequiredField, "colors.preview", def);
        foreach (string key in ThemeSchema.PreviewKeys)
        {
            string? value = preview.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
            if (ColourProblem("colors.preview." + key, value, required: true) is { } fail)
                return Fail(fail.Status, fail.Detail, def);
        }
        foreach (string key in preview.Keys)
        {
            if (!ThemeSchema.PreviewKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                warnings.Add($"colors.preview.{key} is not a variable the preview reads and is ignored.");
        }

        // Code styles.
        if (colors.Code is { } code)
        {
            if (!string.IsNullOrWhiteSpace(code.Highlight)
                && !ThemeSchema.HighlightSheets.Contains(code.Highlight.Trim().ToLowerInvariant()))
                return Fail(ThemeStatus.UnknownValue,
                    $"colors.code.highlight '{code.Highlight}' — expected one of {string.Join(", ", ThemeSchema.HighlightSheets)}.", def);
            if (!string.IsNullOrWhiteSpace(code.Mermaid)
                && !ThemeSchema.MermaidThemes.Contains(code.Mermaid.Trim().ToLowerInvariant()))
                return Fail(ThemeStatus.UnknownValue,
                    $"colors.code.mermaid '{code.Mermaid}' — expected one of {string.Join(", ", ThemeSchema.MermaidThemes)}.", def);
        }

        // CSS.
        var css = CssSanitiser.Sanitise(def.Css);
        if (!css.IsClean)
            return Fail(ThemeStatus.CssRejected, string.Join(" ", css.Rejections), def);

        var theme = ResolvedTheme.From(def, css.Css);

        foreach (var check in ThemeContrast.Measure(theme))
        {
            if (!check.Passes) warnings.Add("Below the contrast bar — " + check);
        }

        return new ThemeValidation
        {
            Status = ThemeStatus.Ok,
            Definition = def,
            Theme = theme,
            Warnings = warnings,
        };
    }

    private static (ThemeStatus Status, string Detail)? ColourProblem(string path, string? value, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
            return required ? (ThemeStatus.MissingRequiredField, path) : null;
        if (!ThemeSchema.TryParseColor(value, out _))
            return (ThemeStatus.InvalidColour, $"{path}: '{value}' is not a colour. Use #RRGGBB, #AARRGGBB or transparent.");
        return null;
    }

    private static ThemeValidation Fail(ThemeStatus status, string detail, ThemeDefinition? def) => new()
    {
        Status = status,
        Detail = detail,
        Definition = def,
    };
}
