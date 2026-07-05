using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using DotSee.ResponsiveImages.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DotSee.ResponsiveImages.Tests;

public class TagHelperTests
{
    /// <summary>
    /// Executes a tag helper and returns the rendered HTML, replicating the framework's preservation
    /// of child content when the helper doesn't modify Content.
    /// </summary>
    private static string Render(TagHelper th, string tagName, string? childContent = null)
    {
        var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test-id");
        var output = new TagHelperOutput(tagName, new TagHelperAttributeList(), (useCached, encoder) =>
        {
            var content = new DefaultTagHelperContent();
            if (childContent != null) content.SetHtmlContent(childContent);
            return Task.FromResult<TagHelperContent>(content);
        });

        th.Process(context, output);

        if (!output.IsContentModified)
        {
            var child = output.GetChildContentAsync().GetAwaiter().GetResult();
            output.Content.SetHtmlContent(child);
        }

        using var sw = new StringWriter();
        output.WriteTo(sw, HtmlEncoder.Default);
        return sw.ToString();
    }

    // ---- ds:picture ----

    [Fact]
    public void DsPicture_RendersPictureElement()
    {
        var h = new RenderHarness();
        var th = new PictureElementTagHelper(h.SrcSetManager, NullLogger<PictureElementTagHelper>.Instance)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "a"
        };

        var html = Render(th, "ds:picture");

        Assert.Contains("<picture", html);
    }

    [Fact]
    public void DsPicture_WrapsInWrapperElementWithClass()
    {
        var h = new RenderHarness();
        var th = new PictureElementTagHelper(h.SrcSetManager, NullLogger<PictureElementTagHelper>.Instance)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "a",
            WrapperElement = "figure",
            WrapperClass = "wrap"
        };

        var html = Render(th, "ds:picture");

        Assert.StartsWith("<figure", html);
        Assert.Contains("class=\"wrap\"", html);
        Assert.Contains("<picture", html);
        Assert.EndsWith("</figure>", html);
    }

    [Fact]
    public void DsPicture_PassesThroughImageClassAndExtraAttributes()
    {
        var h = new RenderHarness();
        var th = new PictureElementTagHelper(h.SrcSetManager, NullLogger<PictureElementTagHelper>.Instance)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "a",
            ImageClass = "hero"
        };
        th.ImageAttributes["fetchpriority"] = "high";

        var html = Render(th, "ds:picture");

        Assert.Contains("class=\"hero\"", html);
        Assert.Contains("fetchpriority=\"high\"", html);
    }

    [Fact]
    public void DsPicture_NullImage_SuppressesOutput()
    {
        var h = new RenderHarness();
        var th = new PictureElementTagHelper(h.SrcSetManager, NullLogger<PictureElementTagHelper>.Instance)
        {
            Image = null!,
            RuleSet = "defaultset",
            ImageAlt = "a"
        };

        var html = Render(th, "ds:picture");

        Assert.True(string.IsNullOrEmpty(html));
    }

    [Fact]
    public void DsPicture_MissingAlt_RendersWarning()
    {
        var h = new RenderHarness();
        var th = new PictureElementTagHelper(h.SrcSetManager, NullLogger<PictureElementTagHelper>.Instance)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = ""
        };

        var html = Render(th, "ds:picture");

        Assert.Contains("provide an image alt", html);
    }

    [Fact]
    public void DsPicture_MissingAlt_WithSuppressWarnings_NoWarning()
    {
        var h = new RenderHarness();
        var th = new PictureElementTagHelper(h.SrcSetManager, NullLogger<PictureElementTagHelper>.Instance)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "",
            SuppressWarnings = true
        };

        var html = Render(th, "ds:picture");

        Assert.DoesNotContain("provide an image alt", html);
        Assert.Contains("<picture", html);
    }

    // ---- ds:img ----

    [Fact]
    public void DsImg_RendersImgWithSrcset()
    {
        var h = new RenderHarness();
        var th = new ImgTagHelper(h.SrcSetManager, NullLogger<ImgTagHelper>.Instance)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "a",
            ImageClass = "c"
        };

        var html = Render(th, "ds:img");

        Assert.Contains("<img", html);
        Assert.DoesNotContain("<picture", html);
        Assert.Contains("srcset=", html);
        Assert.Contains("class=\"c\"", html);
    }

    [Fact]
    public void DsImg_NullImage_SuppressesOutput()
    {
        var h = new RenderHarness();
        var th = new ImgTagHelper(h.SrcSetManager, NullLogger<ImgTagHelper>.Instance)
        {
            Image = null!,
            RuleSet = "defaultset",
            ImageAlt = "a"
        };

        Assert.True(string.IsNullOrEmpty(Render(th, "ds:img")));
    }

    // ---- ds:background ----

    [Fact]
    public void DsBackground_RendersElementWithClassStyleAndInnerContent()
    {
        var h = new RenderHarness();
        var th = new BackgroundImageTagHelper(h.SrcSetManager, NullLogger<BackgroundImageTagHelper>.Instance)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            Element = "section"
        };

        var html = Render(th, "ds:background", childContent: "<p>inner</p>");

        Assert.Contains("<section", html);
        Assert.Contains("media-image-RSFor_defaultset_", html);
        Assert.Contains("<style", html);
        Assert.Contains("<p>inner</p>", html);
    }

    [Fact]
    public void DsBackground_EmitsNonceOnStyle()
    {
        var h = new RenderHarness();
        var th = new BackgroundImageTagHelper(h.SrcSetManager, NullLogger<BackgroundImageTagHelper>.Instance)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            Nonce = "n1"
        };

        var html = Render(th, "ds:background");

        Assert.Contains("nonce='n1'", html);
    }

    [Fact]
    public void DsBackground_NullImage_SuppressesOutput()
    {
        var h = new RenderHarness();
        var th = new BackgroundImageTagHelper(h.SrcSetManager, NullLogger<BackgroundImageTagHelper>.Instance)
        {
            Image = null!,
            RuleSet = "defaultset"
        };

        Assert.True(string.IsNullOrEmpty(Render(th, "ds:background")));
    }

    // ---- ds:picture-csp ----

    [Fact]
    public void DsPictureCsp_EmitsNonceStyleAndScriptBlocks()
    {
        var h = new RenderHarness();
        var th = new PictureCspTagHelper(
            h.SrcSetManager, h.Lazy, h.ConfigSource, h.ImageUrlGenerator, h.UrlProvider,
            NullLogger<PictureCspTagHelper>.Instance)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "a",
            Nonce = "n1"
        };

        var html = Render(th, "ds:picture-csp");

        Assert.Contains("<picture", html);
        Assert.Contains("<style nonce=\"n1\"", html);
        Assert.Contains("data-ds-id=\"ds-", html);
        Assert.Contains("<script nonce=\"n1\"", html);
    }

    [Fact]
    public void DsPictureCsp_PassesThroughExtraAttributesAlongsideDataDsId()
    {
        var h = new RenderHarness();
        var th = new PictureCspTagHelper(
            h.SrcSetManager, h.Lazy, h.ConfigSource, h.ImageUrlGenerator, h.UrlProvider,
            NullLogger<PictureCspTagHelper>.Instance)
        {
            Image = h.CreateImage(),
            RuleSet = "defaultset",
            ImageAlt = "a",
            Nonce = "n1"
        };
        th.ImageAttributes["data-x"] = "y";

        var html = Render(th, "ds:picture-csp");

        Assert.Contains("data-x=\"y\"", html);
        Assert.Contains("data-ds-id=\"ds-", html);
    }
}
