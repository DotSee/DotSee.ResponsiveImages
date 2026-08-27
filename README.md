# DotSee.ResponsiveImages

Responsive images for **Umbraco CMS 17**, driven entirely by configuration:

- `<picture>` elements with per-breakpoint art direction (`<ds:picture>`)
- `<img>` with `srcset`/`sizes` (`<ds:img>`)
- Responsive CSS background images with focal-point-aware `background-position` (`<ds:background>`)
- Native lazy loading (`loading="lazy"`) with inline LQIP blur placeholders — **no runtime JavaScript**
- CSP-safe rendering via a `nonce` attribute
- `<link rel="preload">` hints for the LCP image (`<ds:preloads>`)
- Crops anchored on the **focal point the editor set in the backoffice**
- Optional [Cloudflare image transformation](https://github.com/DotSee/DotSee.ResponsiveImages/blob/master/docs/configuration.md#cloudflare-image-transformations) URLs (`/cdn-cgi/image/…`) so resizing happens at the edge instead of on your origin
- Optional CDN cache purging when media changes
- Rendered markup and resolved rule sets are cached, and invalidated automatically when content or media changes — including through the cache refreshers that reach every node in a load-balanced setup
- A JSON schema for full `appsettings.json` IntelliSense, and auto-registration through an Umbraco composer

## Quick start

```bash
dotnet add package DotSee.ResponsiveImages
```

Add to `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, DotSee.ResponsiveImages
```

Define a rule set in `appsettings.json`:

```json
{
  "DotSee": {
    "ResponsiveImages": {
      "RuleSets": [
        {
          "Name": "default",
          "ImageQuality": 80,
          "OriginalImageMaxWidth": 1920,
          "CropMode": "Crop",
          "Breakpoints": [
            { "BreakPointWidth": 1920, "Width": 1200 },
            { "BreakPointWidth": 1200, "Width": 900 },
            { "BreakPointWidth": 768, "Width": 600 },
            { "BreakPointWidth": 576, "Width": 400 }
          ]
        }
      ]
    }
  }
}
```

Use it in a view:

```cshtml
<ds:picture image="@Model.Image" rule-set="default" image-alt="@Model.Image.Content.Name" />
```

## Using with a CDN (Cloudflare and similar)

The package and an image CDN solve different halves of the problem: the CDN optimises *bytes per
pixel*, the package decides *how many pixels to send* and writes the markup — and it knows the
editor's focal point, which no CDN can guess. Keep format negotiation (Polish, `format=auto`) on,
leave `UseWebP` off, and optionally set `"UrlProvider": "Cloudflare"` to move the resizing itself
to the edge.

## Documentation

- [Getting started](https://github.com/DotSee/DotSee.ResponsiveImages/blob/master/docs/getting-started.md)
- [Configuration reference](https://github.com/DotSee/DotSee.ResponsiveImages/blob/master/docs/configuration.md)
- [Tag helpers](https://github.com/DotSee/DotSee.ResponsiveImages/blob/master/docs/tag-helpers.md)
- [Razor API](https://github.com/DotSee/DotSee.ResponsiveImages/blob/master/docs/razor-api.md)
- [Lazy loading & LQIP](https://github.com/DotSee/DotSee.ResponsiveImages/blob/master/docs/lazy-loading.md)

## License

[MIT](https://github.com/DotSee/DotSee.ResponsiveImages/blob/master/LICENSE). Note: the package references [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp)
(Six Labors Split License) for building inline LQIP placeholders — review Six Labors' licensing terms
for your own use of ImageSharp.
