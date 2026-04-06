# Lazy Loading

DotSee.ResponsiveImages uses **native browser lazy loading** (`loading="lazy"`) with optional LQIP (Low-Quality Image Placeholder) previews. No JavaScript library is required.

## How It Works

When lazy loading is enabled:

1. The `<img>` element receives `loading="lazy" decoding="async"` attributes
2. The browser defers loading until the image is near the viewport
3. If a preview type is configured, a low-quality placeholder is shown as a CSS `background-image` while the real image loads
4. On load, the placeholder is removed via an inline `onload` handler

All `<source>` elements inside `<picture>` use real `srcset` attributes (not `data-srcset`), so browsers that don't support lazy loading simply load all images normally.

## Configuration

### Global Settings

Add the `lazyload` section at the root of your `appsettings.json`:

```json
{
  "lazyload": {
    "EnablelazyLoad": true,
    "PreviewType": "Blur",
    "LowResImagePath": "/img/placeholder-lowres.jpg"
  }
}
```

| Property | Type | Description |
|---|---|---|
| `EnablelazyLoad` | bool | Enable lazy loading globally for all rule sets. |
| `PreviewType` | string | `"Blur"` or `"LowResImage"` (see below). |
| `LowResImagePath` | string | Path to a generic placeholder image. Only used with `"LowResImage"` preview type. |

### Per-Rule-Set Override

Each rule set can override the global setting with the `LazyLoad` property:

```json
{
  "DotSee": {
    "ResponsiveImages": [
      {
        "Name": "hero",
        "LazyLoad": false
      },
      {
        "Name": "gallery",
        "LazyLoad": true
      }
    ]
  }
}
```

Set `LazyLoad` to `null` (or omit it) to inherit the global setting.

## Preview Types

### Blur

```json
{
  "lazyload": {
    "EnablelazyLoad": true,
    "PreviewType": "Blur"
  }
}
```

Generates a tiny (40px wide, quality 20) version of the actual image and displays it as a blurred CSS background while the full image loads.

**Rendered HTML:**

```html
<img src="/media/.../image.jpg"
     loading="lazy"
     decoding="async"
     style="background-size:cover;background-repeat:no-repeat;background-image:url('/media/.../image.jpg?width=40&quality=20');filter:blur(20px);transition:filter 0.3s"
     onload="this.style.filter='none';this.style.backgroundImage='none'"
     alt="..." />
```

**How it looks:**
1. A heavily blurred version of the image appears immediately
2. The full image loads in the background
3. On load, the blur filter and background are removed with a 0.3s CSS transition

**Pros:** Each placeholder matches the actual image content. No additional assets to manage.

**Cons:** Adds a small extra image request per image (though at ~40px and quality 20, these are typically under 1KB).

### LowResImage

```json
{
  "lazyload": {
    "EnablelazyLoad": true,
    "PreviewType": "LowResImage",
    "LowResImagePath": "/img/placeholder.jpg"
  }
}
```

Displays a single generic placeholder image as a CSS background while the real image loads.

**Rendered HTML:**

```html
<img src="/media/.../image.jpg"
     loading="lazy"
     decoding="async"
     style="background-size:cover;background-repeat:no-repeat;background-image:url('/img/placeholder.jpg')"
     onload="this.style.backgroundImage='none'"
     alt="..." />
```

**How it looks:**
1. The generic placeholder appears immediately (cached after first load)
2. The full image loads and covers the placeholder
3. On load, the background image is removed

**Pros:** Only one placeholder image for the entire site. Cached by the browser after first use.

**Cons:** All images show the same placeholder, so there's no visual hint of the actual content.

### No Preview Type

If `PreviewType` is not configured (defaults to 0, which is neither `Blur` nor `LowResImage`), only `loading="lazy" decoding="async"` attributes are added. No placeholder is shown.

```json
{
  "lazyload": {
    "EnablelazyLoad": true
  }
}
```

```html
<img src="/media/.../image.jpg"
     loading="lazy"
     decoding="async"
     alt="..." />
```

## Lazy Loading in Picture Elements vs Img Elements

Both `CreatePictureElement` (and the `<ds:picture>` tag helper) and `CreateMarkup` support lazy loading:

- **`<picture>` elements**: `loading="lazy"` and LQIP styles are applied to the fallback `<img>` inside the `<picture>`. The `<source>` elements always use real `srcset` attributes.
- **`<img>` elements** (from `CreateMarkup`): `loading="lazy"` and LQIP styles are applied directly to the `<img>`.

## Best Practices

- **Don't lazy-load above-the-fold images.** Set `LazyLoad: false` on rule sets used for hero images or any content visible without scrolling. Lazy loading above-the-fold images delays their rendering and hurts LCP (Largest Contentful Paint).

- **Use Blur for content-heavy pages.** The blur preview gives users a visual hint of what's loading, which feels faster than a generic placeholder.

- **Use LowResImage for uniform layouts.** If all images are similar (e.g., product cards with the same aspect ratio), a single placeholder works well and reduces requests.

- **Keep the LowResImagePath image small.** The placeholder should be a tiny, optimised file (ideally under 5KB). It's loaded for every image on the page until cached.

## Content Security Policy (CSP)

The default `<ds:picture>` tag helper and `CreateMarkup`/`CreatePictureElement` methods use inline `style` and `onload` attributes for LQIP previews. A strict CSP that forbids `'unsafe-inline'` will block these.

Use the `<ds:picture-csp>` tag helper instead. It replaces inline attributes with nonce-tagged `<style>` and `<script>` blocks:

```cshtml
<ds:picture-csp image="@Model.Image"
                rule-set="default"
                image-alt="My image"
                nonce="@ViewData["CspNonce"]" />
```

For the Razor API (`SrcSetManager`), you can disable inline LQIP and handle it yourself:

```cshtml
@_srcSetManager.CreatePictureElement(
    Model.Image, "default",
    imageAlt: "My image",
    imageAttributes: new Dictionary<string, string> { { "data-ds-id", "my-img" } },
    emitInlineLqip: false)
```

Then add your own nonce-tagged blocks targeting the `[data-ds-id="my-img"]` selector.

See [Tag Helpers - ds:picture-csp](tag-helpers.md#dspicture-csp) for full details.

## Browser Support

Native `loading="lazy"` is supported by all modern browsers (Chrome, Firefox, Safari 15.4+, Edge). In browsers that don't support it, images load normally (no lazy loading, no degradation).
