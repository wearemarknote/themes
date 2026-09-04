using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MarkdownViewer.Theming;

/// One theme as the marketplace lists it. The site generates the index
/// from the files it serves, so `sha256` and `sizeBytes` describe the
/// exact bytes at `downloadUrl`.
public sealed class ThemeIndexEntry
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }
    [JsonPropertyName("author")] public string? Author { get; set; }
    /// `marknote` or `community`.
    [JsonPropertyName("authorKind")] public string? AuthorKind { get; set; }
    [JsonPropertyName("appearance")] public string? Appearance { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("minMarknoteVersion")] public string? MinMarknoteVersion { get; set; }
    [JsonPropertyName("downloadUrl")] public string? DownloadUrl { get; set; }
    [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
    [JsonPropertyName("sizeBytes")] public long? SizeBytes { get; set; }
    [JsonPropertyName("previewUrl")] public string? PreviewUrl { get; set; }
    [JsonPropertyName("swatches")] public ThemeSwatches? Swatches { get; set; }
    [JsonPropertyName("homepage")] public string? Homepage { get; set; }
    [JsonPropertyName("detailUrl")] public string? DetailUrl { get; set; }

    public bool IsDark => string.Equals(Appearance, "dark", StringComparison.OrdinalIgnoreCase);

    public bool IsCompatibleWith(string runningAppVersion) =>
        string.IsNullOrWhiteSpace(MinMarknoteVersion) || AppVersion.Compare(MinMarknoteVersion, runningAppVersion) <= 0;

    /// Newer than the installed copy, by version.
    public bool IsNewerThan(ResolvedTheme installed) =>
        !string.IsNullOrWhiteSpace(Version) && AppVersion.Compare(Version, installed.Version) > 0;
}

/// Four colours for a gallery card, so a listing can show a theme's look
/// without a screenshot.
public sealed class ThemeSwatches
{
    [JsonPropertyName("bg")] public string? Bg { get; set; }
    [JsonPropertyName("panel")] public string? Panel { get; set; }
    [JsonPropertyName("fg")] public string? Fg { get; set; }
    [JsonPropertyName("accent")] public string? Accent { get; set; }
}

public sealed class ThemeIndex
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("generated")] public string? Generated { get; set; }
    [JsonPropertyName("themes")] public List<ThemeIndexEntry> Themes { get; set; } = new();

    /// Parses an index, dropping any entry that could not be installed
    /// anyway — no id, no download, no hash, or an id that is not
    /// reverse-DNS. Null when the document is not an index at all.
    public static ThemeIndex? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        ThemeIndex? index;
        try { index = JsonSerializer.Deserialize<ThemeIndex>(json, ThemeValidator.JsonOptions); }
        catch (JsonException) { return null; }
        if (index is null || index.SchemaVersion != CurrentSchemaVersion) return null;

        index.Themes = index.Themes
            .Where(t => t is not null
                     && ThemeSchema.IsValidId(t.Id)
                     && !string.IsNullOrWhiteSpace(t.Name)
                     && Uri.TryCreate(t.DownloadUrl, UriKind.Absolute, out _)
                     && ThemeInstaller.TryNormaliseSha256(t.Sha256, out _))
            .ToList();
        return index;
    }
}

public sealed record ThemeIndexFetch(ThemeIndex? Index, bool FromCache, DateTimeOffset? CachedAt, string? Error)
{
    public bool HasIndex => Index is not null;
}

/// Fetches the marketplace index — only when asked, never at launch — and
/// keeps the last good copy on disk so the gallery still opens offline,
/// saying how old what it shows is.
public sealed class ThemeIndexClient
{
    public const string DefaultIndexUrl = "https://marknote.md/themes/index.json";

    private readonly HttpClient _http;
    private readonly Uri _indexUrl;
    private readonly string _cachePath;

    public ThemeIndexClient(Uri indexUrl, string cachePath, HttpMessageHandler? handler = null)
    {
        _indexUrl = indexUrl;
        _cachePath = cachePath;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<ThemeIndexFetch> FetchAsync(CancellationToken ct = default)
    {
        string? error = null;
        try
        {
            string json = await _http.GetStringAsync(_indexUrl, ct);
            var index = ThemeIndex.Parse(json);
            if (index is not null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
                    File.WriteAllText(_cachePath, json);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* the cache is a convenience */ }
                return new ThemeIndexFetch(index, FromCache: false, null, null);
            }
            error = "The listing could not be read.";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            error = ex.Message;
        }

        // Fall back to the last copy that parsed.
        try
        {
            if (File.Exists(_cachePath))
            {
                var cached = ThemeIndex.Parse(File.ReadAllText(_cachePath));
                if (cached is not null)
                    return new ThemeIndexFetch(cached, FromCache: true, File.GetLastWriteTime(_cachePath), error);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* no cache, then */ }

        return new ThemeIndexFetch(null, FromCache: false, null, error);
    }
}
