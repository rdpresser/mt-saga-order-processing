namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public int ManagementPort { get; set; } = 15672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = "mt-saga-order-processing.events";
    public bool AutoProvision { get; set; } = true;
    public bool Durable { get; set; } = true;
    public bool UseQuorumQueues { get; set; }
    public bool AutoPurgeOnStartup { get; set; }

    public string ConnectionString => BuildAmqpUri();

    private string BuildAmqpUri()
    {
        var vhost = string.IsNullOrWhiteSpace(VirtualHost) ? "/" : VirtualHost.Trim();

        if (!vhost.StartsWith('/'))
        {
            vhost = "/" + vhost;
        }

        if (vhost == "/")
        {
            vhost = "/%2F";
        }

        return $"amqp://{UserName}:{Password}@{Host}:{Port}{vhost}";
    }
}
