# Configuration Reference

All configuration lives in `appsettings.json` under `DotSee:ResponsiveImages`.

> **Tip:** everything below is also available as editor IntelliSense — completion, hover docs and enum dropdowns. One line of setup: see [Enable appsettings IntelliSense](getting-started.md#enable-appsettings-intellisense).

## Configuration Layout

`DotSee:ResponsiveImages` is an object holding your rule sets and the global lazy-loading settings:

```json
{
  "DotSee": {
    "ResponsiveImages": {
      "LazyLoad": {
        "EnablelazyLoad": true,
        "PreviewType": "Blur",
        "LowResImagePath": "/img/placeholder-lowres.jpg"
      },
      "UseWebP": false,
      "SuppressTagHelperWarnings": false,
      "RuleSets": [
        { "Name": "default", "...": "..." }
      ]
    }
  }
}
```

| Key | Description |
|---|---|
| `RuleSets` | Array of rule sets. See [Rule Sets](#rule-sets). |
| `LazyLoad` | Global lazy-loading settings. See [Lazy Loading Settings](#lazy-loading-settings). |
| `UseWebP` | Append `&format=webp` to every generated URL. See [WebP Support](#webp-support). |
| `SuppressTagHelperWarnings` | Stop the tag helpers rendering warnings into the page. See [Tag Helper Warnings](#tag-helper-warnings). |

CDN purging lives in a sibling section, `DotSee:ImageCdn` — see [CDN Purging](#cdn-purging). It is kept separate because it configures your CDN rather than image markup.

### Upgrading from the earlier layout

Originally `DotSee:ResponsiveImages` **was** the rule set array, with lazy loading in a `lazyload` section and the WebP switch in a `useWebP` key, both at the root of `appsettings.json`:

```json
{
  "lazyload": { "EnablelazyLoad": true },
  "useWebP": true,
  "DotSee": {
    "ResponsiveImages": [ { "Name": "default" } ]
  }
}
```

**That still works** — nothing is required of existing sites. The package detects which layout you are using from the keys present, and if you configure both, the values under `DotSee:ResponsiveImages` win. Moving to the layout above is a straight nesting operation: wrap the array in `"RuleSets": [ … ]`, and move `lazyload` and `useWebP` in as `LazyLoad` and `UseWebP`.

## Rule Sets

Rule sets are defined as a JSON array under `DotSee:ResponsiveImages:RuleSets`. Each rule set has a name that you reference in your views.

```json
{
  "DotSee": {
    "ResponsiveImages": {
      "RuleSets": [
      {
        "Name": "hero",
        "OriginalImageMaxWidth": 1920,
        "OriginalImageMaxHeight": 1080,
        "ImageQuality": 80,
        "CropMode": "Crop",
        "Use2x": true,
        "Use3x": false,
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
    "ResponsiveImages": {
      "RuleSets": [
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
}
```

## Lazy Loading Settings

Global lazy-loading configuration is the `LazyLoad` section, alongside `RuleSets`:

```json
{
  "DotSee": {
    "ResponsiveImages": {
      "LazyLoad": {
        "EnablelazyLoad": true,
        "PreviewType": "Blur",
        "LowResImagePath": "/img/placeholder-lowres.jpg"
      }
    }
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

To enable automatic WebP conversion:

```json
{
  "DotSee": {
    "ResponsiveImages": {
      "UseWebP": true
    }
  }
}
```

When enabled, `&format=webp` is appended to all generated image URLs. This requires your Umbraco image processor (e.g., ImageSharp) to support WebP output. Defaults to `false`.

> This setting used to be a `useWebP` key at the root of `appsettings.json`. That still works; the nested setting wins if both are present. See [Upgrading from the earlier layout](#upgrading-from-the-earlier-layout).

> If your site sits behind a CDN that already negotiates image formats (Cloudflare Polish, `format=auto`, and similar), leave `UseWebP` off and let the CDN choose. Format negotiation is the one thing such a CDN does better than this package; picking the right *pixel dimensions* for the layout, and the right crop, is what it cannot do for you.

## Tag Helper Warnings

When an image is misconfigured — no alt text, no rule set, or an exception while rendering — the tag helpers render a red message into the page. That is useful while building and unwanted in front of visitors:

```json
{
  "DotSee": {
    "ResponsiveImages": {
      "SuppressTagHelperWarnings": true
    }
  }
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `SuppressTagHelperWarnings` | bool | **false** | When true, the tag helpers stay silent instead of rendering warnings into the page. |

Defaults to `false` so problems are loud where you want them loud. The natural setup is to leave it unset in `appsettings.Development.json` and set it to `true` in `appsettings.Production.json`, which lets the same views move between environments untouched.

Errors are logged either way — this only controls whether the message reaches the page.

> This replaces the per-element `suppress-warnings` attribute, which has been removed. If you have `suppress-warnings="true"` in a view, delete it and set this instead; leaving it in place does nothing and the warnings will reappear.

## CDN Purging

Optional. When an editor replaces an image, a CDN will keep serving the previous file until its TTL expires. This section lets the package drop the affected images from the edge when media changes.

**Everything here is off by default and nothing outbound happens unless you switch it on.** Installing the package never causes a CDN call.

```json
{
  "DotSee": {
    "ImageCdn": {
      "Enabled": true,
      "Provider": "Cloudflare",
      "ZoneId": "your-zone-id",
      "ApiToken": "your-token",
      "BaseUrl": "https://www.example.com",
      "PurgeOnMediaSave": true,
      "PurgeOnMediaDelete": true,
      "Mode": "Files",
      "MaxUrlsPerPurge": 100
    }
  }
}
```

| Property | Type | Default | Description |
|---|---|---|---|
| `Enabled` | bool | **false** | Master switch. While false no CDN call of any kind is made and everything below is ignored. |
| `Provider` | string | `"Cloudflare"` | Only `Cloudflare` is implemented. Any other value falls back to doing nothing. |
| `ZoneId` | string | null | Cloudflare zone id for the zone serving the site. Purging is skipped if missing. |
| `ApiToken` | string | null | Token with the *Zone → Cache Purge* permission. Purging is skipped if missing. |
| `BaseUrl` | string | null | Public origin, e.g. `https://www.example.com`. Required for `Files` mode, which needs absolute URLs. A warning is logged and the purge skipped if missing. |
| `PurgeOnMediaSave` | bool | true | Purge when a media item is saved. Still gated by `Enabled`. |
| `PurgeOnMediaDelete` | bool | true | Purge when a media item is deleted. Still gated by `Enabled`. |
| `Mode` | string | `"Files"` | `Files` purges the changed media's URLs; `Everything` purges the whole zone. |
| `MaxUrlsPerPurge` | int | 100 | Upper bound on URLs submitted per media change, so a rule set with many breakpoints can't produce an unbounded request. |

> **Keep `ApiToken` out of `appsettings.json`.** Use user secrets in development and an environment variable (`DotSee__ImageCdn__ApiToken`) in production.

### Modes

**`Files`** purges the changed media's own URL plus the variant URLs your configured rule sets ask for. Targeted, but **best effort**: the exact URL a page requested also depends on the focal point, the cache buster and any per-call query string, none of which are knowable from a media-save event. It covers the common cases and never touches non-image assets.

**`Everything`** purges the entire zone on every qualifying media change. Reliable, and very blunt — it discards the cache for your whole site, not just images. Only worth it on sites where media changes are rare.

### Do you need this at all?

Often not. Umbraco's crop URLs carry a cache buster derived from the media item's update date, so replacing an image usually produces *new* URLs and the stale ones are simply never requested again. Purging matters when that doesn't hold — most commonly a CDN configured to ignore query strings, or an image replaced in place at the same path.

Failures are logged and never surface to the editor: a CDN being unreachable will not fail a media save.
