using System;
using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.Caching;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Routing;

namespace DotSee.ResponsiveImages.Tests;

/// <summary>
/// Builds a real <see cref="SrcSetManager"/> (and the renderers behind it) wired to stubbed Umbraco
/// boundary services, so the actual rendering logic runs against deterministic image URLs.
///
/// The <see cref="IImageUrlGenerator"/> stub echoes the crop options back into the URL query string
/// (e.g. <c>?width=1200&amp;quality=70&amp;format=webp</c>), which lets tests assert on sizing and the
/// query-string / WebP behaviour without depending on Umbraco's internal URL formatting.
/// </summary>
public sealed class RenderHarness
{
    public SrcSetManager SrcSetManager { get; }
    public GlobalLazyLoadSettings Lazy { get; }
    public string ImageUrl { get; }
    public IPublishedValueFallback Fallback { get; }
    public RuleSet RuleSet { get; }
    public Mock<IImageUrlGenerator> ImageUrlGeneratorMock { get; }
    public Mock<IPublishedUrlProvider> UrlProviderMock { get; }
    public IConfigSource ConfigSource { get; }
    public IImageUrlGenerator ImageUrlGenerator { get; }
    public IPublishedUrlProvider UrlProvider { get; }

    public RenderHarness(
        string imageUrl = "/media/test/image.jpg",
        bool useWebP = false,
        bool? enableLazyLoad = true,
        PreviewType previewType = PreviewType.LowResImage,
        string lowResPath = "/img/lowres.jpg",
        RuleSet? ruleSet = null)
    {
        ImageUrl = imageUrl;
        RuleSet = ruleSet ?? DefaultRuleSet();

        var imageUrlGenerator = new Mock<IImageUrlGenerator>();
        imageUrlGenerator.SetupGet(x => x.SupportedImageFileTypes)
            .Returns(new[] { "jpg", "jpeg", "png", "gif", "webp" });
        imageUrlGenerator.Setup(x => x.GetImageUrl(It.IsAny<ImageUrlGenerationOptions>()))
            .Returns((ImageUrlGenerationOptions o) => BuildUrl(o));

        // Return the URL stored on the content itself (its Name), not a captured harness field.
        // FriendlyPublishedContentExtensions caches the provider from StaticServiceProvider at first use,
        // so a content-driven lookup keeps working across tests regardless of which harness cached it.
        var urlProvider = new Mock<IPublishedUrlProvider>();
        urlProvider.Setup(x => x.GetMediaUrl(
                It.IsAny<IPublishedContent>(), It.IsAny<UrlMode>(), It.IsAny<string?>(),
                It.IsAny<string>(), It.IsAny<Uri?>()))
            .Returns((IPublishedContent c, UrlMode m, string? cu, string alias, Uri? cur) => c.Name ?? string.Empty);
        urlProvider.Setup(x => x.GetUrl(
                It.IsAny<IPublishedContent>(), It.IsAny<UrlMode>(), It.IsAny<string?>(), It.IsAny<Uri?>()))
            .Returns((IPublishedContent c, UrlMode m, string? cu, Uri? cur) => c.Name ?? string.Empty);

        ImageUrlGeneratorMock = imageUrlGenerator;
        UrlProviderMock = urlProvider;
        Fallback = new Mock<IPublishedValueFallback>().Object;

        // Argless .Url() and GetCropUrl(w,h) resolve services from StaticServiceProvider. Umbraco's
        // FriendlyPublishedContentExtensions static ctor eagerly resolves a large graph, so we back it
        // with an auto-mocking provider (returns a Mock for any interface) and supply our real URL stubs.
        StaticServiceProvider.Instance = new AutoMockServiceProvider(new Dictionary<Type, object>
        {
            [typeof(IImageUrlGenerator)] = imageUrlGenerator.Object,
            [typeof(IPublishedUrlProvider)] = urlProvider.Object,
            [typeof(IPublishedValueFallback)] = Fallback
        });

        ImageUrlGenerator = imageUrlGenerator.Object;
        UrlProvider = urlProvider.Object;
        ConfigSource = new ConfigSource(Options.Create(new List<RuleSet> { RuleSet }));
        var ruleProvider = new ConfigFileJsonRuleProvider(NullLogger<ConfigFileJsonRuleProvider>.Instance, ConfigSource);
        var imageUrlService = new ImageUrlService(imageUrlGenerator.Object, urlProvider.Object);
        var cssRenderer = new CssRenderer(imageUrlService, imageUrlGenerator.Object, urlProvider.Object);
        Lazy = new GlobalLazyLoadSettings
        {
            EnablelazyLoad = enableLazyLoad,
            PreviewType = previewType,
            LowResImagePath = lowResPath
        };
        var cache = new CacheService(new MemoryCache(new MemoryCacheOptions()));
        var pictureRenderer = new PictureElementRenderer(
            imageUrlService, ruleProvider, imageUrlGenerator.Object, urlProvider.Object, Lazy, cache);
        var bgManager = new BackgroundImageModelManager(ruleProvider, Lazy, imageUrlService, cache);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["useWebP"] = useWebP ? "true" : "false" })
            .Build();

        SrcSetManager = new SrcSetManager(
            ruleProvider, imageUrlGenerator.Object, urlProvider.Object, imageUrlService,
            cssRenderer, pictureRenderer, bgManager, Lazy, cache, config);
    }

    private static string BuildUrl(ImageUrlGenerationOptions o)
    {
        var qs = new List<string>();
        if (o.Width.HasValue) qs.Add($"width={o.Width}");
        if (o.Height.HasValue) qs.Add($"height={o.Height}");
        if (o.Quality.HasValue) qs.Add($"quality={o.Quality}");
        if (!string.IsNullOrEmpty(o.FurtherOptions))
        {
            qs.Add(o.FurtherOptions.TrimStart('&', '?'));
        }

        return qs.Count > 0 ? $"{o.ImageUrl}?{string.Join("&", qs)}" : o.ImageUrl ?? string.Empty;
    }

    public static RuleSet DefaultRuleSet(bool use2x = false, bool use3x = false, bool? lazyLoad = true)
    {
        var rs = new RuleSet("defaultset")
        {
            ImageQuality = 70,
            OriginalImageMaxWidth = 1920,
            CropMode = ImageCropMode.Crop,
            LazyLoad = lazyLoad,
            Use2x = use2x,
            Use3x = use3x
        };
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 1200, Width = 1200, Height = 0 });
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 768, Width = 768, Height = 0 });
        rs.Breakpoints.Add(new RuleBreakPoint { BreakPointWidth = 576, Width = 576, Height = 0 });
        rs.Sizes.Add("(max-width: 576px) 100vw");
        rs.Sizes.Add("50vw");
        return rs;
    }

    public MediaWithCrops CreateImage(Guid? key = null, int id = 1234, decimal? focalLeft = null, decimal? focalTop = null)
    {
        var content = new Mock<IPublishedContent>();
        content.SetupGet(x => x.Key).Returns(key ?? Guid.Parse("11111111-1111-1111-1111-111111111111"));
        content.SetupGet(x => x.Id).Returns(id);
        content.SetupGet(x => x.Name).Returns(ImageUrl); // URL providers echo this back (see harness ctor)
        // .Url() switches on ContentType.ItemType; Content routes to GetUrl (stubbed). Both GetUrl and
        // GetMediaUrl return the same test URL, so this only affects which stubbed method is called.
        content.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);
        var contentType = new Mock<IPublishedContentType>();
        contentType.SetupGet(x => x.Alias).Returns("Image");
        contentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);
        content.SetupGet(x => x.ContentType).Returns(contentType.Object);

        var crops = new ImageCropperValue { Src = ImageUrl };
        if (focalLeft.HasValue || focalTop.HasValue)
        {
            crops.FocalPoint = new ImageCropperValue.ImageCropperFocalPoint
            {
                Left = focalLeft ?? 0.5m,
                Top = focalTop ?? 0.5m
            };
        }

        return new MediaWithCrops(content.Object, Fallback, crops);
    }
}

/// <summary>
/// An <see cref="IServiceProvider"/> that returns supplied instances for known types and lazily
/// creates a Moq mock for any other interface/abstract type requested. Lets Umbraco's friendly
/// extension static initializers resolve their large dependency graph without registering every type.
/// </summary>
internal sealed class AutoMockServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services;

    public AutoMockServiceProvider(Dictionary<Type, object> services) => _services = services;

    public object? GetService(Type serviceType)
    {
        if (_services.TryGetValue(serviceType, out var existing))
        {
            return existing;
        }

        if (!serviceType.IsInterface && !serviceType.IsAbstract)
        {
            return null;
        }

        var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(serviceType))!;
        var obj = mock.Object;
        _services[serviceType] = obj;
        return obj;
    }
}
