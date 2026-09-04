using System;
using System.Collections.Generic;

namespace MarkdownViewer.Theming;

/// What the user asked for: which side (`Follow` Windows, `Light`, `Dark`)
/// and which theme on each side. Ids after migration; legacy names are
/// still understood while the settings on disk hold them.
public sealed record ThemeSelection(string Mode, string LightId, string DarkId)
{
    public const string Follow = "Follow";
    public const string Light = "Light";
    public const string Dark = "Dark";

    /// Anything that is not exactly Light or Dark is Follow — including the
    /// empty string a pre-split settings file carries.
    public static string NormaliseMode(string? mode) => mode switch
    {
        Light => Light,
        Dark => Dark,
        _ => Follow,
    };
}

/// The outcome of resolving a selection: the theme to show, and the
/// selection as it should now be stored — clamped to themes that exist
/// and sit on the right side.
public sealed record ThemeResolution(
    ResolvedTheme Theme,
    bool IsDark,
    ThemeSelection Selection,
    bool SelectionChanged,
    IReadOnlyList<string> Notes);

/// Turns a selection into a theme. The one place that knows how the mode
/// and the pair combine, and the one place a missing or mis-sided theme is
/// replaced by the side's default — the clamp MigrateThemeSettings used to
/// do, now applied every time so a removed marketplace theme can never
/// leave a picker showing an impossible value.
public static class ThemeResolver
{
    public static ThemeResolution Resolve(ThemeCatalog catalog, ThemeSelection selection, bool systemIsDark)
    {
        var notes = new List<string>();
        string mode = ThemeSelection.NormaliseMode(selection.Mode);

        var light = Side(catalog, selection.LightId, dark: false, notes);
        var dark = Side(catalog, selection.DarkId, dark: true, notes);

        bool isDark = mode switch
        {
            ThemeSelection.Light => false,
            ThemeSelection.Dark => true,
            _ => systemIsDark,
        };

        var clamped = new ThemeSelection(mode, light.Id, dark.Id);
        return new ThemeResolution(
            isDark ? dark : light,
            isDark,
            clamped,
            SelectionChanged: clamped != selection,
            notes);
    }

    private static ResolvedTheme Side(ThemeCatalog catalog, string? value, bool dark, List<string> notes)
    {
        var found = catalog.FindByIdOrLegacyName(value);
        if (found is null)
        {
            if (!string.IsNullOrEmpty(value))
                notes.Add($"'{value}' is not an installed theme; using {(dark ? "Dark" : "Light")}.");
            return catalog.DefaultFor(dark);
        }
        if (found.IsDark != dark)
        {
            notes.Add($"'{found.Name}' is a {found.Appearance} theme and cannot be the {(dark ? "dark" : "light")} choice; using {(dark ? "Dark" : "Light")}.");
            return catalog.DefaultFor(dark);
        }
        return found;
    }
}
