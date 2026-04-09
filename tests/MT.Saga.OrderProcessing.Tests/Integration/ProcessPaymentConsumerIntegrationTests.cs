using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;
using MT.Saga.OrderProcessing.PaymentService.Consumers;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Integration;

public class ProcessPaymentConsumerIntegrationTests
{
    [Fact]
    public async Task Consume_should_publish_payment_processed_for_happy_path()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var ct = TestContext.Current.CancellationToken;

        await harness.Start();
        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(
                EventContext.Create("orders", "order", "process-payment", new ProcessPayment(orderId)),
                ct);

            (await harness.Published.Any<EventContext<PaymentProcessed>>(
                x => x.Context.Message.Payload.OrderId == orderId,
                ct)).ShouldBeTrue();

            (await harness.Published.Any<EventContext<PaymentFailed>>(
                x => x.Context.Message.Payload.OrderId == orderId,
                ct)).ShouldBeFalse();
        }
        finally
        {
            await harness.Stop(ct);
        }
    }

    private static ServiceProvider BuildHarnessProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<MessagingResilienceOptions>()
            .Configure(_ => { });
        services.AddSingleton<IMessagingResilienceOptionsProvider, MessagingResilienceOptionsProvider>();

        services.AddMassTransitTestHarness(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            x.AddConsumer<ProcessPaymentConsumer>();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        return services.BuildServiceProvider(validateScopes: true);
    }
}
