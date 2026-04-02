using MassTransit;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Provider;

namespace MT.Saga.OrderProcessing.PaymentService.Consumers;

public sealed class ProcessPaymentConsumer(
    ILogger<ProcessPaymentConsumer> logger,
    IMessagingResilienceOptionsProvider resilienceOptionsProvider) : IConsumer<EventContext<ProcessPayment>>
{
    public async Task Consume(ConsumeContext<EventContext<ProcessPayment>> context)
    {
        var orderId = context.Message.Payload.OrderId;

        try
        {
            logger.LogInformation("Processing payment for OrderId: {OrderId}", orderId);

            var successEvent = EventContext.Create(
                sourceService: OrderMessagingTopology.SourceService,
                entity: OrderMessagingTopology.EntityName,
                action: OrderMessagingTopology.Actions.PaymentProcessed,
                payload: new PaymentProcessed(orderId),
                correlationId: context.ResolveCorrelationId(),
                causationId: context.MessageId?.ToString(),
                userId: context.ResolveUserId(),
                isAuthenticated: context.ResolveIsAuthenticated(),
                metadata: context.BuildAuditMetadata());

            await context
                .PublishEventContextWithRetryAsync(successEvent, logger, resilienceOptionsProvider.Current, context.CancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Payment processed for OrderId: {OrderId}", orderId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Payment processing failed for OrderId: {OrderId}", orderId);

            var failedEvent = EventContext.Create(
                sourceService: OrderMessagingTopology.SourceService,
                entity: OrderMessagingTopology.EntityName,
                action: OrderMessagingTopology.Actions.PaymentFailed,
                payload: new PaymentFailed(orderId),
                correlationId: context.ResolveCorrelationId(),
                causationId: context.MessageId?.ToString(),
                userId: context.ResolveUserId(),
                isAuthenticated: context.ResolveIsAuthenticated(),
                metadata: context.BuildAuditMetadata());

            await context
                .PublishEventContextWithRetryAsync(failedEvent, logger, resilienceOptionsProvider.Current, context.CancellationToken)
                .ConfigureAwait(false);
        }
    }

}
