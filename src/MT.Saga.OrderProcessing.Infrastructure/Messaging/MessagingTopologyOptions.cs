namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public sealed class MessagingTopologyOptions
{
    public string EventsExchangeName { get; set; } = OrderMessagingTopology.DefaultEventsExchangeName;
    public string EventsExchangeType { get; set; } = OrderMessagingTopology.DefaultEventsExchangeType;
}
