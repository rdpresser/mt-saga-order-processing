using Microsoft.Extensions.Options;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public sealed class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
{
    private readonly RabbitMqOptions _options = options.Value;

    public RabbitMqOptions Options => _options;
    public string ConnectionString => _options.ConnectionString;
    public string VirtualHost => _options.VirtualHost;
    public bool AutoProvision => _options.AutoProvision;
    public bool Durable => _options.Durable;
    public bool UseQuorumQueues => _options.UseQuorumQueues;
    public bool AutoPurgeOnStartup => _options.AutoPurgeOnStartup;
    public string Exchange => _options.Exchange;
}
