# Razor API

The `SrcSetManager` class is the primary API for generating responsive image markup from Razor views. Inject it into your view or controller:

```cshtml
@inject DotSee.ResponsiveImages.SrcSetManager _srcSetManager;
```

## CreatePictureElement

Generates a complete `<picture>` element with `<source>` tags for each breakpoint, including 2x and 3x variants when configured in the rule set.

```csharp
public HtmlString CreatePictureElement(
    MediaWithCrops originalImage,
    string ruleSetName,
    string imageAlt = "",
    string imageClass = "",
    Dictionary<string, string> imageAttributes = null,
    string optionalQueryStringParameters = null,
    bool emitInlineLqip = true)
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
  <source media="only screen and (-webkit-min-device-pixel-ratio: 5/4) and (min-width: 1920px),..." srcset="/media/.../image.jpg?width=3840&quality=80" />
  <source media="only screen and (min-width: 1920px)" srcset="/media/.../image.jpg?width=1920&quality=80" />
  <!-- more sources for each breakpoint -->
  <img class="hero-img" loading="lazy" decoding="async" src="/media/.../image.jpg" alt="Homepage hero" />
</picture>
```

### Caching

Results are cached for 20 minutes (sliding expiration) when `imageAlt`, `imageClass`, and `imageAttributes` are all empty/null. When any of these are set, the output is generated fresh each time.

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
    Dictionary<string, string> otherAttributes = null)
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
<img class="img-fluid" loading="lazy" decoding="async" srcset="/media/.../image.jpg?width=150 576w,/media/.../image.jpg?width=200 992w" sizes="(max-width: 576px) 100vw, 992px" src="/media/.../image.jpg?width=400&height=400&quality=70" alt="Product photo" />
```

### CreateMarkup vs CreatePictureElement

| Feature | `CreateMarkup` | `CreatePictureElement` |
|---|---|---|
| HTML output | Single `<img>` | `<picture>` with multiple `<source>` |
| Breakpoint handling | Browser picks from `srcset` based on `sizes` | Explicit media queries per breakpoint |
| 2x/3x variants | Appended to the srcset list (e.g., `2x`, `3x`) | Separate `<source>` elements with DPI media queries |
| `sizes` attribute | Yes (from config `Sizes` array) | No (media queries on each `<source>` instead) |
| Use when | Simple responsive images with width descriptors | Fine-grained control per breakpoint, art direction |

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
