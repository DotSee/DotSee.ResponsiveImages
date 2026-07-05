using System;

namespace DotSee.ResponsiveImages.Caching;

public interface ICacheService
{
    T GetCachedItem<T>(string cacheKey, Func<T> factory, TimeSpan? timeout = null, bool isSliding = false);

    /// <summary>
    /// Evicts every item cached by this service. Used to invalidate rendered image markup and CSS
    /// when content or media changes (e.g. on publish), since cache keys cannot be reconstructed
    /// per image to target them individually.
    /// </summary>
    void Clear();
}
