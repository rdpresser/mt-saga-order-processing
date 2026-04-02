namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;

public interface IMessagingResilienceOptionsProvider
{
    MessagingResilienceOptions Current { get; }
}
