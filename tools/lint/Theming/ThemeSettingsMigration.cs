using System;
using System.Collections.Generic;

namespace MarkdownViewer.Theming;

/// The theme-related fields as a settings file may hold them, from any
/// build: a single palette name from before issue #30, a mode and a pair
/// of names from after it, ids from the rewrite on, and the retired
/// Backdrop setting from before issue #10.
public sealed record LegacyThemeSettings(
    string? ThemePreference,
    string? ThemeMode,
    string? LightTheme,
    string? DarkTheme,
    string? Backdrop);

public sealed record ThemeMigrationResult(ThemeSelection Selection, string Backdrop, bool Changed);

/// Brings any older settings file forward to a mode and a pair of theme
/// ids. Idempotent: run on its own output it changes nothing, which is
/// what lets it run on every launch instead of guarding a one-off flag.
///
/// Three folds, in the order the app applied them historically:
///  1. Backdrop (issue #10): an explicit Light + material combination
///     becomes the material theme it always secretly was.
///  2. The single palette (issue #30): a pre-split file names one palette,
///     which says both which side and which theme; the other side is
///     seeded from the palette's family so somebody on Nord Dark gets Nord
///     Light, as the toggle would have given them.
///  3. Names to ids, clamped to the side each belongs to.
public static class ThemeSettingsMigration
{
    /// The light/dark counterpart of each legacy name, for seeding the
    /// other side of a pre-split file. Material themes flip to their
    /// closest opposite-side material; palettes without a counterpart
    /// fall to the plain theme. Legacy names only — an id never needs this.
    private static readonly Dictionary<string, string> Counterpart = new(StringComparer.Ordinal)
    {
        ["Light"] = "Dark",
        ["Dark"] = "Light",
        ["Light Acrylic"] = "Dark Acrylic",
        ["Dark Acrylic"] = "Light Acrylic",
        ["Light Mica Alt"] = "Dark Mica",
        ["Dark Mica"] = "Light",            // Light IS Mica
        ["Light Solid"] = "Dark",           // both are the solid pair
        ["Nord"] = "Nord Light",
        ["Nord Dark"] = "Nord Light",
        ["Nord Light"] = "Nord Dark",
        ["Solarized Dark"] = "Solarized Light",
        ["Solarized Light"] = "Solarized Dark",
        ["Gruvbox Dark"] = "Gruvbox Light",
        ["Gruvbox Light"] = "Gruvbox Dark",
        ["Dracula"] = "Light",              // no light Dracula
        ["City Lights"] = "Light",          // dark only
        ["Aspire Dark"] = "Aspire Light",
        ["Aspire Light"] = "Aspire Dark",
    };

    public static ThemeMigrationResult Run(LegacyThemeSettings s, ThemeCatalog catalog)
    {
        string pref = s.ThemePreference ?? string.Empty;
        string backdrop = string.IsNullOrEmpty(s.Backdrop) ? "Mica" : s.Backdrop;

        // 1. Backdrop fold. Only an explicit Light + non-default material
        //    carries over, and only from a file that predates the mode split
        //    (the fold predates it too, so any later file has been through
        //    it). The retired field then resets whatever it held, so it is
        //    inert from here on.
        bool preSplit = string.IsNullOrEmpty(s.ThemeMode);
        if (preSplit && pref == "Light" && backdrop is "Acrylic" or "MicaAlt" or "None")
        {
            pref = backdrop switch
            {
                "Acrylic" => "Light Acrylic",
                "MicaAlt" => "Light Mica Alt",
                _ => "Light Solid",
            };
        }
        backdrop = "Mica";

        // 2. The single palette, when the mode was never written.
        string mode;
        string? lightValue, darkValue;
        if (string.IsNullOrEmpty(s.ThemeMode))
        {
            var chosen = catalog.FindByIdOrLegacyName(pref);
            if (chosen is not null)
            {
                mode = chosen.IsDark ? ThemeSelection.Dark : ThemeSelection.Light;
                string? other = chosen.LegacyNames.Count > 0 && Counterpart.TryGetValue(chosen.LegacyNames[0], out var c) ? c : null;
                lightValue = chosen.IsDark ? other : chosen.Id;
                darkValue = chosen.IsDark ? chosen.Id : other;
            }
            else
            {
                // "Default", empty, or unrecognised: they were following
                // Windows with the stock pair, so keep doing that.
                mode = ThemeSelection.Follow;
                lightValue = ThemeCatalog.DefaultLightId;
                darkValue = ThemeCatalog.DefaultDarkId;
            }
        }
        else
        {
            mode = ThemeSelection.NormaliseMode(s.ThemeMode);
            lightValue = s.LightTheme;
            darkValue = s.DarkTheme;
        }

        // 3. Names to ids, each clamped to its side.
        string lightId = SideId(catalog, lightValue, dark: false);
        string darkId = SideId(catalog, darkValue, dark: true);

        var selection = new ThemeSelection(mode, lightId, darkId);
        bool changed = mode != (s.ThemeMode ?? string.Empty)
                    || lightId != (s.LightTheme ?? string.Empty)
                    || darkId != (s.DarkTheme ?? string.Empty)
                    || backdrop != (s.Backdrop ?? string.Empty);
        return new ThemeMigrationResult(selection, backdrop, changed);
    }

    private static string SideId(ThemeCatalog catalog, string? value, bool dark)
    {
        var found = catalog.FindByIdOrLegacyName(value);
        return found is not null && found.IsDark == dark ? found.Id : catalog.DefaultFor(dark).Id;
    }
}
