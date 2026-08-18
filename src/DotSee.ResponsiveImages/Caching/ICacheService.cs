using System;

namespace DotSee.ResponsiveImages.Caching;

public interface ICacheService
{
    /// <param name="nullResultTimeout">
    /// Optional separate (absolute) lifetime for a null factory result. Without it, a transient
    /// failure is cached exactly like a success — and under a sliding expiration a busy page keeps
    /// refreshing the failure's window so it never expires.
    /// </param>
    T GetCachedItem<T>(string cacheKey, Func<T> factory, TimeSpan? timeout = null, bool isSliding = false, TimeSpan? nullResultTimeout = null);

    /// <summary>
    /// Evicts every item cached by this service. Used to invalidate rendered image markup and CSS
    /// when content or media changes (e.g. on publish), since cache keys cannot be reconstructed
    /// per image to target them individually.
    /// </summary>
    void Clear();
}
