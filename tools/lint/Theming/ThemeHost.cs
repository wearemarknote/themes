using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MarkdownViewer.Theming;

/// One folder under the themes root, whether or not it holds a usable theme.
/// A folder that does not is still listed, with the reason, so a broken
/// install shows as a status card rather than vanishing.
public sealed record InstalledTheme(
    string FolderPath,
    ThemeStatus Status,
    string? Detail,
    ResolvedTheme? Theme,
    ThemeDefinition? Definition)
{
    public bool IsOk => Status == ThemeStatus.Ok;

    /// The best name available: the theme's, or whatever parsed, or the
    /// folder's.
    public string DisplayName =>
        Theme?.Name
        ?? (string.IsNullOrWhiteSpace(Definition?.Name) ? null : Definition!.Name)
        ?? Path.GetFileName(FolderPath);
}

/// Discovers installed themes on disk — one folder per theme, named for its
/// id, holding `theme.json` — the way PluginHost discovers plugins. Owns
/// only discovery and validation; ThemeInstaller writes, ThemeService
/// applies.
public sealed class ThemeHost
{
    public const string ThemeFileName = "theme.json";

    /// `%LOCALAPPDATA%\Marknote\themes`. Created lazily by the installer;
    /// discovering an absent folder is an empty list, not an error.
    public static string DefaultFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Marknote",
        "themes");

    public string Folder { get; }

    public IReadOnlyList<InstalledTheme> Installed { get; private set; } = Array.Empty<InstalledTheme>();

    public ThemeHost(string? folder = null)
    {
        Folder = string.IsNullOrWhiteSpace(folder) ? DefaultFolder : folder;
    }

    /// The folder a theme with this id lives in. The id has been validated
    /// as reverse-DNS, so it is a safe single path segment.
    public static string FolderFor(string root, string id) => Path.Combine(root, id);

    /// Walks the folder and populates <see cref="Installed"/>: usable themes
    /// first by name, then the folders with a problem. Never throws.
    public void Discover(string runningAppVersion)
    {
        if (!Directory.Exists(Folder))
        {
            Installed = Array.Empty<InstalledTheme>();
            return;
        }

        var found = new List<InstalledTheme>();
        try
        {
            foreach (string dir in Directory.EnumerateDirectories(Folder))
                found.Add(Inspect(dir, runningAppVersion));
        }
        catch (Exception ex)
        {
            found.Add(new InstalledTheme(Folder, ThemeStatus.InvalidJson, $"The themes folder could not be read: {ex.Message}", null, null));
        }

        Installed = found
            .OrderBy(t => t.IsOk ? 0 : 1)
            .ThenBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static InstalledTheme Inspect(string folder, string runningAppVersion)
    {
        string file = Path.Combine(folder, ThemeFileName);
        if (!File.Exists(file))
            return new InstalledTheme(folder, ThemeStatus.InvalidJson, $"{ThemeFileName} not found in the theme folder.", null, null);

        string json;
        try { json = File.ReadAllText(file); }
        catch (Exception ex)
        {
            return new InstalledTheme(folder, ThemeStatus.InvalidJson, $"{ThemeFileName} could not be read: {ex.Message}", null, null);
        }

        var v = ThemeValidator.Parse(json, runningAppVersion);
        if (!v.IsOk)
            return new InstalledTheme(folder, v.Status, v.Detail, null, v.Definition);

        var theme = v.Theme!;
        if (ThemeSchema.IsReservedId(theme.Id))
            return new InstalledTheme(folder, ThemeStatus.IdReserved,
                $"'{theme.Id}' is under {ThemeSchema.ReservedIdPrefix}, which only Marknote's built-in themes may use.", null, v.Definition);

        string folderName = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.Equals(folderName, theme.Id, StringComparison.Ordinal))
            return new InstalledTheme(folder, ThemeStatus.IdMismatch,
                $"The folder is named '{folderName}' but the theme's id is '{theme.Id}'. Rename the folder to match.", null, v.Definition);

        return new InstalledTheme(folder, ThemeStatus.Ok, null, theme, v.Definition);
    }
}
