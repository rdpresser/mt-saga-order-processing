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
                EventContext.Create(OrderTopologyConstants.SourceService, OrderTopologyConstants.EntityName, OrderTopologyConstants.CommandActions.ProcessPayment, new ProcessPayment(orderId)),
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

    [Fact]
    public async Task Consume_should_be_consumed_by_process_payment_consumer()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var consumerHarness = provider.GetRequiredService<IConsumerTestHarness<ProcessPaymentConsumer>>();
        var ct = TestContext.Current.CancellationToken;

        await harness.Start();
        try
        {
            var orderId = Guid.NewGuid();

            await harness.Bus.Publish(
                EventContext.Create(OrderTopologyConstants.SourceService, OrderTopologyConstants.EntityName, OrderTopologyConstants.CommandActions.ProcessPayment, new ProcessPayment(orderId)),
                ct);

            // Verify the consumer specifically handled this message
            (await consumerHarness.Consumed.Any<EventContext<ProcessPayment>>(
                x => x.Context.Message.Payload.OrderId == orderId,
                ct)).ShouldBeTrue();
        }
        finally
        {
            await harness.Stop(ct);
        }
    }

    [Fact]
    public async Task Consume_should_propagate_correlation_id_from_incoming_event_context()
    {
        await using var provider = BuildHarnessProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var ct = TestContext.Current.CancellationToken;

        await harness.Start();
        try
        {
            var orderId = Guid.NewGuid();
            // ResolveCorrelationId reads context.CorrelationId — the MassTransit transport header,
            // not the EventContext body field. Set it via the publish callback so the consumer
            // receives and propagates it to the outgoing EventContext<PaymentProcessed>.
            var correlationId = Guid.NewGuid();

            await harness.Bus.Publish(
                EventContext.Create(
                    sourceService: OrderTopologyConstants.SourceService,
                    entity: OrderTopologyConstants.EntityName,
                    action: OrderTopologyConstants.CommandActions.ProcessPayment,
                    payload: new ProcessPayment(orderId)),
                publishContext => { publishContext.CorrelationId = correlationId; },
                ct);

            // The published PaymentProcessed should carry the same CorrelationId (as string)
            (await harness.Published.Any<EventContext<PaymentProcessed>>(
                x => x.Context.Message.Payload.OrderId == orderId &&
                     x.Context.Message.CorrelationId == correlationId.ToString(),
                ct)).ShouldBeTrue();
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
