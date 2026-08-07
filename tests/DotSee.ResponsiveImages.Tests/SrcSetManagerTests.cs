using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using Microsoft.AspNetCore.Html;
using Xunit;

namespace DotSee.ResponsiveImages.Tests;

/// <summary>
/// Structural tests for the SrcSetManager render API. Crop URLs are produced by Umbraco's
/// GetCropUrl pipeline (out of scope for a unit test), so these assert on the markup structure,
/// attributes, breakpoints, lazy-loading, sizes and CSS that this library is responsible for.
/// </summary>
public class SrcSetManagerTests
{
    private static int Count(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) != -1) { count++; i += needle.Length; }
        return count;
    }

    // ---- CreatePictureElement ----

    [Fact]
    public void CreatePictureElement_RendersPictureWithOneSourcePerBreakpointPlusSynthetic()
    {
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "alt text", imageClass: "cls")!.ToString();

        Assert.Contains("<picture>", html);
        Assert.Contains("</picture>", html);
        // 3 configured breakpoints + 1 synthetic (min-width: 1px)
        Assert.Equal(4, Count(html, "<source "));
        Assert.Contains("only screen and (min-width: 1200px)", html);
        Assert.Contains("only screen and (min-width: 768px)", html);
        Assert.Contains("only screen and (min-width: 576px)", html);
        Assert.Contains("only screen and (min-width: 1px)", html);
    }

    [Fact]
    public void CreatePictureElement_OrdersSourcesLargestBreakpointFirst()
    {
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.True(html.IndexOf("min-width: 1200px", System.StringComparison.Ordinal)
                    < html.IndexOf("min-width: 768px", System.StringComparison.Ordinal));
        Assert.True(html.IndexOf("min-width: 768px", System.StringComparison.Ordinal)
                    < html.IndexOf("min-width: 576px", System.StringComparison.Ordinal));
    }

    [Fact]
    public void CreatePictureElement_AppliesAltClassAndLazyLoadAttributes()
    {
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "my alt", imageClass: "hero")!.ToString();

        Assert.Contains("class=\"hero\"", html);
        Assert.Contains("alt=\"my alt\"", html);
        Assert.Contains("loading=\"lazy\"", html);
        Assert.Contains("decoding=\"async\"", html);
    }

    [Fact]
    public void CreatePictureElement_LowResImagePreview_EmitsPlaceholderBackground()
    {
        var h = new RenderHarness(previewType: PreviewType.LowResImage, lowResPath: "/img/ph.jpg");
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.Contains("background-image:url('/img/ph.jpg')", html);
        Assert.Contains("onload=\"this.style.backgroundImage='none'\"", html);
    }

    [Fact]
    public void CreatePictureElement_BlurPreview_EmitsBlurFilter()
    {
        var h = new RenderHarness(previewType: PreviewType.Blur);
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.Contains("filter:blur(20px)", html);
        Assert.Contains("onload=\"this.style.filter='none'", html);
    }

    [Fact]
    public void CreatePictureElement_LazyLoadDisabled_OmitsLazyAttributes()
    {
        // Global off AND rule set off (rule set LazyLoad overrides the global, so it must also be off).
        var h = new RenderHarness(enableLazyLoad: false, ruleSet: RenderHarness.DefaultRuleSet(lazyLoad: false));
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.DoesNotContain("loading=\"lazy\"", html);
        Assert.DoesNotContain("filter:blur", html);
    }

    [Fact]
    public void IsLazyLoadEnabled_RuleSetOverridesGlobal()
    {
        // Global off, but rule set on -> enabled (the override matrix).
        var h = new RenderHarness(enableLazyLoad: false, ruleSet: RenderHarness.DefaultRuleSet(lazyLoad: true));
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.Contains("loading=\"lazy\"", html);
    }

    [Fact]
    public void CreatePictureElement_Use2x_EmitsRetinaCandidateOnTheSameSource()
    {
        var h = new RenderHarness(ruleSet: RenderHarness.DefaultRuleSet(use2x: true));
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        // DPI is expressed as an x-descriptor candidate, so the source count stays one per breakpoint
        // (4 incl. synthetic) instead of being multiplied by the number of pixel ratios.
        Assert.Equal(4, Count(html, "<source "));
        Assert.Contains(" 2x\"", html);
        Assert.DoesNotContain("device-pixel-ratio", html);
    }

    [Fact]
    public void CreatePictureElement_Use3x_EmitsThreeXCandidate()
    {
        var h = new RenderHarness(ruleSet: RenderHarness.DefaultRuleSet(use3x: true));
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.Equal(4, Count(html, "<source "));
        Assert.Contains(" 3x\"", html);
        Assert.DoesNotContain("device-pixel-ratio", html);
    }

    [Fact]
    public void CreatePictureElement_AboveFold_LoadsEagerlyAtHighPriorityWithoutPlaceholder()
    {
        var h = new RenderHarness(previewType: PreviewType.Blur);
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a", aboveFold: true)!.ToString();

        Assert.Contains("loading=\"eager\"", html);
        Assert.Contains("fetchpriority=\"high\"", html);
        Assert.DoesNotContain("loading=\"lazy\"", html);
        Assert.DoesNotContain("onload=", html);
    }

    [Fact]
    public void CreatePictureElement_AboveFold_DoesNotOverrideAnExplicitFetchPriority()
    {
        var h = new RenderHarness();
        var attrs = new Dictionary<string, string> { ["fetchpriority"] = "low" };
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a", imageAttributes: attrs, aboveFold: true)!.ToString();

        Assert.Contains("fetchpriority=\"low\"", html);
        Assert.DoesNotContain("fetchpriority=\"high\"", html);
    }

    // Focal-point anchoring itself happens inside Umbraco's GetCropUrl and cannot be asserted here:
    // this harness cannot produce crop URLs at all (Umbraco returns null before reaching the
    // IImageUrlGenerator stub), which predates these changes. Verify it on the TestWebsite.
    [Fact]
    public void RuleSet_UsesFocalPointByDefault()
    {
        Assert.True(new RuleSet("any").UseFocalPoint);
        Assert.True(RenderHarness.DefaultRuleSet().UseFocalPoint);
    }

    [Fact]
    public void CreatePictureElement_EmitsIntrinsicWidthAndHeight()
    {
        var h = new RenderHarness();
        var img = h.CreateImage(intrinsicWidth: 4000, intrinsicHeight: 3000);
        var html = h.SrcSetManager.CreatePictureElement(img, "defaultset", imageAlt: "a")!.ToString();

        // Rule set caps width at 1920; the height follows the media item's own 4:3 ratio.
        Assert.Contains("width=\"1920\" height=\"1440\"", html);
    }

    [Fact]
    public void CreatePictureElement_CallerSuppliedDimensions_AreNotOverridden()
    {
        var h = new RenderHarness();
        var img = h.CreateImage(intrinsicWidth: 4000, intrinsicHeight: 3000);
        var attrs = new Dictionary<string, string> { ["width"] = "300", ["height"] = "200" };
        var html = h.SrcSetManager.CreatePictureElement(img, "defaultset", imageAlt: "a", imageAttributes: attrs)!.ToString();

        Assert.Contains("width=\"300\"", html);
        Assert.DoesNotContain("width=\"1920\"", html);
    }

    [Fact]
    public void CreatePictureElement_BlurPreview_PrefersInlineDataUriPlaceholder()
    {
        const string dataUri = "data:image/webp;base64,UklGRhoAAABXRUJQ";
        var h = new RenderHarness(previewType: PreviewType.Blur, lqipService: new FakeLqipService(dataUri));
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.Contains($"background-image:url('{dataUri}')", html);
    }

    [Fact]
    public void CreatePictureElement_PassesThroughExtraImageAttributes()
    {
        var h = new RenderHarness();
        var attrs = new System.Collections.Generic.Dictionary<string, string>
        {
            ["fetchpriority"] = "high",
            ["data-x"] = "y"
        };
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a", imageAttributes: attrs)!.ToString();

        Assert.Contains("fetchpriority=\"high\"", html);
        Assert.Contains("data-x=\"y\"", html);
    }

    [Fact]
    public void CreatePictureElement_Svg_RendersPlainImgNotPicture()
    {
        var h = new RenderHarness(imageUrl: "/media/test/logo.svg");
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.DoesNotContain("<picture", html);
        Assert.Contains("<img", html);
    }

    // ---- CreateMarkup ----

    [Fact]
    public void CreateMarkup_RendersSingleImgWithSrcsetAndSizes()
    {
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreateMarkup(h.CreateImage(), "defaultset", alt: "a", title: "t", imageClass: "c")!.ToString();

        Assert.DoesNotContain("<picture", html);
        Assert.Contains("<img", html);
        Assert.Contains("srcset=", html);
        Assert.Contains("1200w", html);
        Assert.Contains("768w", html);
        Assert.Contains("576w", html);
        Assert.Contains("sizes=", html);
        Assert.Contains("50vw", html);
        Assert.Contains("alt=\"a\"", html);
        Assert.Contains("title=\"t\"", html);
        Assert.Contains("class=\"c\"", html);
    }

    [Fact]
    public void CreateMarkup_WithoutUse2xOr3x_EmitsExactlyOneCandidatePerBreakpoint()
    {
        var h = new RenderHarness(); // DefaultRuleSet: Use2x and Use3x both false
        var html = h.SrcSetManager.CreateMarkup(h.CreateImage(), "defaultset", alt: "a")!.ToString();

        var candidates = html.Split("srcset=\"")[1].Split('"')[0].Split(',');

        Assert.Equal(3, candidates.Length);
        Assert.Equal(new[] { "576w", "768w", "1200w" },
            candidates.Select(x => x.Trim().Split(' ').Last()).ToArray());
    }

    [Fact]
    public void CreatePictureElement_WithoutUse2xOr3x_EmitsOneCandidatePerSource()
    {
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        var srcsets = html.Split("srcset=\"").Skip(1).Select(x => x.Split('"')[0]).ToList();

        Assert.Equal(4, srcsets.Count); // 3 breakpoints + synthetic
        Assert.All(srcsets, s => Assert.DoesNotContain(",", s));
        Assert.DoesNotContain(" 2x", html);
        Assert.DoesNotContain(" 3x", html);
    }

    [Fact]
    public void CreateMarkup_DescriptorReportsTheGeneratedWidth_NotTheBreakpoint()
    {
        // A breakpoint may generate an image narrower than the viewport width it targets. The "w"
        // descriptor must state the pixel width actually generated, or the browser picks a candidate
        // that is smaller than it believes and upscales it.
        var rs = new RuleSet("narrow") { ImageQuality = 70, OriginalImageMaxWidth = 1920 };
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 1200, Width = 600, Height = 0 });
        rs.Sizes.Add("50vw");

        var h = new RenderHarness(ruleSet: rs);
        var html = h.SrcSetManager.CreateMarkup(h.CreateImage(), "narrow", alt: "a")!.ToString();

        var srcset = html.Split("srcset=\"")[1].Split('"')[0];

        Assert.EndsWith("600w", srcset.Trim());
    }

    [Fact]
    public void CreateMarkup_Use2xAnd3x_SrcsetStaysWidthDescriptorOnly()
    {
        // Mixing "w" and "x" descriptors in one srcset is invalid and browsers discard the whole
        // attribute, so DPI variants must appear as wider "w" candidates instead.
        var h = new RenderHarness(ruleSet: RenderHarness.DefaultRuleSet(use2x: true, use3x: true));
        var html = h.SrcSetManager.CreateMarkup(h.CreateImage(), "defaultset", alt: "a")!.ToString();

        var srcset = html.Split("srcset=\"")[1].Split('"')[0];

        Assert.DoesNotContain(" 2x", srcset);
        Assert.DoesNotContain(" 3x", srcset);
        Assert.All(srcset.Split(','), candidate => Assert.EndsWith("w", candidate.Trim()));
        // 576/768/1200 plus their DPI multiples, clamped to the 1920 maximum.
        Assert.Contains("1152w", srcset);
        Assert.Contains("1920w", srcset);
    }

    [Fact]
    public void CreateMarkup_SizesFallback_UsesLayoutWidthNotLargestCandidate()
    {
        var h = new RenderHarness(ruleSet: RenderHarness.DefaultRuleSet(use2x: true));
        var html = h.SrcSetManager.CreateMarkup(h.CreateImage(), "defaultset", alt: "a")!.ToString();

        var sizes = html.Split("sizes=\"")[1].Split('"')[0];

        Assert.EndsWith("1200px", sizes);
    }

    [Fact]
    public void CreateMarkup_AboveFold_LoadsEagerlyAtHighPriority()
    {
        var h = new RenderHarness(previewType: PreviewType.Blur);
        var html = h.SrcSetManager.CreateMarkup(h.CreateImage(), "defaultset", alt: "a", aboveFold: true)!.ToString();

        Assert.Contains("loading=\"eager\"", html);
        Assert.Contains("fetchpriority=\"high\"", html);
        Assert.DoesNotContain("loading=\"lazy\"", html);
        Assert.DoesNotContain("onload=", html);
    }

    [Fact]
    public void CreateMarkup_EmitsIntrinsicWidthAndHeight()
    {
        var h = new RenderHarness();
        var img = h.CreateImage(intrinsicWidth: 4000, intrinsicHeight: 3000);
        var html = h.SrcSetManager.CreateMarkup(img, "defaultset", alt: "a")!.ToString();

        Assert.Contains("width=\"1920\" height=\"1440\"", html);
    }

    // ---- GetSrcSet / GetSizes ----

    [Fact]
    public void GetSrcSet_ContainsWidthDescriptorsForEachBreakpoint()
    {
        var h = new RenderHarness();
        var srcset = h.SrcSetManager.GetSrcSet(h.CreateImage(), "defaultset")!.ToString();

        Assert.Contains("1200w", srcset);
        Assert.Contains("768w", srcset);
        Assert.Contains("576w", srcset);
    }

    [Fact]
    public void GetSizes_ComposesConfiguredSizesPlusMaxWidth()
    {
        var h = new RenderHarness();
        var sizes = h.SrcSetManager.GetSizes(h.CreateImage(), h.RuleSet)!.ToString();

        Assert.Contains("(max-width: 576px) 100vw", sizes);
        Assert.Contains("50vw", sizes);
        Assert.Contains("1200px", sizes);
    }

    // ---- GetBreakPointsCss / GetClassName ----

    [Fact]
    public void GetBreakPointsCss_RendersStyleWithGeneratedClassAndMediaQueries()
    {
        var h = new RenderHarness();
        var img = h.CreateImage();
        var css = h.SrcSetManager.GetBreakPointsCss(img, "defaultset")!.ToString();
        var className = h.SrcSetManager.GetClassName(img, "defaultset");

        Assert.Contains("<style", css);
        Assert.Contains("</style>", css);
        Assert.Contains("." + className, css);
        Assert.Contains("@media only screen", css);
        Assert.Contains("background-image", css);
    }

    [Fact]
    public void GetBreakPointsCss_DefaultFocalPoint_IsCentered()
    {
        var h = new RenderHarness();
        var css = h.SrcSetManager.GetBreakPointsCss(h.CreateImage(), "defaultset")!.ToString();

        Assert.Contains("background-position:50% 50%", css);
    }

    [Fact]
    public void GetBreakPointsCss_UsesImageFocalPoint()
    {
        var h = new RenderHarness();
        var img = h.CreateImage(focalLeft: 0.25m, focalTop: 0.75m);
        var css = h.SrcSetManager.GetBreakPointsCss(img, "defaultset")!.ToString();

        Assert.Contains("background-position:25% 75%", css);
    }

    [Fact]
    public void GetBreakPointsCss_EmitsNonceWhenProvided()
    {
        var h = new RenderHarness();
        var css = h.SrcSetManager.GetBreakPointsCss(h.CreateImage(), "defaultset", null, new HtmlString("abc123"))!.ToString();

        Assert.Contains("nonce='abc123'", css);
    }

    [Fact]
    public void GetBreakPointsCss_InjectsPerCallNonce_NotAStaleCachedOne()
    {
        var h = new RenderHarness();
        var img = h.CreateImage();

        var css1 = h.SrcSetManager.GetBreakPointsCss(img, "defaultset", null, new HtmlString("nonceA"))!.ToString();
        var css2 = h.SrcSetManager.GetBreakPointsCss(img, "defaultset", null, new HtmlString("nonceB"))!.ToString();

        Assert.Contains("nonce='nonceA'", css1);
        Assert.Contains("nonce='nonceB'", css2);
        // second call reuses the cached body but must NOT carry the first call's nonce
        Assert.DoesNotContain("nonceA", css2);
    }

    [Fact]
    public void GetBreakPointsCss_WithoutNonce_HasNoNonceAttribute()
    {
        var h = new RenderHarness();
        var css = h.SrcSetManager.GetBreakPointsCss(h.CreateImage(), "defaultset")!.ToString();

        Assert.DoesNotContain("nonce=", css);
    }

    [Fact]
    public void CreatePictureElement_CspMode_InjectsPerCallDataDsId_NotAStaleCachedOne()
    {
        var h = new RenderHarness();
        var img = h.CreateImage();

        var p1 = h.SrcSetManager.CreatePictureElement(img, "defaultset", imageAlt: "a",
            imageAttributes: new Dictionary<string, string> { [SrcSetManager.DsIdAttributeName] = "ds-111" },
            emitInlineLqip: false)!.ToString();
        var p2 = h.SrcSetManager.CreatePictureElement(img, "defaultset", imageAlt: "a",
            imageAttributes: new Dictionary<string, string> { [SrcSetManager.DsIdAttributeName] = "ds-222" },
            emitInlineLqip: false)!.ToString();

        Assert.Contains("<img data-ds-id=\"ds-111\"", p1);
        Assert.Contains("<img data-ds-id=\"ds-222\"", p2);
        // second call reuses the cached body but must NOT carry the first call's id
        Assert.DoesNotContain("ds-111", p2);
    }

    [Fact]
    public void GetClassName_MatchesRuleSetAndImageKey()
    {
        var h = new RenderHarness();
        var className = h.SrcSetManager.GetClassName(h.CreateImage(), "defaultset");

        Assert.StartsWith("media-image-RSFor_defaultset_", className);
    }

    // ---- Null handling ----

    [Fact]
    public void GetBreakPointsCss_NullImage_ReturnsNull()
    {
        var h = new RenderHarness();
        Assert.Null(h.SrcSetManager.GetBreakPointsCss(null!, "defaultset"));
    }

    [Fact]
    public void GetClassName_NullImage_ReturnsNull()
    {
        var h = new RenderHarness();
        Assert.Null(h.SrcSetManager.GetClassName(null!, "defaultset"));
    }
}
