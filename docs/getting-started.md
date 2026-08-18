# Getting Started

DotSee.ResponsiveImages is a NuGet package for Umbraco CMS that generates responsive image markup. It supports `<picture>` elements, `<img>` with `srcset`/`sizes`, CSS background images, lazy loading, and LQIP (Low-Quality Image Placeholders).

## Installation

Install the NuGet package:

```bash
dotnet add package DotSee.ResponsiveImages
```

The package auto-registers all services via an Umbraco `IComposer`. No manual service registration is needed.

## Register Tag Helpers

Add the following to your `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, DotSee.ResponsiveImages
```

> **Note:** The assembly name is `DotSee.ResponsiveImages`, not the namespace `DotSee.ResponsiveImages.TagHelpers`.

## Enable appsettings IntelliSense

The package ships a JSON schema describing every setting, with descriptions and dropdowns for the enum values. It is copied to your project root on build as `appsettings-schema.DotSee.ResponsiveImages.json`.

To use it, add one `$ref` to the `allOf` array in your project's `appsettings-schema.json` (the file Umbraco's own schema is already wired into):

```jsonc
{
  "$schema": "http://json-schema.org/draft-04/schema#",
  "allOf": [
    { "$ref": "https://json.schemastore.org/appsettings.json" },
    { "$ref": "appsettings-schema.Umbraco.Cms.json#" },
    { "$ref": "appsettings-schema.DotSee.ResponsiveImages.json#" }
  ]
}
```

Your `appsettings.json` should already point at that file:

```jsonc
{
  "$schema": "appsettings-schema.json",
  ...
}
```

That's it — Visual Studio, VS Code and Rider all pick it up with no per-developer setup. You get completion, hover documentation and validation across `DotSee:ResponsiveImages` and `DotSee:ImageCdn`.

## Check Your CSS

The package emits `width` and `height` attributes on every image so the browser can reserve the box before the image loads. That means an image with no CSS sizing it renders at exactly that pixel width. Make sure your stylesheet has:

```css
img {
    max-width: 100%;
    height: auto;
}
```

Most resets and frameworks already include this. See [Your CSS must cap image widths](tag-helpers.md#your-css-must-cap-image-widths).

## Enable Preload Hints (recommended)

Add `<ds:preloads />` inside the `<head>` of your layout:

```cshtml
<head>
    <meta charset="utf-8" />
    <ds:preloads />
    <link rel="stylesheet" href="/css/site.css" />
</head>
```

This emits `<link rel="preload">` hints for any image you mark `above-fold="true"`, letting the browser start fetching your hero before it parses the markup — usually the largest single Largest Contentful Paint improvement available. Everything works without it; you just don't get the hints. See [Preload hints](tag-helpers.md#preload-hints-for-the-lcp-image).

## Minimal Configuration

Add a rule set to your `appsettings.json`:

```json
{
  "DotSee": {
    "ResponsiveImages": {
      "LazyLoad": {
        "EnablelazyLoad": true,
        "PreviewType": "Blur"
      },
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

> Upgrading from an earlier version? The previous layout — `DotSee:ResponsiveImages` as a bare array with a root-level `lazyload` section — still works unchanged. See [Configuration Layout](configuration.md#configuration-layout).

### Behind Cloudflare?

Add `"UrlProvider": "Cloudflare"` and the package emits `/cdn-cgi/image/…` URLs so the resizing happens at the edge instead of on your origin — same rule sets, same markup, same focal point. See [Cloudflare image transformations](configuration.md#cloudflare-image-transformations).

## Quick Usage

### Tag Helper

```cshtml
<ds:picture image="@Model.Image"
            rule-set="default"
            image-alt="@Model.Image.Content.Name" />
```

For the hero image at the top of a page, add `above-fold="true"` so it loads eagerly at high priority instead of being deferred:

```cshtml
<ds:picture image="@Model.HeroImage"
            rule-set="default"
            image-alt="Hero"
            above-fold="true" />
```

### Razor Code

```cshtml
@inject DotSee.ResponsiveImages.SrcSetManager _srcSetManager;

@_srcSetManager.CreatePictureElement(Model.Image, "default", imageAlt: "My image")
```

Both produce a `<picture>` element with `<source>` tags for each breakpoint and a fallback `<img>`.

## What's Next

- [Configuration Reference](configuration.md) - Full `appsettings.json` options
- [Tag Helpers](tag-helpers.md) - `<ds:picture>` attributes and examples
- [Razor API](razor-api.md) - `SrcSetManager` methods for srcset, picture, and CSS backgrounds
- [Lazy Loading](lazy-loading.md) - Native lazy loading with blur or low-res image placeholders
- [CDN Purging](configuration.md#cdn-purging) - Optional, off by default: drop replaced images from the edge
