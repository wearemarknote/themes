using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MarkdownViewer.Theming;

/// A theme file that did not make it into the catalogue, and why. Shown
/// as a status card so a broken file is visible rather than silent.
public sealed record ThemeLoadProblem(string Path, ThemeStatus Status, string? Detail);

/// Every theme the app can show: the built-ins, and later the installed
/// ones. Answers by id — and, until every setting holds an id, by the
/// name a theme was saved under in older builds.
public sealed class ThemeCatalog
{
    public const string DefaultLightId = "uk.marknote.light";
    public const string DefaultDarkId = "uk.marknote.dark";

    private readonly List<ResolvedTheme> _themes = new();
    private readonly Dictionary<string, ResolvedTheme> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResolvedTheme> _byLegacyName = new(StringComparer.Ordinal);
    private readonly List<ThemeLoadProblem> _problems = new();

    public IReadOnlyList<ResolvedTheme> All => _themes;
    public IReadOnlyList<ThemeLoadProblem> Problems => _problems;

    public ThemeCatalog(IEnumerable<ResolvedTheme> themes, IEnumerable<ThemeLoadProblem>? problems = null)
    {
        foreach (var t in themes) Add(t, "(memory)");
        if (problems is not null) _problems.AddRange(problems);
    }

    private void Add(ResolvedTheme t, string source)
    {
        // First one in wins; a second file claiming the same id is reported,
        // not merged, so an installed theme can never shadow a built-in.
        if (_byId.ContainsKey(t.Id))
        {
            _problems.Add(new(source, ThemeStatus.InvalidId, $"Another theme already uses the id '{t.Id}'."));
            return;
        }
        _themes.Add(t);
        _byId[t.Id] = t;
        foreach (string name in t.LegacyNames)
            _byLegacyName.TryAdd(name, t);
    }

    public ResolvedTheme? Find(string? id) =>
        id is not null && _byId.TryGetValue(id, out var t) ? t : null;

    public ResolvedTheme? FindByLegacyName(string? name) =>
        name is not null && _byLegacyName.TryGetValue(name, out var t) ? t : null;

    /// A setting may hold an id (any build from the rewrite on) or a legacy
    /// name (any build before it). An id always contains a dot; a name
    /// never does, so the two cannot collide.
    public ResolvedTheme? FindByIdOrLegacyName(string? value) =>
        Find(value) ?? FindByLegacyName(value);

    /// One side of the picker, in picker order: built-in order first, then
    /// by name for everything without one.
    public IReadOnlyList<ResolvedTheme> ForSide(bool dark) =>
        _themes.Where(t => t.IsDark == dark)
               .OrderBy(t => t.Order == 0 ? int.MaxValue : t.Order)
               .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
               .ToList();

    /// The theme a side falls back to when what was asked for is missing
    /// or on the wrong side. Marknote's own Light and Dark; failing those
    /// (a damaged install), whatever that side has.
    public ResolvedTheme DefaultFor(bool dark) =>
        Find(dark ? DefaultDarkId : DefaultLightId)
        ?? ForSide(dark).FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"No {(dark ? "dark" : "light")} theme is available — the built-in theme files are missing.");

    /// Reads every `*.json` in a folder. Never throws: a missing folder is
    /// an empty catalogue with one problem; a bad file is one problem.
    public static ThemeCatalog LoadFolder(string folder, string runningAppVersion)
    {
        var catalog = new ThemeCatalog(Array.Empty<ResolvedTheme>());
        if (!Directory.Exists(folder))
        {
            catalog._problems.Add(new(folder, ThemeStatus.InvalidJson, "The theme folder does not exist."));
            return catalog;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*.json");
            Array.Sort(files, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            catalog._problems.Add(new(folder, ThemeStatus.InvalidJson, $"The theme folder could not be read: {ex.Message}"));
            return catalog;
        }

        foreach (string file in files)
        {
            string json;
            try { json = File.ReadAllText(file); }
            catch (Exception ex)
            {
                catalog._problems.Add(new(file, ThemeStatus.InvalidJson, $"The file could not be read: {ex.Message}"));
                continue;
            }

            var v = ThemeValidator.Parse(json, runningAppVersion);
            if (v.IsOk) catalog.Add(v.Theme!, file);
            else catalog._problems.Add(new(file, v.Status, v.Detail));
        }
        return catalog;
    }
}
