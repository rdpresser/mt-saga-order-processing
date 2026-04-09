using Microsoft.Extensions.Options;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public sealed class RabbitMqConnectionFactory(IOptions<RabbitMqOptions> options)
{
    public RabbitMqOptions Options { get; } = options.Value;
}
