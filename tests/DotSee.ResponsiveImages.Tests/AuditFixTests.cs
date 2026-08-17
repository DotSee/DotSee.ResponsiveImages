using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotSee.ResponsiveImages.Caching;
using DotSee.ResponsiveImages.Cdn;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using DotSee.ResponsiveImages.UrlProviders;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Xunit;

namespace DotSee.ResponsiveImages.Tests;

/// <summary>
/// Regression tests for the whole-project audit: output encoding, nonce validation, candidate-ladder
/// arithmetic, unknown-rule-set handling, cache-key collisions, cache-service concurrency, and the CDN
/// purge URL builder.
/// </summary>
public class AuditFixTests
{
    // ---- output encoding (stored XSS) --------------------------------------------------------------

    [Fact]
    public void HostileAltTextCannotBreakOutOfItsAttribute()
    {
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreatePictureElement(
            h.CreateImage(), "defaultset", imageAlt: "\" onerror=\"alert(1)")!.ToString();

        Assert.DoesNotContain("onerror=\"alert", html);
        Assert.Contains("alt=\"&quot; onerror=&quot;alert(1)\"", html);
    }

    [Fact]
    public void HostileAttributeDictionaryValuesAreEncoded()
    {
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreateMarkup(
            h.CreateImage(), "defaultset", alt: "a",
            otherAttributes: new Dictionary<string, string> { ["data-x"] = "\"><script>alert(1)</script>" })!.ToString();

        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void HostileAttributeNamesAreStrippedToLegalCharacters()
    {
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreateMarkup(
            h.CreateImage(), "defaultset", alt: "a",
            otherAttributes: new Dictionary<string, string> { ["x\" onload=\"alert(1)"] = "y" })!.ToString();

        Assert.DoesNotContain("onload=\"alert", html);
    }

    [Fact]
    public void UnicodeAltTextStaysReadable()
    {
        // The encoder allows the full Unicode range, so Greek alt text is not entity-bloated.
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreateMarkup(h.CreateImage(), "defaultset", alt: "Καρέκλα στον κήπο")!.ToString();

        Assert.Contains("alt=\"Καρέκλα στον κήπο\"", html);
    }

    // ---- nonce validation ---------------------------------------------------------------------------

    [Theory]
    [InlineData("abc123+/=_-", true)]
    [InlineData("x'><script>", false)]
    [InlineData("nonce=\"abc\"", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void NonceValidationAcceptsOnlyBase64Alphabet(string? nonce, bool expected)
    {
        Assert.Equal(expected, Helpers.IsValidNonce(nonce));
    }

    [Fact]
    public void AnInvalidNonceIsDroppedFromTheBackgroundCss()
    {
        var h = new RenderHarness();
        var css = h.SrcSetManager.GetBreakPointsCss(
            h.CreateImage(), "defaultset", nonceAttribute: new HtmlString("x'><script>alert(1)</script>"))!.ToString();

        Assert.DoesNotContain("script>", css.Substring(css.IndexOf('>')));
        Assert.DoesNotContain("nonce=", css);
    }

    [Fact]
    public void AValidNonceIsInjectedIntoTheBackgroundCss()
    {
        var h = new RenderHarness();
        var css = h.SrcSetManager.GetBreakPointsCss(
            h.CreateImage(), "defaultset", nonceAttribute: new HtmlString("abc123"))!.ToString();

        Assert.Contains("<style nonce='abc123'", css);
    }

    [Fact]
    public void AHostileSrcSetAttributeNameFallsBackToSrcset()
    {
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreateMarkup(
            h.CreateImage(), "defaultset", alt: "a", srcSetAttrName: "data-x\" onload=\"alert(1)")!.ToString();

        Assert.DoesNotContain("alert(1)", html);
        Assert.Contains(" srcset=\"", html);
    }

    // ---- candidate ladder ---------------------------------------------------------------------------

    [Fact]
    public void PictureSourcesResolveBreakpointsWithNoExplicitWidth()
    {
        // ScaledWidth used to return 0 for Width-less breakpoints, which made every <source> ask for
        // the uncropped original.
        var rs = new RuleSet("flagset")
        {
            UseBreakPointWidthIfNoWidth = true,
            OriginalImageMaxWidth = 1920
        };
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 1200 });
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 768 });

