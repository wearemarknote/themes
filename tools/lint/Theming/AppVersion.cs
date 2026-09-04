using System;

namespace MarkdownViewer.Theming;

/// Version comparison for `minMarknoteVersion` checks, shared by themes and
/// plugins so the two can never disagree about what "newer" means.
public static class AppVersion
{
    /// Negative if <paramref name="a"/> is older than <paramref name="b"/>,
    /// zero if equal, positive if newer. Missing components are treated as
    /// zero so "1.4" compares as "1.4.0.0" against the four-component
    /// version the app reports. A leading "v" is tolerated.
    public static int Compare(string a, string b)
    {
        return Parse(a).CompareTo(Parse(b));

        static Version Parse(string s)
        {
            s = s.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s[1..];
            return Version.TryParse(s, out var v) ? v : new Version(0, 0);
        }
    }
}
