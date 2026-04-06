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

## Minimal Configuration

Add a rule set to your `appsettings.json`:

```json
{
  "DotSee": {
    "ResponsiveImages": [
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
```

## Quick Usage

### Tag Helper

```cshtml
<ds:picture image="@Model.Image"
            rule-set="default"
            image-alt="@Model.Image.Content.Name" />
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
