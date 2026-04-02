using MassTransit;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;

namespace MT.Saga.OrderProcessing.InventoryService.Consumers.Definitions;

public sealed class ReserveInventoryConsumerDefinition : ConsumerDefinition<ReserveInventoryConsumer>
{
    private readonly MessagingResilienceOptions _options;

    public ReserveInventoryConsumerDefinition(IMessagingResilienceOptionsProvider optionsProvider)
    {
        _options = optionsProvider.Current;
        Endpoint(e => e.Name = OrderMessagingTopology.Queues.ReserveInventory);
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ReserveInventoryConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Apply concurrent message limit from resilience options
        endpointConfigurator.ConcurrentMessageLimit = _options.ConcurrentMessageLimit;

        // Apply Entity Framework outbox for reliable messaging
        endpointConfigurator.UseEntityFrameworkOutbox<OrderSagaDbContext>(context);

        // Apply exponential retry policy from resilience options
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(_options.MaxRetryAttempts, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));

        // Apply kill-switch circuit breaker
        endpointConfigurator.UseKillSwitch(ks =>
        {
            ks.SetActivationThreshold(_options.KillSwitchActivationThreshold);
            ks.SetTripThreshold(_options.KillSwitchTripThreshold);
            ks.SetRestartTimeout(_options.KillSwitchRestartTimeout);
        });
    }
}
