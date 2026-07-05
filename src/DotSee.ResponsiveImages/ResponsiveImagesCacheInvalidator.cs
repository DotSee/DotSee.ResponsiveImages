using DotSee.ResponsiveImages.Caching;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace DotSee.ResponsiveImages;

/// <summary>
/// Clears the rendered image markup / CSS cache when content or media changes, so that changes
/// such as republishing a page or editing a media item's focal point or crops are reflected
/// without waiting for the 20-minute sliding expiration to lapse.
/// </summary>
public class ResponsiveImagesCacheInvalidator :
    INotificationHandler<ContentPublishedNotification>,
    INotificationHandler<ContentUnpublishedNotification>,
    INotificationHandler<ContentDeletedNotification>,
    INotificationHandler<MediaSavedNotification>,
    INotificationHandler<MediaDeletedNotification>
{
    private readonly ICacheService _cacheService;

    public ResponsiveImagesCacheInvalidator(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public void Handle(ContentPublishedNotification notification) => _cacheService.Clear();

    public void Handle(ContentUnpublishedNotification notification) => _cacheService.Clear();

    public void Handle(ContentDeletedNotification notification) => _cacheService.Clear();

    public void Handle(MediaSavedNotification notification) => _cacheService.Clear();

    public void Handle(MediaDeletedNotification notification) => _cacheService.Clear();
}
