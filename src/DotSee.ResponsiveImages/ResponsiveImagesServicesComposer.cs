using DotSee.ResponsiveImages.Caching;
using DotSee.ResponsiveImages.Cdn;
using DotSee.ResponsiveImages.LazyLoad;
using DotSee.ResponsiveImages.Models;
using DotSee.ResponsiveImages.Preloading;
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
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ICacheService, CacheService>();
        builder.Services.AddSingleton<ILqipService, LqipService>();
        builder.Services.AddScoped<IPreloadCollector, PreloadCollector>();

        // CDN purging. Bound unconditionally so the settings are readable, but the service is only the
        // real implementation when the section explicitly enables it - installing the package must never
        // start making outbound calls to someone's CDN.
        builder.Services.Configure<ImageCdnSettings>(builder.Config.GetSection("DotSee:ImageCdn"));
        builder.Services.AddHttpClient(CloudflareCdnPurgeService.HttpClientName);
        builder.Services.AddTransient<CdnPurgeUrlBuilder>();

        var cdnSettings = new ImageCdnSettings();
        builder.Config.GetSection("DotSee:ImageCdn").Bind(cdnSettings);

        if (cdnSettings.Enabled && cdnSettings.Provider.InvariantEquals("Cloudflare"))
        {
            builder.Services.AddSingleton<ICdnPurgeService, CloudflareCdnPurgeService>();
        }
        else
        {
            builder.Services.AddSingleton<ICdnPurgeService, NullCdnPurgeService>();
        }

        // Invalidate cached image markup / CSS when content or media changes
        builder.AddNotificationHandler<ContentPublishedNotification, ResponsiveImagesCacheInvalidator>();
        builder.AddNotificationHandler<ContentUnpublishedNotification, ResponsiveImagesCacheInvalidator>();
        builder.AddNotificationHandler<ContentDeletedNotification, ResponsiveImagesCacheInvalidator>();
        builder.AddNotificationHandler<MediaSavedNotification, ResponsiveImagesCacheInvalidator>();
        builder.AddNotificationHandler<MediaDeletedNotification, ResponsiveImagesCacheInvalidator>();
    }
}