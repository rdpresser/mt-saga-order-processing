using MassTransit;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;

namespace MT.Saga.OrderProcessing.InventoryService.Consumers.Definitions;

public sealed class ReserveInventoryConsumerDefinition : ConsumerDefinition<ReserveInventoryConsumer>
{
    public ReserveInventoryConsumerDefinition()
    {
        Endpoint(e => e.Name = OrderMessagingTopology.Queues.ReserveInventory);
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ReserveInventoryConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseEntityFrameworkOutbox<OrderSagaDbContext>(context);

        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));

        endpointConfigurator.UseKillSwitch(ks =>
        {
            ks.SetActivationThreshold(10);
            ks.SetTripThreshold(0.15);
            ks.SetRestartTimeout(TimeSpan.FromMinutes(1));
        });
    }
}
