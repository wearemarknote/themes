# Marknote themes

Community themes for [Marknote](https://marknote.md), the markdown editor for Windows. Every theme here is one JSON file and no code: it names the colours for every surface the app paints, and the app validates every key before it uses any of them.

Themes merged here are listed on [marknote.md/themes](https://marknote.md/themes) and appear in the app's gallery (**Settings → Themes → Get more themes**).

## Submitting a theme

1. Fork this repo and add `themes/<your-id>/theme.json`. The id is reverse-DNS under a domain you own (`com.example.ember`); `uk.marknote.*` and `md.marknote.*` are Marknote's.
2. Open a pull request. The template is the review checklist, and a workflow lints your file exactly as the app will.
3. A maintainer reads the file, checks the contrast table, and takes the listing screenshot from `samples/preview.md` so every theme is shot the same way.

The format, the contrast bar and the CSS rules are documented at [marknote.md/themes/build](https://marknote.md/themes/build). To lint locally (tools/lint is a copy of the app's own validator):

```
dotnet run --project tools/lint -- themes/<your-id>/theme.json
```

## What is refused

- Anything under a Marknote namespace, or an id that is not reverse-DNS.
- A surface under the contrast bar: text below 4.5:1, links or headings below 3:1, line numbers below 2.5:1.
- CSS that fetches, runs or escapes: `url()`, `@import`, `@font-face`, backslashes, angle brackets, at-rules other than `@media`, `@supports`, `@container`, `@keyframes` and `@layer`.
- A palette that belongs to another project without credit in `homepage` and a licence it allows.

## Licence

Each theme carries its own `license` field and stays its author's. The repo's tooling is MIT.
