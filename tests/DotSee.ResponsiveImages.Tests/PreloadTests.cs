using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using DotSee.ResponsiveImages.Preloading;
using DotSee.ResponsiveImages.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotSee.ResponsiveImages.Tests;

public class PreloadTests
{
    private static string Render(TagHelper th, string tagName)
    {
        var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test-id");
        var output = new TagHelperOutput(tagName, new TagHelperAttributeList(),
            (useCached, encoder) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        th.Process(context, output);

        using var sw = new StringWriter();
        output.WriteTo(sw, HtmlEncoder.Default);
        return sw.ToString();
    }

    // A picture's preload hints are built from the same per-source data the <picture> renders, so this
    // asserts on that shared shape. The hints' own imagesrcset cannot be asserted here: this harness
    // cannot produce crop URLs (see CLAUDE.md), so every srcset comes out empty.
    [Fact]
    public void PictureSources_AreOnePerBreakpointWithMediaAndDimensions()
    {
        var h = new RenderHarness();
        var sources = h.PictureRenderer.BuildSources(h.CreateImage(), h.RuleSet);

        Assert.Equal(4, sources.Count); // 3 breakpoints + synthetic
        Assert.Equal("only screen and (min-width: 1200px)", sources[0].Media);
        Assert.Equal("only screen and (min-width: 1px)", sources[3].Media);
        Assert.Equal(1200, sources[0].Width);
    }

    [Fact]
    public void PicturePreload_SkipsSourcesWithNothingToFetch()
    {
        var h = new RenderHarness();
        var collector = new PreloadCollector();
        var th = new PictureElementTagHelper(h.SrcSetManager, NullLogger<PictureElementTagHelper>.Instance, collector)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "a",
            AboveFold = true
        };

        Render(th, "ds:picture");

        //Empty srcsets must not become hints that cost the browser an evaluation and fetch nothing.
        Assert.Empty(collector.Links);
    }

    [Fact]
    public void ImgAboveFold_RegistersSrcsetAndSizes()
    {
        var h = new RenderHarness();
        var collector = new PreloadCollector();
        var th = new ImgTagHelper(h.SrcSetManager, NullLogger<ImgTagHelper>.Instance, collector)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "a",
            AboveFold = true
        };

        Render(th, "ds:img");

        var link = Assert.Single(collector.Links);
        Assert.Contains("imagesrcset=", link);
        Assert.Contains("imagesizes=", link);
        Assert.Contains("1200w", link);
    }

    [Fact]
    public void WithoutAboveFold_NothingIsRegistered()
    {
        var h = new RenderHarness();
        var collector = new PreloadCollector();
        var th = new PictureElementTagHelper(h.SrcSetManager, NullLogger<PictureElementTagHelper>.Instance, collector)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "a"
        };

        Render(th, "ds:picture");

        Assert.Empty(collector.Links);
    }

    [Fact]
    public void PreloadCanBeDisabledOnAnAboveFoldImage()
    {
        var h = new RenderHarness();
        var collector = new PreloadCollector();
        var th = new ImgTagHelper(h.SrcSetManager, NullLogger<ImgTagHelper>.Instance, collector)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "a",
            AboveFold = true,
            Preload = false
        };

        var html = Render(th, "ds:img");

        Assert.Empty(collector.Links);
        Assert.Contains("fetchpriority=\"high\"", html); // still eager, just not preloaded
    }

    [Fact]
    public void Collector_CollapsesIdenticalHints()
    {
        var collector = new PreloadCollector();
        collector.Add("<link rel=\"preload\" href=\"/a.jpg\" />");
        collector.Add("<link rel=\"preload\" href=\"/a.jpg\" />");
        collector.Add("<link rel=\"preload\" href=\"/b.jpg\" />");

        Assert.Equal(2, collector.Links.Count);
    }

    [Fact]
    public void PreloadsTagHelper_EmitsCollectedLinksAndNoWrapperElement()
    {
        var collector = new PreloadCollector();
        collector.Add("<link rel=\"preload\" href=\"/a.jpg\" />");

        var html = Render(new PreloadsTagHelper(collector), "ds:preloads");

        Assert.Equal("<link rel=\"preload\" href=\"/a.jpg\" />", html);
        Assert.DoesNotContain("ds:preloads", html);
    }

    [Fact]
    public void PreloadsTagHelper_EmitsNothingWhenNoImagesWereMarked()
    {
        Assert.Equal(string.Empty, Render(new PreloadsTagHelper(new PreloadCollector()), "ds:preloads"));
    }

    private static int Count(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, System.StringComparison.Ordinal)) != -1) { count++; i += needle.Length; }
        return count;
    }
}
