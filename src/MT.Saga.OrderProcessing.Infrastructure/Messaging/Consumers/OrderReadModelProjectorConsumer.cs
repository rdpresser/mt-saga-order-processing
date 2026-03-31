using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MT.Saga.OrderProcessing.Contracts;
using MT.Saga.OrderProcessing.Contracts.Events;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Consumers;

public sealed class OrderReadModelProjectorConsumer :
    IConsumer<EventContext<OrderCreated>>,
    IConsumer<EventContext<PaymentProcessed>>,
    IConsumer<EventContext<PaymentFailed>>,
    IConsumer<EventContext<InventoryReserved>>,
    IConsumer<EventContext<InventoryFailed>>,
    IConsumer<EventContext<OrderConfirmed>>,
    IConsumer<EventContext<OrderCancelled>>
{
    private readonly OrderSagaDbContext _dbContext;
    private readonly ILogger<OrderReadModelProjectorConsumer> _logger;

    public OrderReadModelProjectorConsumer(OrderSagaDbContext dbContext, ILogger<OrderReadModelProjectorConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<EventContext<OrderCreated>> context)
        => ProjectStatusAsync(context.Message.Payload.OrderId, OrderStatuses.Created, context.CancellationToken);

    public Task Consume(ConsumeContext<EventContext<PaymentProcessed>> context)
        => ProjectStatusAsync(context.Message.Payload.OrderId, OrderStatuses.PaymentProcessed, context.CancellationToken);

    public Task Consume(ConsumeContext<EventContext<PaymentFailed>> context)
        => ProjectStatusAsync(context.Message.Payload.OrderId, OrderStatuses.PaymentFailed, context.CancellationToken);

    public Task Consume(ConsumeContext<EventContext<InventoryReserved>> context)
        => ProjectStatusAsync(context.Message.Payload.OrderId, OrderStatuses.InventoryReserved, context.CancellationToken);

    public Task Consume(ConsumeContext<EventContext<InventoryFailed>> context)
        => ProjectStatusAsync(context.Message.Payload.OrderId, OrderStatuses.InventoryFailed, context.CancellationToken);

    public Task Consume(ConsumeContext<EventContext<OrderConfirmed>> context)
        => ProjectStatusAsync(context.Message.Payload.OrderId, OrderStatuses.Confirmed, context.CancellationToken);

    public Task Consume(ConsumeContext<EventContext<OrderCancelled>> context)
        => ProjectStatusAsync(context.Message.Payload.OrderId, OrderStatuses.Cancelled, context.CancellationToken);

    internal async Task ProjectStatusAsync(Guid orderId, string status, CancellationToken cancellationToken)
    {
        var readModel = await _dbContext.Orders
            .SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var attemptedInsert = false;

        if (readModel is null)
        {
            attemptedInsert = true;
            _dbContext.Orders.Add(new OrderReadModel
            {
                OrderId = orderId,
                Status = status,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            ApplyStatusTransition(readModel, status, now);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var recovered = await TryRecoverConcurrentUpdateAsync(orderId, status, now, cancellationToken)
                .ConfigureAwait(false);

            if (!recovered)
            {
                throw;
            }

            _logger.LogWarning(
                ex,
                "Recovered optimistic concurrency conflict for order {OrderId}; status projection continued with {Status}.",
                orderId,
                status);
        }
        catch (DbUpdateException ex) when (attemptedInsert)
        {
            var recovered = await TryRecoverConcurrentInsertAsync(orderId, status, now, cancellationToken)
                .ConfigureAwait(false);

            if (!recovered)
            {
                throw;
            }

            _logger.LogWarning(
                ex,
                "Recovered concurrent read-model insert for order {OrderId}; status projection continued with {Status}.",
                orderId,
                status);
        }

        _logger.LogInformation("Order read model updated: {OrderId} -> {Status}", orderId, status);
    }

    private async Task<bool> TryRecoverConcurrentInsertAsync(
        Guid orderId,
        string status,
        DateTime now,
        CancellationToken cancellationToken)
    {
        return await TryRecoverConcurrentChangeAsync(orderId, status, now, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> TryRecoverConcurrentUpdateAsync(
        Guid orderId,
        string status,
        DateTime now,
        CancellationToken cancellationToken)
    {
        return await TryRecoverConcurrentChangeAsync(orderId, status, now, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> TryRecoverConcurrentChangeAsync(
        Guid orderId,
        string status,
        DateTime now,
        CancellationToken cancellationToken)
    {
        _dbContext.ChangeTracker.Clear();

        var existing = await _dbContext.Orders
            .SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return false;
        }

        ApplyStatusTransition(existing, status, now);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static void ApplyStatusTransition(OrderReadModel readModel, string status, DateTime now)
    {
        // Projection can receive out-of-order deliveries; never regress to an earlier lifecycle stage.
        if (GetStatusRank(status) >= GetStatusRank(readModel.Status))
        {
            readModel.Status = status;
            readModel.UpdatedAt = now;
        }
    }

    private static int GetStatusRank(string status)
    {
        return status switch
        {
            OrderStatuses.Created => 1,
            OrderStatuses.PaymentProcessed => 2,
            OrderStatuses.PaymentFailed => 2,
            OrderStatuses.InventoryReserved => 3,
            OrderStatuses.InventoryFailed => 3,
            OrderStatuses.Confirmed => 4,
            OrderStatuses.Cancelled => 4,
            _ => 0
        };
    }
}
