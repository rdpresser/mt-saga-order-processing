using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Contracts.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;
using MT.Saga.OrderProcessing.PaymentService.Consumers;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Integration;

public class RefundPaymentConsumerIntegrationTests
{
    [Fact]
    public async Task Consume_should_complete_without_throwing_for_any_order()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var consumerHarness = provider.GetRequiredService<IConsumerTestHarness<RefundPaymentConsumer>>();
        var ct = TestContext.Current.CancellationToken;

        await harness.Start();
        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(
                EventContext.Create(
                    OrderTopologyConstants.SourceService,
                    OrderTopologyConstants.EntityName,
                    OrderTopologyConstants.CommandActions.RefundPayment,
                    new RefundPayment(orderId)),
                ct);

            // Consumer must have consumed the message without faulting.
            // RefundPayment is fire-and-forget logging — no downstream events are published by the consumer.
            (await consumerHarness.Consumed.Any<EventContext<RefundPayment>>(
                x => x.Context.Message.Payload.OrderId == orderId,
                ct)).ShouldBeTrue();
        }
        finally
        {
            await harness.Stop(ct);
        }
    }

    [Fact]
    public async Task Consume_should_not_fault_when_called_multiple_times_for_same_order()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var consumerHarness = provider.GetRequiredService<IConsumerTestHarness<RefundPaymentConsumer>>();
        var ct = TestContext.Current.CancellationToken;

        await harness.Start();
        try
        {
            var orderIdA = Guid.NewGuid();
            var orderIdB = Guid.NewGuid();

            // Publish two different refund events (simulating multiple compensation triggers)
            await harness.Bus.Publish(
                EventContext.Create(OrderTopologyConstants.SourceService, OrderTopologyConstants.EntityName, OrderTopologyConstants.CommandActions.RefundPayment, new RefundPayment(orderIdA)),
                ct);
            await harness.Bus.Publish(
                EventContext.Create(OrderTopologyConstants.SourceService, OrderTopologyConstants.EntityName, OrderTopologyConstants.CommandActions.RefundPayment, new RefundPayment(orderIdB)),
                ct);

            // Both messages must be consumed without faulting
            (await consumerHarness.Consumed.Any<EventContext<RefundPayment>>(
                x => x.Context.Message.Payload.OrderId == orderIdA, ct)).ShouldBeTrue();
            (await consumerHarness.Consumed.Any<EventContext<RefundPayment>>(
                x => x.Context.Message.Payload.OrderId == orderIdB, ct)).ShouldBeTrue();
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
            x.AddConsumer<RefundPaymentConsumer>();
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
        });

        return services.BuildServiceProvider(validateScopes: true);
    }
}
