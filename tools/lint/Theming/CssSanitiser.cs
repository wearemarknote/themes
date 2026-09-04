using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MarkdownViewer.Theming;

public sealed record CssSanitiseResult(string Css, IReadOnlyList<string> Rejections)
{
    public bool IsClean => Rejections.Count == 0;
}

/// Decides whether a theme's CSS block may reach the preview.
///
/// The design rule is that themes are data, not code. CSS is the one place
/// that rule is soft — a stylesheet can fetch (`url()`, `@import`,
/// `@font-face`), and historically could run script. So the block is held
/// to a short list of things it may not contain, and a theme that contains
/// one is REJECTED, with the reason, rather than quietly edited: an author
/// learns what was wrong, and nothing the reviewer did not read ever paints.
///
/// The checks run on a copy with comments removed, lower-cased and with
/// whitespace collapsed, so `@im/**/port`, `URL (` and `@IMPORT` are all the
/// same thing. Any backslash is refused outright — CSS escapes are the
/// standard way to spell a forbidden token so a filter does not see it, and
/// no theme has a legitimate use for one.
///
/// The preview page's Content-Security-Policy is the second wall behind
/// this: `style-src` and `font-src` are pinned to the app's own assets and
/// `connect-src` is `none`, so even a bypass here cannot reach the network.
public static class CssSanitiser
{
    /// Substrings that end a review. Each is a way to fetch, execute, or
    /// break out of the style element.
    private static readonly string[] ForbiddenTokens =
    [
        "@import", "@font-face", "@namespace", "@charset",
        "url(", "url (", "image-set(", "image-set (", "src:",
        "expression(", "expression (", "-moz-binding", "behavior:", "behaviour:",
        "javascript:", "vbscript:", "data:", "://",
        "<",
    ];

    /// The only at-rules a theme needs. `@-webkit-keyframes` and friends are
    /// not here on purpose — the un-prefixed forms work in WebView2.
    private static readonly HashSet<string> AllowedAtRules = new(StringComparer.Ordinal)
    {
        "media", "supports", "container", "keyframes", "layer",
    };

    private static readonly Regex AtRule = new(@"@([a-z-]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static CssSanitiseResult Sanitise(string? css)
    {
        if (string.IsNullOrWhiteSpace(css)) return new(string.Empty, Array.Empty<string>());

        var rejections = new List<string>();

        if (Encoding.UTF8.GetByteCount(css) > ThemeSchema.MaxCssBytes)
            rejections.Add($"The css block is larger than {ThemeSchema.MaxCssBytes / 1024} KB.");

        foreach (char c in css)
        {
            if (c < 0x20 && c is not ('\t' or '\n' or '\r'))
            {
                rejections.Add($"The css block contains a control character (U+{(int)c:X4}).");
                break;
            }
        }

        if (!TryStripComments(css, out string stripped))
            rejections.Add("The css block has a comment that never closes.");

        if (rejections.Count > 0) return new(string.Empty, rejections);

        // The version the checks look at — and, because it is the version
        // the checks looked at, the version that gets emitted.
        string clean = stripped.Trim();
        string probe = Whitespace.Replace(clean.ToLowerInvariant(), " ");

        if (probe.Contains('\\'))
            rejections.Add("Backslash escapes are not allowed in theme CSS.");

        foreach (string token in ForbiddenTokens)
        {
            if (probe.Contains(token, StringComparison.Ordinal))
                rejections.Add($"'{token.Trim()}' is not allowed in theme CSS.");
        }

        foreach (Match m in AtRule.Matches(probe))
        {
            string name = m.Groups[1].Value;
            if (!AllowedAtRules.Contains(name))
                rejections.Add($"@{name} is not allowed in theme CSS; only @{string.Join(", @", AllowedAtRules)} are.");
        }

        return rejections.Count == 0
            ? new(clean, Array.Empty<string>())
            : new(string.Empty, Dedupe(rejections));
    }

    /// Removes every `/* … */`. A comment that opens and never closes is a
    /// refusal, not a truncation: the browser would treat the rest of the
    /// block as comment, so what the reviewer read and what painted would
    /// differ.
    private static bool TryStripComments(string css, out string result)
    {
        var sb = new StringBuilder(css.Length);
        int i = 0;
        while (i < css.Length)
        {
            if (css[i] == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                int end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0) { result = string.Empty; return false; }
                i = end + 2;
                continue;
            }
            sb.Append(css[i]);
            i++;
        }
        result = sb.ToString();
        return true;
    }

    private static List<string> Dedupe(List<string> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(items.Count);
        foreach (string s in items) if (seen.Add(s)) result.Add(s);
        return result;
    }
}
