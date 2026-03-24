namespace MT.Saga.OrderProcessing.Infrastructure.Caching.Provider;

public interface ICacheProvider
{
    string InstanceName { get; }

    string ConnectionString { get; }
}
