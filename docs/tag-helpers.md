# Tag Helpers

## Setup

Register the tag helpers in your `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, DotSee.ResponsiveImages
```

## ds:picture

Renders a `<picture>` element with one responsive `<source>` per breakpoint. When `Use2x`/`Use3x` are configured, the higher-DPI images are extra `2x`/`3x` candidates on that same `<source>` — the browser matches them against the device pixel ratio itself, so no DPI media queries are needed.

### Attributes

| Attribute | Type | Required | Description |
|---|---|---|---|
| `image` | `MediaWithCrops` | Yes | The Umbraco media item. Must be a `MediaWithCrops` object, not a URL string. |
| `rule-set` | string | Yes | Name of the rule set from `appsettings.json`. |
| `image-alt` | string | Yes | Alt text for the image. A warning is shown if omitted (unless suppressed). |
| `wrapper-element` | string | No | HTML element to wrap the `<picture>` in (e.g., `div`, `figure`). |
| `wrapper-class` | string | No | CSS class applied to the wrapper element. |
| `image-class` | string | No | CSS class applied to the `<img>` inside the picture element. |
| `image-attributes` / `attr-*` | dictionary | No | Extra attributes for the `<img>`. Supply individually as `attr-fetchpriority="high"`, `attr-id="hero"`, etc. |
| `query-string` | string | No | Extra query string parameters appended to every generated image URL (e.g. `bgcolor=fff`). For WebP across the whole site, prefer the `UseWebP` setting — see [Configuration](configuration.md#webp-support). |
| `nonce` | string | No | CSP nonce. When set, the LQIP preview is rendered as nonce-tagged `<style>`/`<script>` blocks instead of inline `style`/`onload` (CSP-safe). See [CSP](#content-security-policy-csp). |
| `above-fold` | bool | No | Marks an image that is visible without scrolling. See [Above-the-fold images](#above-the-fold-images). |
| `preload` | bool | No | Set to `false` to suppress the preload hint an `above-fold` image otherwise registers. Defaults to `true`; no effect unless `above-fold` is set. See [Preload hints](#preload-hints-for-the-lcp-image). |
| `suppress-warnings` | bool | No | Set to `true` to hide validation error messages from the rendered output. Defaults to `false`. |

Both `<ds:picture>` and `<ds:img>` accept `above-fold`, `preload`, `nonce` and `attr-*`.

### Above-the-fold images

Set `above-fold="true"` on the hero — usually the page's Largest Contentful Paint element. The image is then loaded eagerly at high priority and skips the placeholder, instead of being deferred like images further down the page:

```cshtml
<ds:picture image="@Model.HeroImage"
            rule-set="hero"
            image-alt="Hero banner"
            above-fold="true" />
```

```html
<img loading="eager" fetchpriority="high" src="..." width="1920" height="1080" alt="Hero banner" />
```

This is per usage rather than per rule set, so the same rule set can serve a lazy gallery image and an eager hero. An explicit `attr-fetchpriority` always wins over the automatic `high`.

---

## Preload hints for the LCP image

An `above-fold` image also registers a `<link rel="preload" as="image">` hint. The hint lets the browser start fetching the hero **before it has parsed the markup that contains it**, which is exactly the delay the Largest Contentful Paint measurement captures.

For that to help, the hint has to be in `<head>` — a `<link>` sitting next to its own image is discovered no earlier than the image itself. So the hints are collected during rendering and emitted by a separate tag helper.

### Setup (one line, once)

Add `<ds:preloads />` inside the `<head>` of your layout:

```cshtml
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8" />
    <title>@ViewData["Title"]</title>

    <ds:preloads />

    <link rel="stylesheet" href="/css/site.css" />
</head>
<body>
    @RenderBody()
</body>
</html>
```

Then mark your hero as usual — nothing else to do:

```cshtml
<ds:picture image="@Model.HeroImage"
            rule-set="hero"
            image-alt="Hero banner"
            above-fold="true" />
```

### Rendered output

For `<ds:picture>`, one hint per breakpoint, each carrying the same media query and srcset as the corresponding `<source>`, so the browser evaluates them identically and preloads exactly the image it is about to use:

```html
<head>
  <link rel="preload" as="image" imagesrcset="/media/hero.jpg?width=1920 1x, /media/hero.jpg?width=3840 2x" media="only screen and (min-width: 1200px)" fetchpriority="high" />
  <link rel="preload" as="image" imagesrcset="/media/hero.jpg?width=768" media="only screen and (min-width: 768px)" fetchpriority="high" />
</head>
```

For `<ds:img>`, a single hint carrying the `srcset` and `sizes` the `<img>` will have:

```html
<link rel="preload" as="image" imagesrcset="/media/hero.jpg?width=576 576w, /media/hero.jpg?width=1200 1200w" imagesizes="(max-width: 576px) 100vw, 50vw, 1200px" fetchpriority="high" />
```

### Why it works, and the one limitation

Razor executes a view **before** its layout, so by the time the layout writes `<head>`, the body has already registered its hints. The consequence: images rendered by the *layout itself*, above the `<ds:preloads />` tag, are not included — there is nothing collected yet at that point. Images in the view, in partials, and in block lists all work.

### When not to use it

Preload only the image that is actually the LCP element, usually one per page. Preloading below-the-fold images competes for bandwidth with the one that matters and makes LCP worse. If you want an image eager but not preloaded, use `above-fold="true" preload="false"`.

If you don't add `<ds:preloads />` to your layout, nothing breaks — `above-fold` still applies `loading="eager"` and `fetchpriority="high"`, you just don't get the head hint.

### Basic Usage

```cshtml
<ds:picture image="@Model.Image"
            rule-set="default"
            image-alt="A descriptive alt text" />
```

### With Wrapper Element

Wraps the output in a `<figure>` tag with a CSS class:

```cshtml
<ds:picture image="@Model.Image"
            rule-set="hero"
            image-alt="Hero banner"
            wrapper-element="figure"
            wrapper-class="hero-image" />
```

Renders:

```html
<figure class="hero-image">
  <picture>
    <source media="..." srcset="..." />
    <!-- more sources -->
    <img src="..." alt="Hero banner" />
  </picture>
</figure>
```

### With Image Class

```cshtml
<ds:picture image="@Model.Image"
            rule-set="thumbnail"
            image-alt="Product photo"
            image-class="img-fluid rounded" />
```

### Block List Example

A typical usage inside a block list component:

```cshtml
@inherits UmbracoViewPage<BlockListItem>
@using Umbraco.Cms.Core.Models.Blocks

@{
    var row = Model.Content as ImageRow;
}

<ds:picture image="@row.Image"
            rule-set="default"
            image-alt="@row.Image.Content.Name"
            wrapper-element="div"
            wrapper-class="img-container" />
```

### Suppressing Warnings

By default, the tag helper renders visible error messages when required attributes are missing. In production, suppress these:

```cshtml
<ds:picture image="@Model.Image"
            rule-set="default"
            image-alt="@Model.AltText"
            suppress-warnings="true" />
```

### Error Handling

| Condition | Behavior |
|---|---|
| `image` is null | Output is suppressed entirely (nothing rendered). An error is logged. |
| `image-alt` is empty | A warning div is rendered (unless `suppress-warnings="true"`). |
| `rule-set` is empty | A warning div is rendered (unless `suppress-warnings="true"`). |
| Exception during rendering | A warning div is rendered with the error message (unless `suppress-warnings="true"`). |

### Important Notes

- The `image` attribute must receive a `MediaWithCrops` object, **not** a URL string. Use `@Model.Image` (the property value), not `@Model.Image.Url()`.
- The tag helper renders nothing if the image is null, which is safe for optional image properties.
- SVG images are handled separately and rendered as a plain `<img>` tag (no `<picture>` element).
- `width` and `height` attributes are emitted automatically so the browser can reserve the box before the image arrives (no layout shift). They describe the **largest image the markup can actually deliver** — the widest srcset candidate, not the rule set's `OriginalImageMaxWidth` ceiling — using the candidate's own height when the rule set fixes one, so the declared aspect ratio matches the delivered crop. Supplying `attr-width`/`attr-height` yourself disables this. **See [Your CSS must cap image widths](#your-css-must-cap-image-widths) below.**
- Crops honour the focal point the editor set in the backoffice unless the rule set sets `UseFocalPoint: false`.
- When lazy loading with LQIP is enabled, `<ds:picture>` uses inline `style` and `onload` attributes **unless** you supply a `nonce` — see [CSP](#content-security-policy-csp).

---

## Your CSS must cap image widths

Because the package emits `width` and `height` attributes, an image with **no CSS sizing it will lay out at exactly that pixel width**. That is what those attributes mean in HTML: they set the default rendered size, not just the aspect ratio. On a page with no rule capping images, a 1200px-wide image will occupy 1200 CSS px and can overflow its container.

Nearly every CSS framework and reset already handles this, but confirm your site has the equivalent of:

```css
img {
    max-width: 100%;
    height: auto;
}
```

`height: auto` matters as much as `max-width`: without it, a capped width combined with the fixed `height` attribute distorts the image.

If you use Bootstrap, its `.img-fluid` class does exactly this — but it is opt-in per element, so either add it via `image-class` on every image or add the global rule above. A site relying solely on `.img-fluid` will render any image you forget to class at full declared width.

---

## Content Security Policy (CSP)

By default the LQIP preview is applied with inline `style` and `onload` attributes, which a strict Content Security Policy (no `'unsafe-inline'`) will block. Supply a **`nonce`** attribute and `<ds:picture>` instead emits nonce-tagged `<style>` and `<script>` blocks linked to the image via a generated `data-ds-id`:

```cshtml
<ds:picture image="@Model.Image"
            rule-set="default"
            image-alt="A descriptive alt text"
            nonce="@ViewData["CspNonce"]" />
```

### Rendered Output (with Blur preview)

```html
<style nonce="abc123">
  [data-ds-id="ds-1a2b3c4d"]{background-size:cover;background-repeat:no-repeat;background-image:url('/media/.../image.jpg?width=40&quality=20');filter:blur(20px);transition:filter 0.3s}
</style>
<picture>
  <source media="..." srcset="..." />
  <!-- more sources -->
  <img data-ds-id="ds-1a2b3c4d" loading="lazy" decoding="async" src="..." alt="A descriptive alt text" />
</picture>
<script nonce="abc123">
  document.querySelector('[data-ds-id="ds-1a2b3c4d"]').addEventListener('load',function(){this.style.filter='none';this.style.backgroundImage='none'});
</script>
```

With a nonce, your policy only needs to allow nonce-based inline styles and scripts — no `'unsafe-inline'`:

```
Content-Security-Policy: style-src 'nonce-abc123'; script-src 'nonce-abc123';
```

When lazy loading is disabled or no LQIP preview type is configured, the `nonce` has no effect (there is nothing inline to protect). The `<ds:img>` tag helper supports the same `nonce` attribute for the single-`<img>` variant.

> **Deprecated:** `<ds:picture-csp>` is now an obsolete alias of `<ds:picture>` and behaves identically to `<ds:picture nonce="…">`. Prefer `<ds:picture>` with a `nonce`; the `-csp` element will be removed in a future version.
