# Tag Helpers

## Setup

Register the tag helpers in your `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, DotSee.ResponsiveImages
```

## ds:picture

Renders a `<picture>` element with responsive `<source>` tags for each breakpoint, including 2x/3x variants when configured.

### Attributes

| Attribute | Type | Required | Description |
|---|---|---|---|
| `image` | `MediaWithCrops` | Yes | The Umbraco media item. Must be a `MediaWithCrops` object, not a URL string. |
| `rule-set` | string | Yes | Name of the rule set from `appsettings.json`. |
| `image-alt` | string | Yes | Alt text for the image. A warning is shown if omitted (unless suppressed). |
| `wrapper-element` | string | No | HTML element to wrap the `<picture>` in (e.g., `div`, `figure`). |
| `wrapper-class` | string | No | CSS class applied to the wrapper element. |
| `image-class` | string | No | CSS class applied to the `<img>` inside the picture element. |
| `suppress-warnings` | bool | No | Set to `true` to hide validation error messages from the rendered output. Defaults to `false`. |

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
- When lazy loading with LQIP is enabled, `<ds:picture>` uses inline `style` and `onload` attributes. If your site uses a Content Security Policy, use `<ds:picture-csp>` instead (see below).

---

## ds:picture-csp

A CSP-safe version of `<ds:picture>`. Instead of inline `style` and `onload` attributes for LQIP previews, it emits nonce-tagged `<style>` and `<script>` blocks that comply with strict Content Security Policies.

When lazy loading is disabled or no LQIP preview type is configured, `<ds:picture-csp>` behaves identically to `<ds:picture>`.

### Attributes

All attributes from `<ds:picture>` are supported, plus:

| Attribute | Type | Required | Description |
|---|---|---|---|
| `nonce` | string | Yes | The CSP nonce value for inline `<style>` and `<script>` blocks. |

### Basic Usage

```cshtml
<ds:picture-csp image="@Model.Image"
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

### CSP Policy Requirements

With `<ds:picture-csp>`, your Content Security Policy only needs to allow nonce-based inline styles and scripts:

```
Content-Security-Policy: style-src 'nonce-abc123'; script-src 'nonce-abc123';
```

No `'unsafe-inline'` is required.

### With Wrapper Element

```cshtml
<ds:picture-csp image="@Model.Image"
                rule-set="hero"
                image-alt="Hero banner"
                nonce="@ViewData["CspNonce"]"
                wrapper-element="figure"
                wrapper-class="hero-image" />
```

### When to Use Which

| Tag Helper | Use When |
|---|---|
| `<ds:picture>` | No CSP, or CSP allows `'unsafe-inline'` for styles and scripts. Simpler output, slightly smaller HTML. |
| `<ds:picture-csp>` | Strict CSP that requires nonces for inline styles and scripts. |

Both tag helpers produce identical `<picture>` and `<source>` markup. The only difference is how LQIP previews are applied when lazy loading is enabled.
