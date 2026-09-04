using System.Security.Cryptography;
using MarkdownViewer.Theming;

// ThemeLint — validates theme files the way Marknote will, and prints the
// contrast table a reviewer signs off against.
//
//   ThemeLint <theme.json>... [--app-version 1.9.0] [--allow-reserved] [--json]
//
// Exit code 0 when every file validates and clears the contrast bar; 1
// otherwise. Warnings about unknown keys never fail a run; contrast does.
// Run from the Marknote repo (dotnet run --project tools/ThemeLint -- …) or
// from the Marknote-Themes repo's lint workflow, which checks this repo out.

var files = new List<string>();
string appVersion = "99.0";
bool allowReserved = false;
bool asJson = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--app-version": appVersion = args[++i]; break;
        case "--allow-reserved": allowReserved = true; break;
        case "--json": asJson = true; break;
        case "-h" or "--help":
            Console.WriteLine("usage: ThemeLint <theme.json>... [--app-version X] [--allow-reserved] [--json]");
            return 0;
        default: files.Add(args[i]); break;
    }
}
if (files.Count == 0)
{
    Console.Error.WriteLine("ThemeLint: no files given.");
    return 2;
}

bool allOk = true;
var report = new List<object>();
foreach (string file in files)
{
    string json;
    try { json = File.ReadAllText(file); }
    catch (Exception ex)
    {
        allOk = false;
        Console.Error.WriteLine($"{file}: cannot read — {ex.Message}");
        continue;
    }

    var v = ThemeValidator.Parse(json, appVersion);
    var problems = new List<string>();
    if (!v.IsOk) problems.Add($"{v.Status}: {v.Detail}");
    else if (!allowReserved && ThemeSchema.IsReservedId(v.Theme!.Id))
        problems.Add($"IdReserved: '{v.Theme.Id}' is under {ThemeSchema.ReservedIdPrefix}, which only built-in themes may use. Use your own domain, reversed.");

    var contrast = v.IsOk ? ThemeContrast.Measure(v.Theme!) : Array.Empty<ContrastCheck>();
    foreach (var c in contrast) if (!c.Passes) problems.Add("Contrast: " + c);
    foreach (string w in v.Warnings) if (!w.StartsWith("Below the contrast bar", StringComparison.Ordinal)) problems.Add("Warning: " + w);

    bool failed = !v.IsOk || contrast.Any(c => !c.Passes) || problems.Any(p => p.StartsWith("IdReserved", StringComparison.Ordinal));
    if (failed) allOk = false;

    byte[] bytes = File.ReadAllBytes(file);
    string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    if (asJson)
    {
        report.Add(new
        {
            file,
            ok = !failed,
            id = v.Theme?.Id ?? v.Definition?.Id,
            name = v.Theme?.Name ?? v.Definition?.Name,
            version = v.Theme?.Version,
            appearance = v.Theme?.Appearance,
            sha256 = sha,
            sizeBytes = bytes.Length,
            problems,
            contrast = contrast.Select(c => new { c.Surface, ratio = Math.Round(c.Ratio, 2), c.Minimum, passes = c.Passes }),
        });
        continue;
    }

    Console.WriteLine($"{(failed ? "FAIL" : "ok  ")} {file}");
    if (v.Theme is { } t)
        Console.WriteLine($"     {t.Name} v{t.Version} · {t.Id} · {t.Appearance} · sha256 {sha} · {bytes.Length:N0} bytes");
    foreach (var c in contrast)
        Console.WriteLine($"     {(c.Passes ? " " : "!")} {c.Surface,-58} {c.Ratio,6:0.00}:1  (min {c.Minimum:0.0})");
    foreach (string p in problems)
        Console.WriteLine($"     ! {p}");
}

if (asJson)
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

return allOk ? 0 : 1;
