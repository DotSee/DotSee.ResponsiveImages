using DotSee.ResponsiveImages.Caching;
using DotSee.ResponsiveImages.Cdn;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using DotSee.ResponsiveImages.Preloading;
using DotSee.ResponsiveImages.UrlProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace DotSee.ResponsiveImages;

public class ResponsiveImagesServicesComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddTransient<SrcSetManager>();

        // Both settings live under DotSee:ResponsiveImages. See ResponsiveImagesConfiguration for the
        // layout, and for the original one (bare array + root-level "lazyload") that is still honoured.
        var globalLazySettings = new GlobalLazyLoadSettings();
        ResponsiveImagesConfiguration.GetLazyLoadSection(builder.Config).Bind(globalLazySettings);
        builder.Services.AddSingleton<IGlobalLazyLoadSettings>(globalLazySettings);
        builder.Services.AddTransient<BackgroundImageModelManager>();

        // read Responsive Images from appSettings
        builder.Services.Configure<List<RuleSet>>(
            ResponsiveImagesConfiguration.GetRuleSetsSection(builder.Config)
        );
        builder.Services.AddSingleton<IConfigSource, ConfigSource>();
        builder.Services.AddSingleton<IRuleProvider, ConfigFileJsonRuleProvider>();
        builder.Services.AddTransient<CssRenderer>();
        builder.Services.AddTransient<ImageUrlService>();
        builder.Services.AddTransient<PictureElementRenderer>();

        // Which backend builds image URLs. Bound unconditionally so the settings are always readable, with
        // the provider itself chosen at compose time - so, as with the CDN purge service, switching
        // provider needs a restart rather than taking effect on a config reload mid-request.
        builder.Services.Configure<CloudflareImageSettings>(
            ResponsiveImagesConfiguration.GetCloudflareSection(builder.Config));

        if (ResponsiveImagesConfiguration.GetUrlProvider(builder.Config)
                .InvariantEquals(ResponsiveImagesConfiguration.CloudflareUrlProvider))
        {
            builder.Services.AddSingleton<IResponsiveImageUrlProvider, CloudflareImageUrlProvider>();
        }
        else
        {
            builder.Services.AddSingleton<IResponsiveImageUrlProvider, UmbracoImageUrlProvider>();
        }

        // The cache service gets its own private MemoryCache rather than the application-wide one:
        // sharing the host's instance means a host-configured SizeLimit makes every size-less entry
        // throw, and this package's keys would sit alongside every other component's.
        builder.Services.AddSingleton<ICacheService>(
            _ => new CacheService(new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())));
        builder.Services.AddSingleton<ILqipService, LqipService>();
        builder.Services.AddScoped<IPreloadCollector, PreloadCollector>();

        // CDN purging. Bound unconditionally so the settings are readable, but the service is only the
        // real implementation when the section explicitly enables it - installing the package must never
        // start making outbound calls to someone's CDN.
        builder.Services.Configure<ImageCdnSettings>(builder.Config.GetSection("DotSee:ImageCdn"));
        // A short timeout: purging currently happens synchronously inside the media-save notification,
        // so the HttpClient default of 100 seconds per batch would let an unreachable CDN hang an
        // editor's save for minutes.
        builder.Services.AddHttpClient(CloudflareCdnPurgeService.HttpClientName,
            client => client.Timeout = System.TimeSpan.FromSeconds(10));
        builder.Services.AddTransient<CdnPurgeUrlBuilder>();

        var cdnSettings = new ImageCdnSettings();
        builder.Config.GetSection("DotSee:ImageCdn").Bind(cdnSettings);

        if (cdnSettings.Enabled && cdnSettings.Provider.InvariantEquals("Cloudflare"))
        {
            builder.Services.AddSingleton<ICdnPurgeService, CloudflareCdnPurgeService>();
        }
        else
        {
            // NullCdnPurgeService logs when a purge is attempted while the section says Enabled — an
            // unrecognised Provider value or a config change after startup would otherwise be
            // indistinguishable from purging deliberately switched off.
            builder.Services.AddSingleton<ICdnPurgeService, NullCdnPurgeService>();
        }

        // Invalidate cached image markup / CSS when content or media changes
        builder.AddNotificationHandler<ContentPublishedNotification, ResponsiveImagesCacheInvalidator>();
        builder.AddNotificationHandler<ContentUnpublishedNotification, ResponsiveImagesCacheInvalidator>();
        builder.AddNotificationHandler<ContentDeletedNotification, ResponsiveImagesCacheInvalidator>();
        builder.AddNotificationHandler<MediaSavedNotification, ResponsiveImagesCacheInvalidator>();
        builder.AddNotificationHandler<MediaDeletedNotification, ResponsiveImagesCacheInvalidator>();
        // The backoffice "delete" moves media to the recycle bin; MediaDeletedNotification only fires
        // when the bin is emptied.
        builder.AddNotificationHandler<MediaMovedToRecycleBinNotification, ResponsiveImagesCacheInvalidator>();
        // Cache refreshers are the notifications that reach every node in a load-balanced setup; the
        // service-level ones above fire only where the operation happened.
        builder.AddNotificationHandler<ContentCacheRefresherNotification, ResponsiveImagesCacheInvalidator>();
        builder.AddNotificationHandler<MediaCacheRefresherNotification, ResponsiveImagesCacheInvalidator>();
    }
}