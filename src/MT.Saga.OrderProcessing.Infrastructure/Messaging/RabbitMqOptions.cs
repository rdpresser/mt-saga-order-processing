namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    private const char UriPathSeparator = '/';

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public int ManagementPort { get; set; } = 15672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = OrderMessagingTopology.DefaultEventsExchangeName;
    public bool AutoProvision { get; set; } = true;
    public bool Durable { get; set; } = true;
    public bool UseQuorumQueues { get; set; }
    public bool AutoPurgeOnStartup { get; set; }

    public string ConnectionString => BuildAmqpUri();

    private string BuildAmqpUri()
    {
        var user = Uri.EscapeDataString(UserName ?? string.Empty);
        var pass = Uri.EscapeDataString(Password ?? string.Empty);
        var rawVhost = string.IsNullOrWhiteSpace(VirtualHost)
            ? UriPathSeparator.ToString()
            : VirtualHost.Trim();

        if (!rawVhost.StartsWith(UriPathSeparator))
        {
            rawVhost = UriPathSeparator + rawVhost;
        }

        var vhostPath = rawVhost == UriPathSeparator.ToString()
            ? "%2F"
            : Uri.EscapeDataString(rawVhost.TrimStart(UriPathSeparator));

        var builder = new UriBuilder
        {
            Scheme = "amqp",
            Host = Host,
            Port = Port,
            UserName = user,
            Password = pass,
            Path = vhostPath
        };

        return builder.Uri.ToString();
    }
}
