using DotSee.ResponsiveImages.LazyLoad;
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
    public void CreatePictureElement_Use2x_EmitsRetinaSources()
    {
        var h = new RenderHarness(ruleSet: RenderHarness.DefaultRuleSet(use2x: true));
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.Contains("-webkit-min-device-pixel-ratio: 5/4", html);
        // regular + 2x source per breakpoint (4 breakpoints incl. synthetic)
        Assert.Equal(8, Count(html, "<source "));
    }

    [Fact]
    public void CreatePictureElement_Use3x_EmitsThreeXSources()
    {
        var h = new RenderHarness(ruleSet: RenderHarness.DefaultRuleSet(use3x: true));
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.Contains("-webkit-min-device-pixel-ratio: 2.25", html);
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
