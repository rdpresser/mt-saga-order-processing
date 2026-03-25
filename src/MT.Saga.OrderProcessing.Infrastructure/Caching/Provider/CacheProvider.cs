using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

namespace MT.Saga.OrderProcessing.Infrastructure.Caching.Provider;

public sealed class CacheProvider(IOptions<RedisOptions> options, IConfiguration configuration) : ICacheProvider
{
    private readonly RedisOptions _options = options.Value;
    private readonly IConfiguration _configuration = configuration;

    public string InstanceName => _options.InstanceName;

    public string ConnectionString
    {
        get
        {
            var injectedConnectionString = _configuration.GetConnectionString("redis");

            // Prefer Aspire-injected connection string when available
            // to keep password/host/port aligned with the orchestrated Redis resource.
            if (!string.IsNullOrWhiteSpace(injectedConnectionString))
            {
                return injectedConnectionString;
            }

            // Fallback for standalone/local execution without Aspire resource binding.
            return _options.ConnectionString;
        }
    }
}
