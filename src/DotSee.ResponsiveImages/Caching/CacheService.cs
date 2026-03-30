using System;
using Microsoft.Extensions.Caching.Memory;

namespace DotSee.ResponsiveImages.Caching;

public class CacheService : ICacheService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(20);
    private readonly IMemoryCache _memoryCache;

    public CacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public T GetCachedItem<T>(string cacheKey, Func<T> factory, TimeSpan? timeout = null, bool isSliding = false)
    {
        return _memoryCache.GetOrCreate(cacheKey, entry =>
        {
            var expiry = timeout ?? DefaultTimeout;

            if (isSliding)
            {
                entry.SlidingExpiration = expiry;
            }
            else
            {
                entry.AbsoluteExpirationRelativeToNow = expiry;
            }

            return factory();
        });
    }

    public void ClearByKey(string keyPrefix)
    {
        // IMemoryCache doesn't support key enumeration by design.
        // For prefix-based clearing, individual keys must be evicted directly.
        // This is a no-op fallback; callers needing prefix clearing should
        // evict specific known keys instead.
    }
}
