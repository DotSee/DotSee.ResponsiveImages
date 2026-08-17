using System;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using DotSee.ResponsiveImages.UrlProviders;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Xunit;

namespace DotSee.ResponsiveImages.Tests;

/// <summary>
/// The one place in this suite that can assert on the *contents* of a generated URL.
/// </summary>
/// <remarks>
/// Everywhere else, the mocked <c>IPublishedContent</c> does not satisfy Umbraco's <c>GetCropUrl</c>, which
/// returns null before ever reaching the <c>IImageUrlGenerator</c> stub — so tests can only check markup
/// structure, and anything about the URL itself has to be verified by running the TestWebsite.
/// <see cref="CloudflareImageUrlProvider"/> never calls <c>GetCropUrl</c>: it composes the URL itself from
/// the rule set and the media path. So the focal point, crop mode, quality and format really are testable
/// here, and that is worth using.
/// </remarks>
public class CloudflareUrlTests
{
    private const string MediaUrl = "/media/test/image.jpg";

    // ---- width / height / order ------------------------------------------------------------------

    [Fact]
    public void UrlIsPrefixOptionsThenSource()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(), 1200, 0);

        Assert.Equal("/cdn-cgi/image/width=1200,fit=cover,quality=70,format=auto,onerror=redirect/media/test/image.jpg", url);
    }

    [Fact]
    public void OptionsAppearInAStableOrder()
    {
        // The whole URL is the CDN's cache key, so two calls asking for the same thing must spell it the
        // same way - otherwise every reordering doubles the billable transformations.
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);
        var image = harness.CreateImage(focalLeft: 0.25m, focalTop: 0.75m);

        var first = provider.GetCropUrl(image, RuleSet(), 800, 600);
        var second = provider.GetCropUrl(image, RuleSet(), 800, 600);

        Assert.Equal(first, second);
        Assert.Equal("/cdn-cgi/image/width=800,height=600,fit=cover,gravity=0.25x0.75,quality=70,format=auto,onerror=redirect/media/test/image.jpg", first);
    }

    [Theory]
    [InlineData(0, 0, "fit=cover")]
    [InlineData(800, 0, "width=800,fit=cover")]
    [InlineData(0, 600, "height=600,fit=cover")]
    [InlineData(800, 600, "width=800,height=600,fit=cover")]
    public void NonPositiveDimensionsAreOmittedSoTheAspectRatioIsPreserved(int width, int height, string expected)
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(), width, height);

        Assert.Contains("/cdn-cgi/image/" + expected + ",", url);
    }

    // ---- crop mode -> fit ------------------------------------------------------------------------

    [Theory]
    [InlineData(ImageCropMode.Crop, "cover")]
    [InlineData(ImageCropMode.Min, "cover")]
    [InlineData(ImageCropMode.Stretch, "cover")]
    [InlineData(ImageCropMode.Max, "scale-down")]
    [InlineData(ImageCropMode.Pad, "pad")]
    [InlineData(ImageCropMode.BoxPad, "pad")]
    public void CropModeMapsToFit(ImageCropMode cropMode, string expectedFit)
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(cropMode: cropMode), 800, 600);

        Assert.Contains("fit=" + expectedFit, url);
    }

    [Fact]
    public void EveryCropModeMapsToSomething()
    {
        // A new Umbraco crop mode must not silently fall through to an empty fit.
        foreach (ImageCropMode mode in Enum.GetValues<ImageCropMode>())
        {
            Assert.False(string.IsNullOrWhiteSpace(CloudflareImageUrlProvider.ToFit(mode)));
        }
    }

    // ---- focal point -> gravity ------------------------------------------------------------------

    [Fact]
    public void FocalPointBecomesGravity()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(focalLeft: 0.3m, focalTop: 0.8m), RuleSet(), 800, 600);

        Assert.Contains("gravity=0.3x0.8", url);
    }

    [Fact]
    public void NoFocalPointMeansNoGravity()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(), 800, 600);

        Assert.DoesNotContain("gravity=", url);
    }

    [Fact]
    public void UseFocalPointFalseSuppressesGravity()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(
            harness.CreateImage(focalLeft: 0.3m, focalTop: 0.8m), RuleSet(useFocalPoint: false), 800, 600);

        Assert.DoesNotContain("gravity=", url);
    }

    [Theory]
    [InlineData(ImageCropMode.Max)]
    [InlineData(ImageCropMode.Pad)]
    public void GravityIsOmittedForFitsThatDoNotCrop(ImageCropMode cropMode)
    {
        // Cloudflare ignores gravity unless the fit crops, and a dead option would only fragment its cache.
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(
            harness.CreateImage(focalLeft: 0.3m, focalTop: 0.8m), RuleSet(cropMode: cropMode), 800, 600);

        Assert.DoesNotContain("gravity=", url);
    }

    // ---- quality / format -----------------------------------------------------------------------

    [Fact]
    public void ZeroQualityIsOmitted()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(quality: 0), 800, 0);

        Assert.DoesNotContain("quality=", url);
    }

    [Fact]
    public void FormatDefaultsToAuto()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        Assert.Contains("format=auto", provider.GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0));
    }

    [Fact]
    public void UseWebPReachesHereAsAPerCallFormatAndWins()
    {
        // This is how UseWebP arrives - SrcSetManager appends format=webp to the query string.
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0, "format=webp");

        Assert.Contains("format=webp", url);
        Assert.DoesNotContain("format=auto", url);
    }

    [Fact]
    public void FormatNoneOmitsTheOption()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness, new CloudflareImageSettings { Format = "none" });

        Assert.DoesNotContain("format=", provider.GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0));
    }

    [Fact]
    public void MetadataIsOmittedUnlessConfigured()
    {
        var harness = new RenderHarness(MediaUrl);

        Assert.DoesNotContain("metadata=", Build(harness).GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0));
        Assert.Contains("metadata=none",
            Build(harness, new CloudflareImageSettings { Metadata = "none" })
                .GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0));
    }

    // ---- caller-supplied query string ------------------------------------------------------------

    [Fact]
    public void UnknownQueryStringKeysAreCarriedOnTheSourceSoTheyReachTheOrigin()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0, "bgcolor=fff&rnd=133");

        Assert.EndsWith("/media/test/image.jpg?bgcolor=fff&rnd=133", url);
    }

    [Fact]
    public void QueryStringCannotOverrideTheSizeTheLadderAskedFor()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0, "width=99&mode=pad&rxy=0.1,0.1");

        Assert.Contains("width=800", url);
        Assert.DoesNotContain("width=99", url);
        Assert.DoesNotContain("mode=", url);
        Assert.DoesNotContain("rxy=", url);
    }

    [Fact]
    public void QualityInTheQueryStringWins()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(quality: 70), 800, 0, "quality=35");

        Assert.Contains("quality=35", url);
        Assert.DoesNotContain("quality=70", url);
    }

    // ---- settings --------------------------------------------------------------------------------

    [Fact]
    public void BaseUrlMakesUrlsAbsolute()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness, new CloudflareImageSettings { BaseUrl = "https://images.example.com/" });

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0);

        Assert.StartsWith("https://images.example.com/cdn-cgi/image/width=800,", url);
        Assert.EndsWith("/media/test/image.jpg", url);
    }

    [Fact]
    public void PrefixCanBeOverriddenForAWorkerRoute()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness, new CloudflareImageSettings { Prefix = "images/resize" });

        Assert.StartsWith("/images/resize/width=800,", provider.GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0));
    }

    [Fact]
    public void AnAbsoluteMediaUrlIsUsedAsTheSourceVerbatim()
    {
        // An external media file system (blob storage, another CDN) returns an absolute URL, which
        // Cloudflare accepts as a source.
        var harness = new RenderHarness("https://cdn.example.com/media/test/image.jpg");
        var provider = Build(harness);

        var url = provider.GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0);

        Assert.EndsWith("/https://cdn.example.com/media/test/image.jpg", url);
    }

    // ---- placeholder / purge ---------------------------------------------------------------------

    [Fact]
    public void PlaceholderIsTinyBlurredAndUncropped()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        var url = provider.GetPlaceholderUrl(harness.CreateImage(focalLeft: 0.3m, focalTop: 0.8m), RuleSet());

        Assert.Equal("/cdn-cgi/image/width=40,quality=20,blur=64,format=auto,onerror=redirect/media/test/image.jpg", url);
    }

    [Fact]
    public void PurgeUrlsUseTheSameFormatAsRenderedUrls()
    {
        // If these two disagree on format, purging silently matches nothing at the edge.
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);
        var ruleSet = RuleSet();

        var rendered = provider.GetCropUrl(harness.CreateImage(), ruleSet, 800, 600);
        var purge = provider.GetCropUrlForPath(MediaUrl, ruleSet, 800, 600);

        Assert.Equal(rendered, purge);
    }

    // ---- cache buster ----------------------------------------------------------------------------

    [Fact]
    public void SourceCarriesTheMediaCacheBusterAsUmbracoCropUrlsDo()
    {
        // Without this, replacing a file in place at the same path leaves Cloudflare serving the old image.
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);
        var image = harness.CreateImage(updateDate: new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc));

        var url = provider.GetCropUrl(image, RuleSet(), 800, 0);

        Assert.EndsWith("/media/test/image.jpg?v=" + new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc)
            .ToFileTimeUtc().ToString("x", System.Globalization.CultureInfo.InvariantCulture), url);
    }

    [Fact]
    public void CacheBusterCanBeTurnedOff()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness, new CloudflareImageSettings { CacheBuster = false });
        var image = harness.CreateImage(updateDate: new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc));

        Assert.DoesNotContain("v=", provider.GetCropUrl(image, RuleSet(), 800, 0));
    }

    [Fact]
    public void CacheBusterAndAPassThroughQueryStringCoexist()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);
        var image = harness.CreateImage(updateDate: new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc));

        var url = provider.GetCropUrl(image, RuleSet(), 800, 0, "bgcolor=fff");

        Assert.Contains("?v=", url);
        Assert.EndsWith("&bgcolor=fff", url);
    }

    [Fact]
    public void AMediaItemWithNoUpdateDateGetsNoCacheBuster()
    {
        // ToFileTimeUtc throws below the FileTime epoch, so this must not take the page down.
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        Assert.EndsWith("/media/test/image.jpg", provider.GetCropUrl(harness.CreateImage(), RuleSet(), 800, 0));
    }

    [Fact]
    public void NullImageYieldsNoUrl()
    {
        var harness = new RenderHarness(MediaUrl);
        var provider = Build(harness);

        Assert.Null(provider.GetCropUrl(null, RuleSet(), 800, 0));
        Assert.Null(provider.GetPlaceholderUrl(null, RuleSet()));
        Assert.Null(provider.GetCropUrlForPath(null, RuleSet(), 800, 0));
    }

    // ---- end to end through the renderers --------------------------------------------------------
    //
    // These are the tests that matter most: they prove the seam is complete. Any renderer still calling
    // Umbraco's GetCropUrl directly would show up here as a "/media/test/image.jpg?width=..." URL in
    // markup that is supposed to be entirely Cloudflare-shaped.

    private const string UmbracoStyleUrl = MediaUrl + "?";

    [Fact]
    public void PictureMarkupIsEntirelyCloudflareUrls()
    {
        var harness = CloudflareHarness();
        var html = harness.SrcSetManager
            .CreatePictureElement(harness.CreateImage(focalLeft: 0.3m, focalTop: 0.8m), "defaultset", imageAlt: "a")!
            .ToString();

        Assert.Contains("/cdn-cgi/image/width=1200,", html);
        Assert.Contains("gravity=0.3x0.8", html);
        Assert.DoesNotContain(UmbracoStyleUrl, html);
        // Structure is untouched: one source per breakpoint plus the synthetic one.
        Assert.Equal(4, Count(html, "<source "));
    }

    [Fact]
    public void ImgMarkupIsEntirelyCloudflareUrlsAndKeepsWidthDescriptors()
    {
        var harness = CloudflareHarness();
        var html = harness.SrcSetManager.CreateMarkup(harness.CreateImage(), "defaultset", alt: "a")!.ToString();

        Assert.Contains("/cdn-cgi/image/width=576,", html);
        Assert.Contains("/media/test/image.jpg 576w", html);
        Assert.Contains("/media/test/image.jpg 1200w", html);
        Assert.DoesNotContain(UmbracoStyleUrl, html);
    }

    [Fact]
    public void BackgroundCssIsEntirelyCloudflareUrlsIncludingTheDpiVariants()
    {
        // The 2x/3x background URLs used to be built by calling Umbraco's GetCropUrl directly, bypassing
        // the one place crop concerns are applied - so they are the likeliest thing to regress here.
        var harness = CloudflareHarness(RenderHarness.DefaultRuleSet(use2x: true, use3x: true));
        var css = harness.SrcSetManager.GetBreakPointsCss(harness.CreateImage(), "defaultset")!.ToString();

        Assert.Contains("min-resolution: 1.25dppx", css);
        Assert.Contains("min-resolution: 2.25dppx", css);
        Assert.Contains("/cdn-cgi/image/width=2400,", css);   // 1200 at 2x
        Assert.Contains("/cdn-cgi/image/width=3600,", css);   // 1200 at 3x
        Assert.DoesNotContain(UmbracoStyleUrl, css);
    }

    [Fact]
    public void DpiVariantsCarryTheFocalPointToo()
    {
        var harness = CloudflareHarness(RenderHarness.DefaultRuleSet(use2x: true));
        var css = harness.SrcSetManager
            .GetBreakPointsCss(harness.CreateImage(focalLeft: 0.3m, focalTop: 0.8m), "defaultset")!.ToString();

        Assert.Contains("width=2400,fit=cover,gravity=0.3x0.8", css);
    }

    [Fact]
    public void PreloadLinksAreCloudflareUrls()
    {
        var harness = CloudflareHarness();
        var image = harness.CreateImage();

        var picture = harness.SrcSetManager.GetPicturePreloadLinks(image, "defaultset")!.ToString();
        var img = harness.SrcSetManager.GetImagePreloadLink(image, "defaultset")!.ToString();

        Assert.Contains("/cdn-cgi/image/", picture);
        Assert.Contains("/cdn-cgi/image/", img);
        Assert.DoesNotContain(UmbracoStyleUrl, picture);
        Assert.DoesNotContain(UmbracoStyleUrl, img);
    }

    [Fact]
    public void BlurPlaceholderFallsBackToACloudflareUrlWhenTheInlineOneCannotBeBuilt()
    {
        // The inline base64 placeholder is unaffected by the provider - it decodes the media file itself.
        // Its fallback, for a file that cannot be read, is a URL, and that must be Cloudflare-shaped too.
        var harness = CloudflareHarness(previewType: PreviewType.Blur);
        var html = harness.SrcSetManager.CreateMarkup(harness.CreateImage(), "defaultset", alt: "a")!.ToString();

        Assert.Contains("/cdn-cgi/image/width=40,quality=20,blur=64,", html);
        Assert.DoesNotContain(UmbracoStyleUrl, html);
    }

    [Fact]
    public void InlineBase64PlaceholderStillWinsOverAnyUrl()
    {
        var harness = CloudflareHarness(
            previewType: PreviewType.Blur, lqipService: new FakeLqipService("data:image/webp;base64,AAAA"));
        var html = harness.SrcSetManager.CreateMarkup(harness.CreateImage(), "defaultset", alt: "a")!.ToString();

        Assert.Contains("background-image:url('data:image/webp;base64,AAAA')", html);
        Assert.DoesNotContain("blur=64", html);
    }

    [Fact]
    public void SvgsBypassCloudflareEntirely()
    {
        // Cloudflare cannot transform an SVG, and there is nothing to resize anyway.
        var harness = CloudflareHarness(mediaUrl: "/media/test/logo.svg");
        var html = harness.SrcSetManager.CreatePictureElement(harness.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.DoesNotContain("/cdn-cgi/image/", html);
        Assert.Contains("src=\"/media/test/logo.svg\"", html);
    }

    // ---- helpers ---------------------------------------------------------------------------------

    private static int Count(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) != -1) { count++; i += needle.Length; }
        return count;
    }

    /// <summary>A full render harness wired to the Cloudflare provider instead of Umbraco's.</summary>
    private static RenderHarness CloudflareHarness(
        RuleSet? ruleSet = null,
        PreviewType previewType = PreviewType.LowResImage,
        ILqipService? lqipService = null,
        string mediaUrl = MediaUrl)
    {
        var provider = new CloudflareImageUrlProvider(
            new StaticOptionsMonitor<CloudflareImageSettings>(new CloudflareImageSettings()),
            StubPublishedUrlProvider(mediaUrl));

        return new RenderHarness(
            mediaUrl,
            previewType: previewType,
            ruleSet: ruleSet,
            lqipService: lqipService,
            urlProviderOverride: provider);
    }

    private static IPublishedUrlProvider StubPublishedUrlProvider(string mediaUrl)
    {
        var mock = new Mock<IPublishedUrlProvider>();
        mock.Setup(x => x.GetMediaUrl(
                It.IsAny<IPublishedContent>(), It.IsAny<UrlMode>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<Uri?>()))
            .Returns(mediaUrl);
        return mock.Object;
    }

    private static CloudflareImageUrlProvider Build(RenderHarness harness, CloudflareImageSettings? settings = null)
        => new CloudflareImageUrlProvider(
            new StaticOptionsMonitor<CloudflareImageSettings>(settings ?? new CloudflareImageSettings()),
            harness.UrlProvider);

    private static RuleSet RuleSet(
        ImageCropMode cropMode = ImageCropMode.Crop,
        int quality = 70,
        bool useFocalPoint = true)
        => new RuleSet("cf")
        {
            CropMode = cropMode,
            ImageQuality = quality,
            UseFocalPoint = useFocalPoint
        };
}

/// <summary>
/// An <see cref="IOptionsMonitor{T}"/> over a fixed value, for services that read settings through the
/// monitor so they respond to configuration reloads.
/// </summary>
internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    public StaticOptionsMonitor(T value) => CurrentValue = value;

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string?> listener) => new NoopDisposable();

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
