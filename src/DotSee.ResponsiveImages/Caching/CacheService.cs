using System;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace DotSee.ResponsiveImages.Caching;

public class CacheService : ICacheService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(20);
    private readonly IMemoryCache _memoryCache;

    // Every cached entry is linked to this token. Cancelling it evicts them all at once,
    // which is how Clear() invalidates the whole plugin cache (IMemoryCache has no key enumeration).
    private CancellationTokenSource _resetTokenSource = new();

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

            entry.AddExpirationToken(new CancellationChangeToken(_resetTokenSource.Token));

            return factory();
        });
    }

    public void Clear()
    {
        var previous = Interlocked.Exchange(ref _resetTokenSource, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }
}
