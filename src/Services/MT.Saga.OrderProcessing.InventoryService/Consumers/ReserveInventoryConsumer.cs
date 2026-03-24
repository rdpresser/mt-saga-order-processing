using MassTransit;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Messaging;

namespace MT.Saga.OrderProcessing.InventoryService.Consumers;

public sealed class ReserveInventoryConsumer(
    ILogger<ReserveInventoryConsumer> logger,
    MessagingResilienceOptions resilienceOptions) : IConsumer<EventContext<ReserveInventory>>
{
    public async Task Consume(ConsumeContext<EventContext<ReserveInventory>> context)
    {
        var orderId = context.Message.Payload.OrderId;

        try
        {
            logger.LogInformation("Reserving inventory for OrderId: {OrderId}", orderId);

            var outOfStock = context.Headers.TryGetHeader("inventory-out-of-stock", out var outOfStockRaw)
                && outOfStockRaw is bool outOfStockFlag
                && outOfStockFlag;

            if (outOfStock)
            {
                logger.LogWarning("Inventory unavailable for OrderId: {OrderId}. Triggering compensation flow.", orderId);

                var failedEvent = EventContext.Create(
                    sourceService: OrderMessagingTopology.SourceService,
                    entity: OrderMessagingTopology.EntityName,
                    action: OrderMessagingTopology.Actions.InventoryFailed,
                    payload: new InventoryFailed(orderId),
                    correlationId: context.ResolveCorrelationId(),
                    causationId: context.MessageId?.ToString(),
                    userId: context.ResolveUserId(),
                    isAuthenticated: context.ResolveIsAuthenticated(),
                    metadata: context.BuildAuditMetadata());

                await context
                    .PublishEventContextWithRetryAsync(failedEvent, logger, resilienceOptions, context.CancellationToken)
                    .ConfigureAwait(false);

                return;
            }

            var successEvent = EventContext.Create(
                sourceService: OrderMessagingTopology.SourceService,
                entity: OrderMessagingTopology.EntityName,
                action: OrderMessagingTopology.Actions.InventoryReserved,
                payload: new InventoryReserved(orderId),
                correlationId: context.ResolveCorrelationId(),
                causationId: context.MessageId?.ToString(),
                userId: context.ResolveUserId(),
                isAuthenticated: context.ResolveIsAuthenticated(),
                metadata: context.BuildAuditMetadata());

            await context
                .PublishEventContextWithRetryAsync(successEvent, logger, resilienceOptions, context.CancellationToken)
                .ConfigureAwait(false);

            logger.LogInformation("Inventory reserved for OrderId: {OrderId}", orderId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Inventory reservation failed for OrderId: {OrderId}", orderId);

            var failedEvent = EventContext.Create(
                sourceService: OrderMessagingTopology.SourceService,
                entity: OrderMessagingTopology.EntityName,
                action: OrderMessagingTopology.Actions.InventoryFailed,
                payload: new InventoryFailed(orderId),
                correlationId: context.ResolveCorrelationId(),
                causationId: context.MessageId?.ToString(),
                userId: context.ResolveUserId(),
                isAuthenticated: context.ResolveIsAuthenticated(),
                metadata: context.BuildAuditMetadata());

            await context
                .PublishEventContextWithRetryAsync(failedEvent, logger, resilienceOptions, context.CancellationToken)
                .ConfigureAwait(false);
        }
    }

}