        var sources = CandidateLadder.GetPictureSources(rs);

        Assert.All(sources, s => Assert.True(s.Width > 0, $"breakpoint {s.BreakPoint.BreakPointWidth} resolved to width 0"));
        Assert.Equal(1200, sources.First(s => s.BreakPoint.BreakPointWidth == 1200).Width);
    }

    [Fact]
    public void TheSyntheticBreakpointCopiesTheResolvedWidthNotTheRawZero()
    {
        var rs = new RuleSet("flagset") { UseBreakPointWidthIfNoWidth = true };
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 576 });

        var ordered = CandidateLadder.GetOrderedBreakPoints(rs);
        var synthetic = ordered.Last();

        Assert.Equal(1, synthetic.BreakPointWidth);
        Assert.Equal(576, synthetic.Width);
    }

    [Fact]
    public void DpiCandidatesClampToTheMaxWidthAndCollapseOntoOneX()
    {
        var rs = new RuleSet("capped") { Use2x = true, Use3x = true, OriginalImageMaxWidth = 1200 };
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 1200, Width = 1200 });

        var source = CandidateLadder.GetPictureSources(rs).First(s => s.BreakPoint.BreakPointWidth == 1200);

        // 2x/3x would be 2400/3600, above the 1200 source ceiling; they clamp and collapse onto 1x.
        Assert.Single(source.Candidates);
        Assert.Equal(1200, source.Candidates[0].Width);
    }

    [Fact]
    public void PictureHonoursAnExplicitBreakpointHeight()
    {
        // A 400x400 square crop used to render as 400x266 in <picture> (the rule-set maximums' ratio)
        // while <img> honoured it.
        var rs = new RuleSet("square") { OriginalImageMaxWidth = 1200, OriginalImageMaxHeight = 800 };
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 576, Width = 400, Height = 400 });

        var source = CandidateLadder.GetPictureSources(rs).First(s => s.BreakPoint.BreakPointWidth == 576);

        Assert.Equal(400, source.Width);
        Assert.Equal(400, source.Height);
    }

    [Fact]
    public void AHeightClampKeepsTheRequestedWidth()
    {
        // maxHeight 500 on a 1000x1000 breakpoint: the height clamps and the width stays as requested.
        // With a cropping mode the processor delivers exactly the clamped WxH, so 1000x500 is both the
        // request and the delivered box; recomputing the width from the rule-set maximums' 8:1 ratio
        // would have ENLARGED it to 4000.
        var rs = new RuleSet("clamped") { OriginalImageMaxWidth = 4000, OriginalImageMaxHeight = 500 };
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 1000, Width = 1000, Height = 1000 });

        var candidate = CandidateLadder.GetSrcSetCandidates(rs).Single();

        Assert.Equal(500, candidate.Height);
        Assert.Equal(1000, candidate.Width);
    }

    [Fact]
    public void RenderingABackgroundDoesNotMutateTheSharedRuleSet()
    {
        var rs = RenderHarness.DefaultRuleSet();
        int breakpointsBefore = rs.Breakpoints.Count;

        var h = new RenderHarness(ruleSet: rs);
        _ = h.SrcSetManager.GetBreakPointsCss(h.CreateImage(), "defaultset");

        Assert.Equal(breakpointsBefore, rs.Breakpoints.Count);
    }

    // ---- unknown rule set ---------------------------------------------------------------------------

    [Fact]
    public void AnUnknownRuleSetRendersNothingInsteadOfThrowing()
    {
        var h = new RenderHarness();
        var image = h.CreateImage();

        Assert.Null(h.SrcSetManager.CreateMarkup(image, "no-such-rule-set", alt: "a"));
        Assert.Null(h.SrcSetManager.CreatePictureElement(image, "no-such-rule-set", imageAlt: "a"));
        Assert.Null(h.SrcSetManager.GetSrcSet(image, "no-such-rule-set"));
        Assert.Null(h.SrcSetManager.GetBreakPointsCss(image, "no-such-rule-set"));
        Assert.Null(h.SrcSetManager.GetSizes(image, null));
    }

    [Fact]
    public void ANullImageRendersNothingFromEveryEntryPoint()
    {
        var h = new RenderHarness();

        Assert.Null(h.SrcSetManager.CreatePictureElement(null, "defaultset", imageAlt: "a"));
        Assert.Null(h.SrcSetManager.CreateMarkup(null, "defaultset", alt: "a"));
        Assert.Null(h.SrcSetManager.GetSrcSet(null, "defaultset"));
    }

    // ---- markup shape -------------------------------------------------------------------------------

    [Fact]
    public void CreateMarkupRendersAnSvgAsAPlainImg()
    {
        var h = new RenderHarness("/media/test/logo.svg");
        var html = h.SrcSetManager.CreateMarkup(h.CreateImage(), "defaultset", alt: "a")!.ToString();

        Assert.Contains("src=\"/media/test/logo.svg\"", html);
        Assert.DoesNotContain("srcset", html);
        Assert.DoesNotContain("?width=", html);
    }

    [Fact]
    public void CreateMarkupEmitsOnlyOneClassAttribute()
    {
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreateMarkup(
            h.CreateImage(), "defaultset", alt: "a", imageClass: "a",
            otherAttributes: new Dictionary<string, string> { ["class"] = "b" })!.ToString();

        Assert.Equal(1, CountOccurrences(html, "class=\""));
    }

    [Fact]
    public void PictureKeepsACallerClassWhenNoImageClassWasRendered()
    {
        // The old filter dropped a dictionary "class" whenever imageClass was non-null - and its
        // default is "", so a caller's class was silently discarded.
        var h = new RenderHarness();
        var html = h.SrcSetManager.CreatePictureElement(
            h.CreateImage(), "defaultset", imageAlt: "a",
            imageAttributes: new Dictionary<string, string> { ["class"] = "rounded" })!.ToString();

        Assert.Contains("class=\"rounded\"", html);
    }

    [Fact]
    public void UppercaseSvgExtensionsTakeTheSvgPath()
    {
        var h = new RenderHarness("/media/test/logo.SVG");
        var html = h.SrcSetManager.CreatePictureElement(h.CreateImage(), "defaultset", imageAlt: "a")!.ToString();

        Assert.DoesNotContain("<picture>", html);
        Assert.Contains("src=\"/media/test/logo.SVG\"", html);
    }

    [Fact]
    public void BackgroundCssDummyRuleDoesNotFetchTheDocument()
    {
        // The Cloudflare provider is the URL-assertable path in tests (the Umbraco GetCropUrl extension
        // returns empty against the mocked IPublishedContent), and every breakpoint URL being non-empty
        // is what proves the remaining url('') would have been the dummy rule.
        var h = CloudflareHarness();
        var css = h.SrcSetManager.GetBreakPointsCss(h.CreateImage(), "defaultset")!.ToString();

        Assert.DoesNotContain("url('')", css);
        Assert.Contains("background-image:none", css);
    }

    [Fact]
    public void UseWebPReachesCreateMarkupUrls()
    {
        // UseWebP was documented as applying to "all generated URLs" but never reached CreateMarkup.
        var h = CloudflareHarness(useWebP: true);
        var html = h.SrcSetManager.CreateMarkup(h.CreateImage(), "defaultset", alt: "a")!.ToString();

        Assert.Contains("format=webp", html);
        Assert.DoesNotContain("format=auto", html);
    }

    // ---- cache keys ---------------------------------------------------------------------------------

    [Fact]
    public void UnderscoresInAltAndClassDoNotCollideInTheCache()
    {
        // "Spring_Sale" + "" used to build the identical key as "Spring" + "Sale", so one element's
        // cached markup was served for the other.
        var h = new RenderHarness();
        var image = h.CreateImage();

        var first = h.SrcSetManager.CreatePictureElement(image, "defaultset", imageAlt: "Spring_Sale", imageClass: "")!.ToString();
        var second = h.SrcSetManager.CreatePictureElement(image, "defaultset", imageAlt: "Spring", imageClass: "Sale")!.ToString();

        Assert.Contains("alt=\"Spring_Sale\"", first);
        Assert.Contains("alt=\"Spring\"", second);
    }

    [Fact]
    public void DifferentQueryStringsGetDifferentBackgroundCssAndClassNames()
    {
        // Two <ds:background> for the same image with different query strings used to share one cache
        // entry AND one class name - whichever rendered first won for both.
        var h = CloudflareHarness();
        var image = h.CreateImage();

        var plain = h.SrcSetManager.GetBreakPointsCss(image, "defaultset")!.ToString();
        var tinted = h.SrcSetManager.GetBreakPointsCss(image, "defaultset", "bgcolor=fff")!.ToString();

        Assert.Contains("bgcolor=fff", tinted);
        Assert.DoesNotContain("bgcolor=fff", plain);

        var plainClass = h.SrcSetManager.GetClassName(image, "defaultset");
        var tintedClass = h.SrcSetManager.GetClassName(image, "defaultset", "bgcolor=fff");
        Assert.NotEqual(plainClass, tintedClass);

        // Each CSS block targets its own class name.
        Assert.Contains(tintedClass!.Replace("media-image-", string.Empty), tinted);
    }

    [Fact]
    public void ClassNamesAreUnchangedWhenNoQueryStringOrFocalPointApplies()
    {
        // The common case keeps the historical class shape, so existing sites' markup is untouched.
        var h = new RenderHarness();
        var className = h.SrcSetManager.GetClassName(h.CreateImage(), "defaultset");

        Assert.Equal("media-image-RSFor_defaultset_11111111111111111111111111111111", className);
    }

    // ---- CacheService -------------------------------------------------------------------------------

    [Fact]
    public void ConcurrentMissesRunTheFactoryOnce()
    {
        var cache = new CacheService(new MemoryCache(new MemoryCacheOptions()));
        int calls = 0;

        Parallel.For(0, 32, _ =>
            cache.GetCachedItem("stampede", () =>
            {
                Interlocked.Increment(ref calls);
                Thread.Sleep(25);
                return "value";
            }));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void NullResultsExpireOnTheirOwnShorterClock()
    {
        var cache = new CacheService(new MemoryCache(new MemoryCacheOptions()));
        int calls = 0;
        string? Factory() { calls++; return null; }

        Assert.Null(cache.GetCachedItem("negative", Factory, timeout: TimeSpan.FromMinutes(20), isSliding: true, nullResultTimeout: TimeSpan.FromMilliseconds(50)));
        Assert.Null(cache.GetCachedItem("negative", Factory, timeout: TimeSpan.FromMinutes(20), isSliding: true, nullResultTimeout: TimeSpan.FromMilliseconds(50)));
        Assert.Equal(1, calls);

        Thread.Sleep(150);

        Assert.Null(cache.GetCachedItem("negative", Factory, timeout: TimeSpan.FromMinutes(20), isSliding: true, nullResultTimeout: TimeSpan.FromMilliseconds(50)));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ClearRacingReadsDoesNotThrow()
    {
        var cache = new CacheService(new MemoryCache(new MemoryCacheOptions()));

        var readers = Task.Run(() =>
        {
            for (int i = 0; i < 2000; i++) { cache.GetCachedItem("k" + (i % 10), () => i); }
        });
        var clearer = Task.Run(() =>
        {
            for (int i = 0; i < 200; i++) { cache.Clear(); }
        });

        Task.WaitAll(readers, clearer);
    }

    // ---- CDN purge URL builder ----------------------------------------------------------------------

    [Fact]
    public void PurgeSkipsUrlsOnAForeignHost()
    {
        // Blob-storage media resolves to absolute URLs; Uri.TryCreate ignores the base for those, and
        // Cloudflare rejects a purge batch containing another host's URLs outright.
        var h = new RenderHarness();
        var builder = new CdnPurgeUrlBuilder(
            new UmbracoImageUrlProvider(h.ImageUrlGenerator, h.UrlProvider),
            new ConfigSource(Options.Create(new List<RuleSet>())));

        var urls = builder.Build("https://acct.blob.core.windows.net/media/x.jpg", "https://www.example.com", 100);

        Assert.Empty(urls);
    }

    [Fact]
    public void PurgeBuildsAbsoluteUrlsForOwnHostMedia()
    {
        var h = new RenderHarness();
        var builder = new CdnPurgeUrlBuilder(
            new UmbracoImageUrlProvider(h.ImageUrlGenerator, h.UrlProvider),
            new ConfigSource(Options.Create(new List<RuleSet> { RenderHarness.DefaultRuleSet() })));

        var urls = builder.Build("/media/abc/x.jpg", "https://www.example.com", 100);

        Assert.NotEmpty(urls);
        Assert.All(urls, u => Assert.StartsWith("https://www.example.com/", u));
    }

    [Fact]
    public void PurgeRespectsThePerItemCap()
    {
        var h = new RenderHarness();
        var builder = new CdnPurgeUrlBuilder(
            new UmbracoImageUrlProvider(h.ImageUrlGenerator, h.UrlProvider),
            new ConfigSource(Options.Create(new List<RuleSet> { RenderHarness.DefaultRuleSet(use2x: true, use3x: true) })));

        var urls = builder.Build("/media/abc/x.jpg", "https://www.example.com", 3);

        Assert.True(urls.Count <= 3, $"expected at most 3 URLs, got {urls.Count}");
    }

    // ---- Cloudflare purge service -------------------------------------------------------------------

    [Fact]
    public async Task PurgeChunksIntoBatchesOfThirty()
    {
        var handler = new RecordingHandler();
        var service = new CloudflareCdnPurgeService(
            new SingleClientFactory(handler),
            new StaticOptionsMonitor<ImageCdnSettings>(new ImageCdnSettings { Enabled = true, ZoneId = "zone", ApiToken = "token" }),
            NullLogger<CloudflareCdnPurgeService>.Instance);

        var urls = Enumerable.Range(0, 65).Select(i => $"https://www.example.com/media/{i}.jpg").ToList();
        var result = await service.PurgeAsync(urls);

        Assert.True(result.Succeeded);
        Assert.Equal(3, handler.Requests.Count);   // 30 + 30 + 5
        Assert.All(handler.Requests, r => Assert.Contains("/zones/zone/purge_cache", r.RequestUri!.ToString()));
    }

    [Fact]
    public async Task NullPurgeServiceStaysInert()
    {
        var service = new NullCdnPurgeService(
            new StaticOptionsMonitor<ImageCdnSettings>(new ImageCdnSettings { Enabled = true }),
            NullLogger<NullCdnPurgeService>.Instance);

        Assert.False(service.IsEnabled);
        var result = await service.PurgeAsync(new[] { "https://www.example.com/x.jpg" });
        Assert.True(result.Succeeded);
        Assert.False(result.Attempted);
    }

    // ---- helpers ------------------------------------------------------------------------------------

    /// <summary>
    /// A render harness wired to the Cloudflare URL provider. The Cloudflare provider composes URLs
    /// itself, so it is the one path whose URL *contents* the mocked environment can assert on — the
    /// Umbraco GetCropUrl extension returns empty against the mocked IPublishedContent.
    /// </summary>
    private static RenderHarness CloudflareHarness(bool useWebP = false)
    {
        var urlProvider = new Mock<IPublishedUrlProvider>();
        urlProvider.Setup(x => x.GetMediaUrl(
                It.IsAny<IPublishedContent>(), It.IsAny<UrlMode>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<Uri?>()))
            .Returns("/media/test/image.jpg");

        var provider = new CloudflareImageUrlProvider(
            new StaticOptionsMonitor<CloudflareImageSettings>(new CloudflareImageSettings()),
            urlProvider.Object);

        return new RenderHarness(useWebP: useWebP, urlProviderOverride: provider);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) != -1) { count++; i += needle.Length; }
        return count;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true}")
            });
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public SingleClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new HttpClient(_handler, disposeHandler: false);
    }
}
