using System;
using System.Collections.Concurrent;
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

    // One in-flight factory per key, so N concurrent misses on the same key run the factory once
    // instead of N times. That matters most right after Clear() — every publish empties the whole
    // cache, and without this each cold LQIP key would decode the same media file once per
    // concurrent request.
    private readonly ConcurrentDictionary<string, Lazy<object>> _inFlight = new();

    public CacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public T GetCachedItem<T>(string cacheKey, Func<T> factory, TimeSpan? timeout = null, bool isSliding = false, TimeSpan? nullResultTimeout = null)
    {
        if (_memoryCache.TryGetValue(cacheKey, out object cached))
        {
            return cached == null ? default : (T)cached;
        }

        var lazy = _inFlight.GetOrAdd(cacheKey, _ => new Lazy<object>(
            () => CreateAndCache(cacheKey, factory, timeout, isSliding, nullResultTimeout),
            LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            var value = lazy.Value;
            return value == null ? default : (T)value;
        }
        finally
        {
            // Remove after materialisation (or failure) so a later miss creates a fresh factory
            // rather than reusing one whose result has expired — or whose exception Lazy has latched.
            _inFlight.TryRemove(cacheKey, out _);
        }
    }

    private object CreateAndCache<T>(string cacheKey, Func<T> factory, TimeSpan? timeout, bool isSliding, TimeSpan? nullResultTimeout)
    {
        // Capture the source once: Clear() swaps the field, and reading it twice could hand us a
        // token from a source another thread is in the middle of replacing.
        var resetTokenSource = _resetTokenSource;

        var result = factory();

        using var entry = _memoryCache.CreateEntry(cacheKey);

        if (result == null && nullResultTimeout.HasValue)
        {
            // Negative results get their own (typically much shorter, always absolute) lifetime:
            // a transient failure cached as sliding would refresh its own window on every hit and
            // never expire on a busy page.
            entry.AbsoluteExpirationRelativeToNow = nullResultTimeout.Value;
        }
        else if (isSliding)
        {
            entry.SlidingExpiration = timeout ?? DefaultTimeout;
        }
        else
        {
            entry.AbsoluteExpirationRelativeToNow = timeout ?? DefaultTimeout;
        }

        entry.AddExpirationToken(new CancellationChangeToken(resetTokenSource.Token));
        entry.Value = result;

        return result;
    }

    public void Clear()
    {
        // Cancel, but deliberately do not Dispose: a concurrent GetCachedItem may have already read
        // the old source and be about to ask it for its Token, which throws on a disposed source.
        // A cancelled-but-undisposed source is harmless and is collected once its entries are gone.
        var previous = Interlocked.Exchange(ref _resetTokenSource, new CancellationTokenSource());
        previous.Cancel();
    }
}
