using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexus.BuildingBlocks.Web;

namespace Nexus.BuildingBlocks.Data;

public static class CacheExtensions
{
    /// <summary>Redis when Redis:ConnectionString is set, otherwise an in-process cache (still functional).</summary>
    public static IServiceCollection AddNexusCache(this IServiceCollection services, IConfiguration config)
    {
        var redis = config["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redis))
            services.AddStackExchangeRedisCache(o => { o.Configuration = redis; o.InstanceName = "nexus:"; });
        else
            services.AddDistributedMemoryCache();
        return services;
    }

    /// <summary>Cache failures are logged and never break the request (cache is an optimisation, not a dependency).</summary>
    public static async Task<T?> GetOrCreateAsync<T>(this IDistributedCache cache, string key, TimeSpan ttl,
        Func<CancellationToken, Task<T?>> factory, ILogger? logger, CancellationToken ct)
    {
        try
        {
            var cached = await cache.GetStringAsync(key, ct);
            if (cached != null) return JsonSerializer.Deserialize<T>(cached, JsonDefaults.Options);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Cache read failed for {Key}", key);
        }

        var value = await factory(ct);
        if (value is null) return value;
        try
        {
            await cache.SetStringAsync(key, JsonSerializer.Serialize(value, JsonDefaults.Options),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Cache write failed for {Key}", key);
        }
        return value;
    }

    public static async Task InvalidateAsync(this IDistributedCache cache, ILogger? logger, params string[] keys)
    {
        foreach (var k in keys)
        {
            try { await cache.RemoveAsync(k); }
            catch (Exception ex) { logger?.LogWarning(ex, "Cache invalidation failed for {Key}", k); }
        }
    }
}
