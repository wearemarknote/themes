using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MarkdownViewer.Theming;

public enum ThemeInstallOutcome
{
    Installed,
    /// The same id was already installed and has been replaced.
    Updated,
    /// Not https, or not a host the app installs from.
    RefusedUrl,
    DownloadFailed,
    /// Larger than <see cref="ThemeInstaller.MaxBytes"/>.
    TooLarge,
    /// The bytes did not hash to what the caller said they would.
    HashMismatch,
    /// Downloaded and hashed fine, but not a valid theme; Detail says why.
    Invalid,
    /// A `uk.marknote.*` id, which only built-ins may carry.
    Reserved,
    /// Writing to disk failed.
    WriteFailed,
    Cancelled,
}

public sealed record ThemeInstallResult(
    ThemeInstallOutcome Outcome,
    string? Detail,
    ResolvedTheme? Theme,
    string? FolderPath)
{
    public bool IsOk => Outcome is ThemeInstallOutcome.Installed or ThemeInstallOutcome.Updated;
}

/// Downloads a theme file and installs it — or refuses, for one stated
/// reason. Every refusal happens before anything is written: the file on
/// disk is only ever a theme that came from an allowed host, hashed to the
/// value the index promised, validated in full, and carries an id the
/// author may use.
public sealed class ThemeInstaller
{
    /// A theme is one small JSON file; four times the CSS ceiling is room
    /// for every palette key and a generous description.
    public const int MaxBytes = 256 * 1024;

    /// Where the app will download a theme from. A `marknote://install-theme`
    /// link on any web page reaches the app, so the app decides where it
    /// will fetch from, not the page.
    public static readonly string[] AllowedHosts = ["marknote.md", "www.marknote.md"];

    private readonly HttpClient _http;
    private readonly string _folder;
    private readonly string _runningAppVersion;

    public ThemeInstaller(string folder, string runningAppVersion, HttpMessageHandler? handler = null)
    {
        _folder = folder;
        _runningAppVersion = runningAppVersion;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public static bool IsAllowedUrl(Uri? url) =>
        url is not null
        && url.IsAbsoluteUri
        && string.Equals(url.Scheme, "https", StringComparison.OrdinalIgnoreCase)
        && AllowedHosts.Contains(url.Host, StringComparer.OrdinalIgnoreCase);

    /// Downloads, checks and validates, then — if <paramref name="confirm"/>
    /// is given — asks before writing, with the theme as it really is: the
    /// name and author a confirmation shows come from the verified bytes,
    /// not from whoever built the link.
    public async Task<ThemeInstallResult> InstallAsync(
        Uri url, string expectedSha256,
        Func<ResolvedTheme, Task<bool>>? confirm = null,
        CancellationToken ct = default)
    {
        if (!IsAllowedUrl(url))
            return new(ThemeInstallOutcome.RefusedUrl,
                $"Themes are only installed over https from {string.Join(" or ", AllowedHosts)}.", null, null);

        if (!TryNormaliseSha256(expectedSha256, out byte[] expected))
            return new(ThemeInstallOutcome.HashMismatch, "The expected SHA-256 is not a 64-digit hex value.", null, null);

        byte[] bytes;
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return new(ThemeInstallOutcome.DownloadFailed, $"The server answered {(int)response.StatusCode}.", null, null);

            long? length = response.Content.Headers.ContentLength;
            if (length > MaxBytes)
                return new(ThemeInstallOutcome.TooLarge, $"The theme is {length:N0} bytes; the limit is {MaxBytes:N0}.", null, null);

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes)
                    return new(ThemeInstallOutcome.TooLarge, $"The theme is larger than {MaxBytes:N0} bytes.", null, null);
            }
            bytes = buffer.ToArray();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new(ThemeInstallOutcome.Cancelled, null, null, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return new(ThemeInstallOutcome.DownloadFailed, ex.Message, null, null);
        }

        byte[] actual = SHA256.HashData(bytes);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            return new(ThemeInstallOutcome.HashMismatch,
                "The downloaded file does not match the hash the listing promised. Nothing was installed.", null, null);

        string json = Encoding.UTF8.GetString(bytes);
        if (json.Length > 0 && json[0] == '﻿') json = json[1..];
        var v = ThemeValidator.Parse(json, _runningAppVersion);
        if (!v.IsOk)
            return new(ThemeInstallOutcome.Invalid, $"{v.Status}: {v.Detail}", null, null);

        var theme = v.Theme!;
        if (ThemeSchema.IsReservedId(theme.Id))
            return new(ThemeInstallOutcome.Reserved,
                $"'{theme.Id}' is under {ThemeSchema.ReservedIdPrefix}, which only Marknote's built-in themes may use.", null, null);

        if (confirm is not null && !await confirm(theme))
            return new(ThemeInstallOutcome.Cancelled, null, theme, null);

        string folder = ThemeHost.FolderFor(_folder, theme.Id);
        string file = Path.Combine(folder, ThemeHost.ThemeFileName);
        bool existed = File.Exists(file);
        try
        {
            Directory.CreateDirectory(folder);
            // Write beside, then move over: a theme is either wholly the old
            // file or wholly the new one, never a torn write.
            string tmp = file + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(ThemeInstallOutcome.WriteFailed, ex.Message, null, null);
        }

        return new(existed ? ThemeInstallOutcome.Updated : ThemeInstallOutcome.Installed, null, theme, folder);
    }

    /// Removes an installed theme's folder. Only an id that validates as
    /// reverse-DNS is ever used as a path, so nothing outside the themes
    /// folder can be named.
    public static bool TryUninstall(string root, string id, out string? error)
    {
        error = null;
        if (!ThemeSchema.IsValidId(id) || ThemeSchema.IsReservedId(id))
        {
            error = $"'{id}' is not an installed theme id.";
            return false;
        }
        string folder = ThemeHost.FolderFor(root, id);
        if (!Directory.Exists(folder)) return true;
        try
        {
            Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// Accepts `hex`, `HEX`, or `sha256:hex`; must be exactly 32 bytes.
    public static bool TryNormaliseSha256(string? text, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Trim();
        if (s.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) s = s[7..];
        if (s.Length != 64) return false;
        try { bytes = Convert.FromHexString(s); }
        catch (FormatException) { return false; }
        return bytes.Length == 32;
    }

    public static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
