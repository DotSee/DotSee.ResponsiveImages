# Razor API

The `SrcSetManager` class is the primary API for generating responsive image markup from Razor views. Inject it into your view or controller:

```cshtml
@inject DotSee.ResponsiveImages.SrcSetManager _srcSetManager;
```

## CreatePictureElement

Generates a complete `<picture>` element with one `<source>` per breakpoint. Configured 2x/3x variants are added as extra `2x`/`3x` candidates on that same `<source>`.

```csharp
public HtmlString CreatePictureElement(
    MediaWithCrops originalImage,
    string ruleSetName,
    string imageAlt = "",
    string imageClass = "",
    Dictionary<string, string> imageAttributes = null,
    string optionalQueryStringParameters = null,
    bool emitInlineLqip = true,
    bool aboveFold = false)
```

### Parameters

| Parameter | Type | Description |
|---|---|---|
| `originalImage` | `MediaWithCrops` | The Umbraco media item. |
| `ruleSetName` | string | Name of the rule set from config. |
| `imageAlt` | string | Alt text for the fallback `<img>`. |
| `imageClass` | string | CSS class for the fallback `<img>`. |
| `imageAttributes` | `Dictionary<string, string>` | Additional HTML attributes for the `<img>`. |
| `optionalQueryStringParameters` | string | Extra query string parameters appended to all image URLs (e.g., `"format=webp"`). |
| `emitInlineLqip` | bool | When `true` (default), LQIP preview is rendered as inline `style`/`onload` attributes. Set to `false` for CSP-safe usage where you handle LQIP externally. See [Lazy Loading - CSP](lazy-loading.md#content-security-policy-csp). |
| `aboveFold` | bool | Set for an image visible without scrolling (typically the LCP element). Loads eagerly with `fetchpriority="high"` and no placeholder, overriding lazy loading for this call only. |

### Example

```cshtml
@_srcSetManager.CreatePictureElement(
    Model.HeroImage,
    "hero",
    imageAlt: "Homepage hero",
    imageClass: "hero-img")
```

### Output

```html
<picture>
  <source media="only screen and (min-width: 1920px)" srcset="/media/.../image.jpg?width=1920&quality=80, /media/.../image.jpg?width=3840&quality=80 2x" width="1920" height="800" />
  <!-- more sources for each breakpoint -->
  <img class="hero-img" loading="lazy" decoding="async" src="/media/.../image.jpg?width=1920&quality=80" width="1920" height="1080" alt="Homepage hero" />
</picture>
```

> **Note:** The fallback `<img>` `src` is generated with the rule set's `OriginalImageMaxWidth`/`OriginalImageMaxHeight`, `ImageQuality`, and `CropMode`, and includes any `optionalQueryStringParameters` (and `format=webp` when `useWebP` is enabled). It is not the raw original media URL.

> `width`/`height` are emitted on both the `<source>` and the fallback `<img>` so the browser reserves the correct box before the image arrives. Supply `width`/`height` in `imageAttributes` to take over.

### Caching

Results are cached for 20 minutes (sliding expiration). The cache key includes the image ID, rule set name, alt text, image class, extra attributes, query string parameters, and the `aboveFold` flag -- so different combinations of these values are cached independently.

In CSP mode (`emitInlineLqip` is `false`), the per-call `data-ds-id` is **not** part of the cache key: the markup is cached without it and the unique id is injected into the fallback `<img>` afterwards. Caching therefore stays effective under CSP.

---

## CreateMarkup

Generates a single `<img>` element with `srcset` and `sizes` attributes. Use this when you don't need `<picture>` element with per-breakpoint media queries.

```csharp
public HtmlString CreateMarkup(
    MediaWithCrops originalImage,
    string ruleSetName,
    string alt = "",
    string title = "",
    string srcSetAttrName = "srcset",
    string imageClass = "",
    Dictionary<string, string> otherAttributes = null,
    bool emitInlineLqip = true,
    bool aboveFold = false)
```

### Parameters

| Parameter | Type | Description |
|---|---|---|
| `originalImage` | `MediaWithCrops` | The Umbraco media item. |
| `ruleSetName` | string | Name of the rule set from config. |
| `alt` | string | Alt text. |
| `title` | string | Title attribute. |
| `srcSetAttrName` | string | Name of the srcset attribute. Defaults to `"srcset"`. |
| `imageClass` | string | CSS class for the `<img>`. |
| `otherAttributes` | `Dictionary<string, string>` | Additional HTML attributes. |
| `emitInlineLqip` | bool | As for `CreatePictureElement`. |
| `aboveFold` | bool | As for `CreatePictureElement`. |

### Example

```cshtml
@_srcSetManager.CreateMarkup(
    Model.Image,
    "thumbnail",
    alt: "Product photo",
    imageClass: "img-fluid")
```

### Output

```html
<img class="img-fluid" loading="lazy" decoding="async" srcset="/media/.../image.jpg?width=150 150w,/media/.../image.jpg?width=200 200w" sizes="(max-width: 576px) 100vw, 992px" src="/media/.../image.jpg?width=400&height=400&quality=70" width="400" height="400" alt="Product photo" />
```

> Every candidate is described by its **own pixel width** (`w`). A `srcset` may not mix `w` and `x`
> descriptors — the HTML spec makes the whole attribute invalid and browsers discard it, falling back
> to `src` — so `Use2x`/`Use3x` add wider `w` candidates rather than `2x`/`3x` ones. The browser
> combines `sizes` with the device pixel ratio and picks the right candidate by itself.

### CreateMarkup vs CreatePictureElement

| Feature | `CreateMarkup` | `CreatePictureElement` |
|---|---|---|
| HTML output | Single `<img>` | `<picture>` with multiple `<source>` |
| Breakpoint handling | Browser picks from `srcset` based on `sizes` | Explicit media queries per breakpoint |
| 2x/3x variants | Extra wider `w` candidates in the same srcset | Extra `2x`/`3x` candidates on the same `<source>` |
| `sizes` attribute | Yes (from config `Sizes` array) | No (media queries on each `<source>` instead) |
| Use when | Simple responsive images with width descriptors | Fine-grained control per breakpoint, art direction |

---

## GetPicturePreloadLinks / GetImagePreloadLink

Build the `<link rel="preload" as="image">` hints for an above-the-fold image. The tag helpers call these automatically when you set `above-fold="true"`; use them directly only if you are building markup yourself.

```csharp
public HtmlString GetPicturePreloadLinks(
    MediaWithCrops originalImage,
    string ruleSetName,
    string optionalQueryStringParameters = null)

public HtmlString GetImagePreloadLink(
    MediaWithCrops originalImage,
    string ruleSetName)
```

`GetPicturePreloadLinks` returns one `<link>` per breakpoint, each carrying the same `media` and `imagesrcset` as the corresponding `<source>`, so the browser preloads exactly the image the `<picture>` will use. `GetImagePreloadLink` returns a single `<link>` with `imagesrcset`/`imagesizes` matching what `CreateMarkup` renders. Both return `null` for a null image, and sources with nothing to fetch are skipped.

The output belongs in `<head>`. If you use the tag helpers, `<ds:preloads />` handles that for you — see [Preload hints](tag-helpers.md#preload-hints-for-the-lcp-image).

---

## GetBreakPointsCss

Generates a `<style>` tag with CSS `background-image` rules using media queries. Use this for responsive CSS background images.

```csharp
public HtmlString GetBreakPointsCss(
    MediaWithCrops originalImage,
    string ruleSetName,
    string optionalQueryStringParameters = null,
    IHtmlContent nonceAttribute = null)
```

### Parameters

| Parameter | Type | Description |
|---|---|---|
| `originalImage` | `MediaWithCrops` | The Umbraco media item. |
| `ruleSetName` | string | Name of the rule set from config. |
| `optionalQueryStringParameters` | string | Extra query parameters for image URLs. |
| `nonceAttribute` | `IHtmlContent` | CSP nonce attribute for the `<style>` tag (e.g., `nonce="abc123"`). |

### Example

```cshtml
@{
    var className = _srcSetManager.GetClassName(Model.BackgroundImage, "hero");
}

@_srcSetManager.GetBreakPointsCss(Model.BackgroundImage, "hero")

<div class="@className">
    <!-- Content over background image -->
</div>
```

### Output

The method generates a `<style>` block containing media queries with `background-image` rules for each breakpoint, including 2x and 3x variants. It respects the image's focal point and converts it to a `background-position` value.

### Content Security Policy (CSP)

If your site uses a Content Security Policy, pass a nonce attribute:

```cshtml
@_srcSetManager.GetBreakPointsCss(Model.Image, "hero", nonceAttribute: Html.Raw("nonce=\"abc123\""))
```

---

## GetClassName

Returns the CSS class name generated for a background image. Use this together with `GetBreakPointsCss` to link the CSS rules to your HTML element.

```csharp
public string GetClassName(IPublishedContent originalImage, string ruleSetName)
```

Returns a string like `media-image-RSFor_hero_a1b2c3d4`.

---

## GetSrcSet

Returns just the `srcset` attribute value as an `HtmlString`. Useful when building custom markup.

```csharp
public HtmlString GetSrcSet(MediaWithCrops originalImage, string ruleSetName)
```

### Example

```cshtml
<img srcset="@_srcSetManager.GetSrcSet(Model.Image, "default")"
     sizes="(max-width: 768px) 100vw, 50vw"
     src="@Model.Image.Url()"
     alt="Custom markup" />
```

---

## GetSizes

Returns the `sizes` attribute value as an `HtmlString`, built from the rule set's `Sizes` configuration.

```csharp
public HtmlString GetSizes(MediaWithCrops originalImage, RuleSet ruleSet)
```

> **Note:** This method takes a `RuleSet` object, not a rule set name string. You'll need to resolve the rule set yourself or use `CreateMarkup` which handles this internally.

---

## Null Safety

All methods return `null` if `originalImage` is null. Always check for null if the image is optional:

```cshtml
@if (Model.Image != null)
{
    @_srcSetManager.CreatePictureElement(Model.Image, "default", imageAlt: "My image")
}
```
