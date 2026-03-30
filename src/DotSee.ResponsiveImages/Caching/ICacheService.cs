using System;

namespace DotSee.ResponsiveImages.Caching;

public interface ICacheService
{
    T GetCachedItem<T>(string cacheKey, Func<T> factory, TimeSpan? timeout = null, bool isSliding = false);
    void ClearByKey(string keyPrefix);
}
