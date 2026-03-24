using Microsoft.Extensions.Options;

namespace MT.Saga.OrderProcessing.Infrastructure.Caching.Provider;

public sealed class CacheProvider(IOptions<RedisOptions> options) : ICacheProvider
{
    private readonly RedisOptions _options = options.Value;

    public string InstanceName => _options.InstanceName;

    public string ConnectionString => _options.ConnectionString;
}
