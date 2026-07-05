using DotSee.ResponsiveImages.Caching;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace DotSee.ResponsiveImages.Tests;

public class CacheServiceTests
{
    private static CacheService CreateService() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void GetCachedItem_CachesResult_FactoryRunsOnce()
    {
        var cache = CreateService();
        var calls = 0;

        var first = cache.GetCachedItem("key", () => { calls++; return "value"; });
        var second = cache.GetCachedItem("key", () => { calls++; return "value"; });

        Assert.Equal("value", first);
        Assert.Equal("value", second);
        Assert.Equal(1, calls); // second call served from cache
    }

    [Fact]
    public void Clear_EvictsCachedItem_FactoryRunsAgain()
    {
        var cache = CreateService();
        var calls = 0;

        cache.GetCachedItem("key", () => { calls++; return "value"; });
        Assert.Equal(1, calls);

        cache.Clear();

        cache.GetCachedItem("key", () => { calls++; return "value"; });
        Assert.Equal(2, calls); // cache was cleared, so the factory re-ran
    }

    [Fact]
    public void Clear_EvictsSlidingExpirationEntries()
    {
        var cache = CreateService();
        var calls = 0;

        cache.GetCachedItem("key", () => { calls++; return "value"; }, timeout: TimeSpan.FromMinutes(20), isSliding: true);
        cache.Clear();
        cache.GetCachedItem("key", () => { calls++; return "value"; }, timeout: TimeSpan.FromMinutes(20), isSliding: true);

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Clear_AffectsAllKeys()
    {
        var cache = CreateService();
        var calls = 0;

        cache.GetCachedItem("a", () => { calls++; return "a"; });
        cache.GetCachedItem("b", () => { calls++; return "b"; });
        Assert.Equal(2, calls);

        cache.Clear();

        cache.GetCachedItem("a", () => { calls++; return "a"; });
        cache.GetCachedItem("b", () => { calls++; return "b"; });
        Assert.Equal(4, calls); // both entries were evicted
    }

    [Fact]
    public void GetCachedItem_AfterClear_RepopulatesAndCachesAgain()
    {
        var cache = CreateService();
        var calls = 0;

        cache.GetCachedItem("key", () => { calls++; return "value"; });
        cache.Clear();
        cache.GetCachedItem("key", () => { calls++; return "value"; }); // repopulate (calls == 2)
        cache.GetCachedItem("key", () => { calls++; return "value"; }); // served from cache

        Assert.Equal(2, calls); // new entries after Clear are cached normally
    }
}
