using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class RabbitMqOptionsTests
{
    [Fact]
    public void ConnectionString_should_encode_default_vhost()
    {
        var options = new RabbitMqOptions
        {
            Host = "rabbitmq",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/"
        };

        var connectionString = options.ConnectionString;

        connectionString.ShouldBe("amqp://guest:guest@rabbitmq:5672/%2F");
    }

    [Fact]
    public void ConnectionString_should_include_custom_vhost()
    {
        var options = new RabbitMqOptions
        {
            Host = "rabbitmq",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "orders"
        };

        var connectionString = options.ConnectionString;

        connectionString.ShouldBe("amqp://guest:guest@rabbitmq:5672/orders");
    }
}
