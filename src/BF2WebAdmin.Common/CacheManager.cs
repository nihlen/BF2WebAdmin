using System;
using Microsoft.Extensions.Caching.Memory;

namespace BF2WebAdmin.Common;

public static class CacheManager
{
    private static readonly MemoryCache Cache = new MemoryCache(new MemoryCacheOptions());

    public static void Add<T>(string key, T value, DateTime expirationDateUtc)
    {
        Cache.Set(key, value, expirationDateUtc);
    }

    public static T Get<T>(string key)
    {
        return Cache.Get<T>(key);
    }
}