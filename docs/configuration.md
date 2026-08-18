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
| `UrlProvider` | Which backend generates image URLs — Umbraco or Cloudflare. See [Cloudflare image transformations](#cloudflare-image-transformations). |
| `Cloudflare` | Options for the Cloudflare URL provider. Only read when `UrlProvider` is `Cloudflare`. |
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
| `ImageQuality` | int | 0 | JPEG/WebP quality (1-100). When unset, no quality option is added to generated URLs and the image processor's default applies. |
| `CropMode` | string | `"Crop"` | Umbraco `ImageCropMode`: `Crop`, `Max`, `Stretch`, `Pad`, `BoxPad`, `Min`. Defaults to `Crop` when unset. |
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

## Cloudflare image transformations

By default the package generates image-processor query strings (`?width=400&height=400&quality=70`) that your **origin** resolves. If the site sits behind a Cloudflare zone with transformations enabled, one setting moves that work to the edge:

```json
{
  "DotSee": {
    "ResponsiveImages": {
      "UrlProvider": "Cloudflare"
    }
  }
}
```

Every URL the package emits then becomes a Cloudflare transformation URL:

```
/cdn-cgi/image/width=400,height=400,fit=cover,gravity=0.3x0.8,quality=70,format=auto,onerror=redirect/media/gmgjipfc/friendly-chair.jpg?v=1dcc0464fe1e69e
```

**Nothing else changes.** Rule sets, breakpoints, `<picture>` art direction, `srcset`/`sizes`, the focal point, lazy loading, LQIP, responsive CSS backgrounds, preload hints and caching all behave exactly as documented above. The division of labour is the point: this package decides *how many pixels* to send and writes the markup — neither of which a CDN can do, because only the page knows what the CSS layout box is. Cloudflare produces the pixels and spares your origin the CPU.

| Property | Type | Default | Description |
|---|---|---|---|
| `UrlProvider` | string | `"Umbraco"` | `Umbraco` or `Cloudflare`. |
| `Cloudflare.Prefix` | string | `"/cdn-cgi/image"` | Path prefix Cloudflare listens on. Change only for a Worker route. |
| `Cloudflare.BaseUrl` | string | null | Absolute origin to prefix URLs with, e.g. `https://images.example.com`. Leave unset for relative URLs — correct when the site itself is the zone doing the transforming. |
| `Cloudflare.Format` | string | `"auto"` | Cloudflare `format`. `auto` serves AVIF/WebP by content negotiation. `none` keeps the source format. |
| `Cloudflare.Metadata` | string | null | Cloudflare `metadata`: `none`, `copyright` or `keep`. Unset leaves Cloudflare's own behaviour (keep the copyright tag, drop the rest) and keeps the option out of the URL. |
| `Cloudflare.CacheBuster` | bool | true | Append the media item's cache buster to the source URL, as Umbraco's own crop URLs do. |
| `Cloudflare.OnError` | string | `"redirect"` | Cloudflare `onerror`. `redirect` serves the untransformed original if a transformation fails, so an unsupported source degrades to a working image. |

### Focal point

`UseFocalPoint` (on by default) becomes Cloudflare's `gravity` option, so the crop is still anchored on the point the editor chose in the backoffice. This is the setting worth caring about most: `gravity=auto` and Polish can only guess where the subject of a photo is, whereas the focal point is CMS data.

`gravity` is emitted only when the crop mode actually crops — Cloudflare ignores it otherwise, and a dead option would only fragment the edge cache.

### Crop mode mapping

`CropMode` maps onto Cloudflare's `fit`:

| `CropMode` | Cloudflare `fit` | |
|---|---|---|
| `Crop` | `cover` | exact |
| `Max` | `scale-down` | exact |
| `Pad`, `BoxPad` | `pad` | exact |
| `Min` | `cover` | **approximation** — Cloudflare has no shortest-side-constrained mode |
| `Stretch` | `cover` | **approximation** — Cloudflare has no distort mode |

The two approximations are the only places Cloudflare mode is not a faithful translation of what the origin would have produced. If you rely on `Min` or `Stretch`, check the result before switching.

### WebP

Leave `UseWebP` off and let `format=auto` do it — Cloudflare will serve AVIF where the browser supports it, which WebP does not reach. If `UseWebP` is on it still wins, emitting `format=webp` instead of `format=auto`, so existing configuration keeps meaning what it says.

### Query strings

Options the package owns — `width`, `height`, `mode`, `rxy` — are ignored if you pass them through a tag helper's `query-string`, since the rule set and candidate ladder decide those. `format` and `quality` are translated into their Cloudflare equivalents. Anything else is appended to the *source* URL, where it reaches your origin unchanged.

### Things to know

- **Transformations must be enabled on the zone.** Until they are, `/cdn-cgi/image/` URLs will not resolve. They also do not resolve on `localhost`, so local testing shows you the markup, not the delivered image.
- **SVGs are untouched.** They bypass transformation entirely and render as a plain `<img>`, as they already did.
- **LQIP still costs nothing.** The blur placeholder is built by decoding the media file on the server and inlining it as a `data:` URI — no request, and no billable transformation. Only its fallback, for a file that cannot be decoded, is a URL.
- **This setting needs the current configuration layout.** Under the [earlier layout](#upgrading-from-the-earlier-layout) `DotSee:ResponsiveImages` *is* the rule set array and cannot carry a named key. Nest your rule sets under `RuleSets` first; everything else about the old layout keeps working.
- **Switching provider needs a restart** — it is resolved once at startup, like `DotSee:ImageCdn`.
- **Only URLs this package generates change.** If your own views call Umbraco's `GetCropUrl()` directly — a page-header background, an author thumbnail — those keep producing origin query strings. Move them to a rule set and a tag helper to bring them along.
- **Cloudflare bills per unique transformation.** Every breakpoint × DPI factor × media edit is a variant. Fewer, wider-spaced breakpoints cost less than many closely-spaced ones, and `Use3x` roughly doubles the count for a difference almost nobody can see.

### With CDN purging

If you also use [CDN purging](#cdn-purging), purge URLs are generated by the same provider, so they are in the same format as the URLs your pages actually requested. As documented there, `Files` mode remains best effort — a media-save event cannot know the cache buster or focal point a page used.

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
| `PurgeOnMediaSave` | bool | true | Purge when a media item's **file** changes on save. Renames and caption edits do not purge — the cached edge objects are still valid, and purge requests are rate limited. Still gated by `Enabled`. |
| `PurgeOnMediaDelete` | bool | true | Purge when a media item is deleted — including moving it to the recycle bin, which is what the backoffice delete button actually does. Still gated by `Enabled`. |
| `Mode` | string | `"Files"` | `Files` purges the changed media's URLs; `Everything` purges the whole zone. |
| `MaxUrlsPerPurge` | int | 100 | Upper bound on URLs submitted **per media item**, so a rule set with many breakpoints can't produce an unbounded request. A non-positive value is treated as unlimited, with a warning. |

> **Keep `ApiToken` out of `appsettings.json`.** Use user secrets in development and an environment variable (`DotSee__ImageCdn__ApiToken`) in production.

> **Enabling purging requires a restart.** The purge service is selected once at startup (like [`UrlProvider`](#cloudflare-image-transformations)); flipping `Enabled` to `true` on a running site logs a warning at the next media save instead of purging. Disabling takes effect immediately.

> **External media (blob storage) cannot be purged through the zone.** Purge URLs whose host differs from `BaseUrl` are skipped and logged — Cloudflare rejects a purge batch containing another host's URLs outright, so one such URL would otherwise take every legitimate one down with it.

### Modes

**`Files`** purges the changed media's own URL plus the variant URLs your configured rule sets ask for. Targeted, but **best effort**: the exact URL a page requested also depends on the focal point, the cache buster and any per-call query string, none of which are knowable from a media-save event. It covers the common cases and never touches non-image assets.

**`Everything`** purges the entire zone on every qualifying media change. Reliable, and very blunt — it discards the cache for your whole site, not just images. Only worth it on sites where media changes are rare.

### Do you need this at all?

Often not. Umbraco's crop URLs carry a cache buster derived from the media item's update date, so replacing an image usually produces *new* URLs and the stale ones are simply never requested again. Purging matters when that doesn't hold — most commonly a CDN configured to ignore query strings, or an image replaced in place at the same path.

Failures are logged and never surface to the editor: a CDN being unreachable will not fail a media save.
