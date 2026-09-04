## Theme

- **Id:**
- **Name:**
- **Light or dark:**
- **Based on a published palette?** (name + link, and the licence it allows)

## Checklist

- [ ] The file is at `themes/<id>/theme.json` and the folder is named for the id.
- [ ] The id is reverse-DNS under a domain I own — not `uk.marknote.*` or `md.marknote.*`.
- [ ] The name is sentence case and 24 characters or fewer.
- [ ] `appearance` matches the page background (dark themes have a dark `preview.bg`).
- [ ] The lint passes locally: `dotnet run --project tools/ThemeLint -- themes/<id>/theme.json` — paste its contrast table below.
- [ ] Any `css` is small and does only what a stylesheet needs to — no fetching, nothing that runs.
- [ ] `license` is set and `homepage` credits the palette if it is someone else's.

## Contrast table

```
(paste the lint output here)
```
