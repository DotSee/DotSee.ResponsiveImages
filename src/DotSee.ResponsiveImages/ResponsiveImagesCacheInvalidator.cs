using System.Collections.Generic;
using System.Linq;
using DotSee.ResponsiveImages.Caching;
using DotSee.ResponsiveImages.Cdn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace DotSee.ResponsiveImages;

/// <summary>
/// Clears the rendered image markup / CSS cache when content or media changes, so that changes
/// such as republishing a page or editing a media item's focal point or crops are reflected
/// without waiting for the 20-minute sliding expiration to lapse.
/// </summary>
/// <remarks>
/// When CDN purging is switched on (see <see cref="ImageCdnSettings"/>), media changes also drop the
/// affected images from the CDN — a local cache clear is invisible to an edge that is still serving
/// the previous file. Nothing outbound happens unless it has been explicitly enabled in configuration.
/// </remarks>
public class ResponsiveImagesCacheInvalidator :
    INotificationHandler<ContentPublishedNotification>,
    INotificationHandler<ContentUnpublishedNotification>,
    INotificationHandler<ContentDeletedNotification>,
    INotificationHandler<MediaSavedNotification>,
    INotificationHandler<MediaDeletedNotification>
{
    private readonly ICacheService _cacheService;
    private readonly ICdnPurgeService _cdnPurgeService;
    private readonly CdnPurgeUrlBuilder _purgeUrlBuilder;
    private readonly IOptionsMonitor<ImageCdnSettings> _cdnSettings;
    private readonly ILogger<ResponsiveImagesCacheInvalidator> _logger;

    public ResponsiveImagesCacheInvalidator(
        ICacheService cacheService,
        ICdnPurgeService cdnPurgeService,
        CdnPurgeUrlBuilder purgeUrlBuilder,
        IOptionsMonitor<ImageCdnSettings> cdnSettings,
        ILogger<ResponsiveImagesCacheInvalidator> logger)
    {
        _cacheService = cacheService;
        _cdnPurgeService = cdnPurgeService;
        _purgeUrlBuilder = purgeUrlBuilder;
        _cdnSettings = cdnSettings;
        _logger = logger;
    }

    public void Handle(ContentPublishedNotification notification) => _cacheService.Clear();

    public void Handle(ContentUnpublishedNotification notification) => _cacheService.Clear();

    public void Handle(ContentDeletedNotification notification) => _cacheService.Clear();

    public void Handle(MediaSavedNotification notification)
    {
        _cacheService.Clear();
        Purge(notification.SavedEntities, _cdnSettings.CurrentValue.PurgeOnMediaSave);
    }

    public void Handle(MediaDeletedNotification notification)
    {
        _cacheService.Clear();
        Purge(notification.DeletedEntities, _cdnSettings.CurrentValue.PurgeOnMediaDelete);
    }

    private void Purge(IEnumerable<IMedia> media, bool enabledForThisEvent)
    {
        var settings = _cdnSettings.CurrentValue;

        if (!settings.Enabled || !enabledForThisEvent || !_cdnPurgeService.IsEnabled) { return; }

        if (settings.Mode == CdnPurgeMode.Everything)
        {
            //Deliberately blunt: discards the whole zone, not just images.
            var everything = _cdnPurgeService.PurgeEverythingAsync().GetAwaiter().GetResult();
            LogResult(everything, "the entire zone");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            _logger.LogWarning(
                "CDN purging is enabled but DotSee:ImageCdn:BaseUrl is not set, so absolute URLs cannot be built. Skipping.");
            return;
        }

        var urls = media
            .Select(CdnPurgeUrlBuilder.GetMediaPath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .SelectMany(path => _purgeUrlBuilder.Build(path, settings.BaseUrl, settings.MaxUrlsPerPurge))
            .Distinct()
            .Take(settings.MaxUrlsPerPurge)
            .ToList();

        if (urls.Count == 0) { return; }

        var result = _cdnPurgeService.PurgeAsync(urls).GetAwaiter().GetResult();
        LogResult(result, urls.Count + " image URL(s)");
    }

    private void LogResult(CdnPurgeResult result, string what)
    {
        if (!result.Attempted) { return; }

        if (result.Succeeded)
        {
            _logger.LogInformation("CDN purge of {What} succeeded.", what);
        }
        else
        {
            _logger.LogWarning("CDN purge of {What} failed: {Message}", what, result.Message);
        }
    }
}
