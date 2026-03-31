using MassTransit;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;

namespace MT.Saga.OrderProcessing.PaymentService.Consumers.Definitions;

public sealed class RefundPaymentConsumerDefinition : ConsumerDefinition<RefundPaymentConsumer>
{
    public RefundPaymentConsumerDefinition()
    {
        Endpoint(e => e.Name = OrderMessagingTopology.Queues.RefundPayment);
        ConcurrentMessageLimit = 8;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<RefundPaymentConsumer> consumerConfigurator,
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
