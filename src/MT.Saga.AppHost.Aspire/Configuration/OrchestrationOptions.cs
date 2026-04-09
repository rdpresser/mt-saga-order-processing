namespace MT.Saga.AppHost.Aspire.Configuration;

public sealed class RedisOrchestrationOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6379;
    public string Password { get; set; } = string.Empty;
    public bool Secure { get; set; }
    public string InstanceName { get; set; } = "mt-saga-order-processing";
}

public sealed class RabbitMqOrchestrationOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public sealed class MessagingTopologyOrchestrationOptions
{
    public string EventsExchangeName { get; set; } = "orders.events-exchange";
    public string EventsExchangeType { get; set; } = "topic";
}

public sealed class PostgresOrchestrationOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "postgres";
    public string Database { get; set; } = "mt_saga_order_processing";
}
