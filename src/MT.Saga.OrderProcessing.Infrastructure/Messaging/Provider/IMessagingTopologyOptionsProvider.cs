namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;

public interface IMessagingTopologyOptionsProvider
{
    MessagingTopologyOptions Current { get; }
}
