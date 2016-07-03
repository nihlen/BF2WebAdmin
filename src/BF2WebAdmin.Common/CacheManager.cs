using System;
using System.Runtime.Caching;

namespace BF2WebAdmin.Common
{
    public static class CacheManager
    {
        private static readonly MemoryCache Cache = MemoryCache.Default;

        public static void Add<T>(string key, T value, DateTime expirationDateUtc)
        {
            Cache.Add(key, value, expirationDateUtc);
        }

        public static T Get<T>(string key)
        {
            return (T)Cache.Get(key);
        }
    }
}