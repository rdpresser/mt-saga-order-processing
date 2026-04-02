using MT.Saga.OrderProcessing.Infrastructure.Caching.Abstractions;
using MT.Saga.OrderProcessing.Infrastructure.Caching.Provider;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;

namespace MT.Saga.OrderProcessing.Infrastructure.Caching.DependencyInjection;

public static class CachingServiceCollectionExtensions
{
    public static IServiceCollection AddOrderProcessingCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection("Cache:Redis"))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "Cache:Redis:Host is required")
            .Validate(options => options.Port is > 0 and < 65536, "Cache:Redis:Port must be between 1 and 65535")
            .Validate(options => !string.IsNullOrWhiteSpace(options.InstanceName), "Cache:Redis:InstanceName is required")
            .Validate(options => !(options.Secure && string.IsNullOrWhiteSpace(options.Password)), "Cache:Redis:Password is required when Secure=true")
            .ValidateOnStart();

        services.AddSingleton<ICacheProvider, CacheProvider>();

        services.AddOptions<RedisBackplaneOptions>()
            .Configure<ICacheProvider>((options, cacheProvider) =>
            {
                options.Configuration = cacheProvider.ConnectionString;
            });

        services.AddSingleton<IFusionCacheBackplane, RedisBackplane>();

        services.AddFusionCache()
            .WithDefaultEntryOptions(options =>
            {
                options.Duration = CacheServiceOptions.DefaultExpiration.Duration;
                options.DistributedCacheDuration = CacheServiceOptions.DefaultExpiration.DistributedCacheDuration;
                options.MemoryCacheDuration = TimeSpan.FromMinutes(2);
            })
            .WithDistributedCache(serviceProvider =>
            {
                var cacheProvider = serviceProvider.GetRequiredService<ICacheProvider>();

                return new RedisCache(new RedisCacheOptions
                {
                    Configuration = cacheProvider.ConnectionString,
                    InstanceName = cacheProvider.InstanceName
                });
            })
            .WithRegisteredBackplane()
            .WithSystemTextJsonSerializer(new System.Text.Json.JsonSerializerOptions())
            .AsHybridCache();

        services.AddSingleton<ICacheService, FusionCacheService>();

        return services;
    }
}
