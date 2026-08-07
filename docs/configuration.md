# Configuration Reference

All configuration lives in `appsettings.json` under two sections: `DotSee:ResponsiveImages` for rule sets, and `lazyload` for global lazy-loading settings.

## Rule Sets

Rule sets are defined as a JSON array under `DotSee:ResponsiveImages`. Each rule set has a name that you reference in your views.

```json
{
  "DotSee": {
    "ResponsiveImages": [
      {
        "Name": "hero",
        "OriginalImageMaxWidth": 1920,
        "OriginalImageMaxHeight": 1080,
        "ImageQuality": 80,
        "CropMode": "Crop",
        "Use2x": true,
        "Use3x": false,
        "Upscale": false,
        "UseBreakPointWidthIfNoWidth": true,
        "LazyLoad": null,
        "Sizes": [
          "(max-width: 576px) 100vw",
          "(max-width: 992px) 50vw",
          "33vw"
        ],
        "Breakpoints": [
          { "BreakPointWidth": 1920, "Width": 1920, "Height": 800 },
          { "BreakPointWidth": 1200, "Width": 1200, "Height": 600 },
          { "BreakPointWidth": 992, "Width": 992, "Height": 500 },
          { "BreakPointWidth": 768, "Width": 768, "Height": 0 },
          { "BreakPointWidth": 576, "Width": 576, "Height": 0 }
        ]
      }
    ]
  }
}
```

### Rule Set Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | string | *required* | Unique identifier. Referenced in views as `rule-set="hero"`. |
| `ImageQuality` | int | 0 | JPEG/WebP quality (1-100). |
| `CropMode` | string | `"Min"` | Umbraco `ImageCropMode`: `Crop`, `Max`, `Stretch`, `Pad`, `BoxPad`, `Min`. |
| `OriginalImageMaxWidth` | int? | null | Maximum width constraint. Images won't be generated wider than this. |
| `OriginalImageMaxHeight` | int? | null | Maximum height constraint. Used for proportional calculations. |
| `Use2x` | bool | false | Generate a 2x (retina) variant. In `<picture>` it is an extra `2x` candidate on the same `<source>`; in `<img srcset>` it is an extra, wider `w` candidate. |
| `Use3x` | bool | false | As `Use2x`, at 3x. |
| `UseFocalPoint` | bool | **true** | Anchor generated crops on the focal point the editor set in the backoffice instead of the image centre. Set to `false` for plain centre cropping. |
| `Upscale` | bool | false | Allow upscaling beyond the original image dimensions. |
| `UseBreakPointWidthIfNoWidth` | bool | false | If a breakpoint's `Width` is 0, use `BreakPointWidth` as the image width. |
| `LazyLoad` | bool? | null | Override the global lazy-load setting for this rule set. `null` inherits the global value. |
| `Sizes` | string[] | [] | CSS `sizes` attribute entries for `CreateMarkup()`. |
| `Breakpoints` | array | [] | List of breakpoint definitions (see below). |

### Breakpoint Properties

| Property | Type | Description |
|---|---|---|
| `BreakPointWidth` | int | Viewport width threshold in pixels (used in `min-width` media queries). |
| `Width` | int | Image width to generate at this breakpoint. If 0, falls back to `BreakPointWidth` when `UseBreakPointWidthIfNoWidth` is true. |
| `Height` | int | Image height to generate. Set to 0 for width-only resizing (no height constraint). |

## Multiple Rule Sets

You can define multiple rule sets for different use cases:

```json
{
  "DotSee": {
    "ResponsiveImages": [
      {
        "Name": "hero",
        "ImageQuality": 85,
        "OriginalImageMaxWidth": 1920,
        "CropMode": "Crop",
        "Use2x": true,
        "Breakpoints": [
          { "BreakPointWidth": 1920, "Width": 1920, "Height": 600 },
          { "BreakPointWidth": 1200, "Width": 1200, "Height": 450 },
          { "BreakPointWidth": 768, "Width": 768, "Height": 400 }
        ]
      },
      {
        "Name": "thumbnail",
        "ImageQuality": 70,
        "OriginalImageMaxWidth": 400,
        "OriginalImageMaxHeight": 400,
        "CropMode": "Crop",
        "LazyLoad": true,
        "Breakpoints": [
          { "BreakPointWidth": 992, "Width": 200, "Height": 200 },
          { "BreakPointWidth": 576, "Width": 150, "Height": 150 }
        ]
      },
      {
        "Name": "fullwidth",
        "ImageQuality": 80,
        "OriginalImageMaxWidth": 2560,
        "CropMode": "Max",
        "Use2x": true,
        "UseBreakPointWidthIfNoWidth": true,
        "Sizes": [
          "(max-width: 576px) 100vw",
          "(max-width: 992px) 100vw",
          "100vw"
        ],
        "Breakpoints": [
          { "BreakPointWidth": 2560, "Width": 2560 },
          { "BreakPointWidth": 1920, "Width": 1920 },
          { "BreakPointWidth": 1200, "Width": 1200 },
          { "BreakPointWidth": 768, "Width": 768 },
          { "BreakPointWidth": 576, "Width": 576 }
        ]
      }
    ]
  }
}
```

## Lazy Loading Settings

Global lazy-loading configuration is in the `lazyload` section (at the root level, not inside `DotSee`):

```json
{
  "lazyload": {
    "EnablelazyLoad": true,
    "PreviewType": "Blur",
    "LowResImagePath": "/img/placeholder-lowres.jpg"
  }
}
```

| Property | Type | Values | Description |
|---|---|---|---|
| `EnablelazyLoad` | bool? | true/false/null | Enable native `loading="lazy"` globally. |
| `PreviewType` | string | `"Blur"`, `"LowResImage"` | LQIP preview strategy (see [Lazy Loading](lazy-loading.md)). |
| `LowResImagePath` | string | URL path | Path to a generic low-resolution placeholder image. Only used when `PreviewType` is `"LowResImage"`. |

### Lazy Load Override Behavior

The per-rule-set `LazyLoad` property overrides the global setting:

| Global `EnablelazyLoad` | Rule Set `LazyLoad` | Result |
|---|---|---|
| true | null | Lazy loading **enabled** |
| true | false | Lazy loading **disabled** |
| false | null | Lazy loading **disabled** |
| false | true | Lazy loading **enabled** |
| null | any | Lazy loading **disabled** |

## WebP Support

To enable automatic WebP conversion, add this to your root configuration:

```json
{
  "useWebP": true
}
```

When enabled, `&format=webp` is appended to all generated image URLs. This requires your Umbraco image processor (e.g., ImageSharp) to support WebP output.

> If your site sits behind a CDN that already negotiates image formats (Cloudflare Polish, `format=auto`, and similar), leave `useWebP` off and let the CDN choose. Format negotiation is the one thing such a CDN does better than this package; picking the right *pixel dimensions* for the layout, and the right crop, is what it cannot do for you.
