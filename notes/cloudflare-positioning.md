# Positioning vs. Cloudflare — analysis & priorities

> Internal strategy note (not user-facing docs). Written 2026-08-07.
> Question it answers: *is this package still worth anything now that Cloudflare
> does images, and what should we build that Cloudflare doesn't?*

## Short answer

Still useful — but the package and Cloudflare solve **different halves** of the same
problem, and our docs currently don't say so. The overlap is smaller than it feels.

| | Cloudflare | This package |
|---|---|---|
| **Bytes per pixel** (encoding, AVIF/WebP, quality) | Polish / `format=auto` — does it well, zero code | `useWebP` + `ImageQuality` |
| **Pixels delivered** (resolution matched to layout box) | ✗ Polish never resizes. Transformations give a *URL API*, not markup | ✓ this is the whole product |
| **Markup**: `<picture>`, `srcset`, `sizes`, media queries | ✗ Cloudflare does not write your HTML | ✓ |
| **Art direction** (different crop per breakpoint) | ✗ `gravity=auto` guesses; can't know the editor's crop | ✓ |
| **Editorial focal point / named Umbraco crops** | ✗ that data lives in the CMS | ✓ |
| **LQIP, CSP nonce, responsive CSS backgrounds** | ✗ | ✓ |

Polish serving a beautifully-encoded AVIF at 2400px to a 375px phone is still a ~10×
waste. Resolution switching is the bigger win and Cloudflare structurally cannot do it —
it has no idea what the CSS layout box is. Only markup generated at render time knows.

**The one feature Cloudflare genuinely obsoletes is `useWebP`.** Stop treating format
negotiation as a selling point.

Real competition isn't Cloudflare — it's Slimsy and hand-rolled partials. Our
differentiator there is already good: no JS dependency, native `loading="lazy"`,
CSP-safe LQIP.

## Priorities

### 1. Pluggable URL backend — highest value by a distance

`ImageUrlService.cs:48` hard-wires everything to Umbraco's `IImageUrlGenerator`, i.e.
ImageSharp.Web querystrings. Extract an `IResponsiveImageUrlProvider` so the package can
emit `/cdn-cgi/image/width=800,quality=80,format=auto/<origin>` instead.

Flips the story from "competes with Cloudflare" to "**drives** Cloudflare" — we own the
markup, Cloudflare owns the pixels and the origin CPU. Same seam gets imgix, Cloudinary,
Azure CDN for free, and lets `format=auto` replace `useWebP`.

### 2. A variant budget

Cloudflare Transformations bill per unique transformation. 4 breakpoints × 3 DPI × alt
images is a lot of billable variants, most never requested. A "generate at most N
candidates, snapped to a shared width ladder across rule sets" option is a feature that
*saves customers money* — nobody in the Umbraco space is selling that. Only becomes
relevant once #1 exists.

### 3. Close the Core Web Vitals gaps

We emit `loading="lazy" decoding="async"` (`PictureElementRenderer.cs:248`,
`SrcSetManager.cs:256`) but no intrinsic `width`/`height` on the `<img>` — that's CLS
left on the table, and it's cheap since the crop dimensions are already known. Also want
first-class support (not just `attr-` passthrough) for:

- `fetchpriority="high"` + suppressing lazy on an `above-fold`/`hero` flag — an LCP hero
  that's `loading="lazy"` is an active regression
- `sizes="auto"` for lazy images, which sidesteps the single thing everyone gets wrong
  about `sizes`

### 4. Double down on the editorial layer

Focal point, named crops, per-breakpoint art direction — this is the moat. It's CMS data
and no CDN will ever have it. Anything that makes an editor's crop choice render
correctly at every breakpoint is defensible work.

### 5. Reposition the docs

Add a "Using with Cloudflare / a CDN" page saying plainly: keep Polish on, turn
`useWebP` off, let us handle resolution and art direction. Today a Cloudflare customer
reading the README sees overlap and bounces.

## Deprioritise

Anything about compression, format conversion, or byte savings as a headline. That war is
over and the CDN won it.
