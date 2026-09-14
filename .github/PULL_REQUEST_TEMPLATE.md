## Theme

- **Id:** (both, for a light and dark pack)
- **Name:**
- **Light, dark, or a pack of both:**
- **Pack name**, if both: (the design's name as it should read on the card, e.g. "Ember")
- **Based on a published palette?** (name + link, and the licence it allows)
- **Your name as it should be credited:** (your GitHub account is shown beside it)

## Checklist

- [ ] The file is at `themes/<id>/theme.json` and the folder is named for the id.
- [ ] For a pack: both files are in this pull request, one `light` and one `dark`, ids differing only by side.
- [ ] The id is reverse-DNS under a domain I own — not `uk.marknote.*` or `md.marknote.*`.
- [ ] The name is sentence case and 24 characters or fewer.
- [ ] `appearance` matches the page background (dark themes have a dark `preview.bg`).
- [ ] The lint passes locally: `dotnet run --project tools/lint -- themes/<id>/theme.json` — paste its contrast table below.
- [ ] Any `css` is small and does only what a stylesheet needs to — no fetching, nothing that runs.
- [ ] `license` is set and `homepage` credits the palette if it is someone else's.

## Contrast table

```
(paste the lint output here)
```
