using Microsoft.Extensions.Configuration;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public sealed class RabbitMqHelper
{
    private const string RabbitMqSectionName = "Messaging:RabbitMq";
    private const string AlternateRabbitMqSectionName = "Messaging:RabbitMQ";

    public RabbitMqOptions RabbitMqSettings { get; }

    public RabbitMqHelper(IConfiguration configuration)
    {
        RabbitMqSettings = configuration.GetSection(RabbitMqSectionName).Get<RabbitMqOptions>()
            ?? configuration.GetSection(AlternateRabbitMqSectionName).Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();
    }

    public static RabbitMqOptions Build(IConfiguration configuration) =>
        new RabbitMqHelper(configuration).RabbitMqSettings;
}
