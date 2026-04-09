using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MT.Saga.OrderProcessing.Contracts;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Consumers;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class OrderReadModelProjectorStatusTransitionTests
{
    [Fact]
    public async Task ProjectStatusAsync_should_create_read_model_for_new_order()
    {
        var databaseName = $"orders-status-transition-{Guid.NewGuid():N}";
        var orderId = Guid.NewGuid();
        var options = CreateOptions(databaseName);
        var ct = TestContext.Current.CancellationToken;

        await using var projectorContext = new OrderSagaDbContext(options);
        var consumer = new OrderReadModelProjectorConsumer(projectorContext, NullLogger<OrderReadModelProjectorConsumer>.Instance);

        await consumer.ProjectStatusAsync(orderId, OrderStatuses.Created, ct);

        await using var verificationContext = new OrderSagaDbContext(options);
        var projected = await verificationContext.Orders.SingleAsync(x => x.OrderId == orderId, ct);

        projected.Status.ShouldBe(OrderStatuses.Created);
        projected.OrderId.ShouldBe(orderId);
    }

    [Fact]
    public async Task ProjectStatusAsync_should_upgrade_status_through_full_lifecycle()
    {
        var databaseName = $"orders-status-transition-{Guid.NewGuid():N}";
        var orderId = Guid.NewGuid();
        var options = CreateOptions(databaseName);
        var ct = TestContext.Current.CancellationToken;

        // Created (rank 1)
        await using (var ctx = new OrderSagaDbContext(options))
        {
            var consumer = new OrderReadModelProjectorConsumer(ctx, NullLogger<OrderReadModelProjectorConsumer>.Instance);
            await consumer.ProjectStatusAsync(orderId, OrderStatuses.Created, ct);
        }

        // PaymentProcessed (rank 2)
        await using (var ctx = new OrderSagaDbContext(options))
        {
            var consumer = new OrderReadModelProjectorConsumer(ctx, NullLogger<OrderReadModelProjectorConsumer>.Instance);
            await consumer.ProjectStatusAsync(orderId, OrderStatuses.PaymentProcessed, ct);
        }

        await using (var verify = new OrderSagaDbContext(options))
        {
            var projected = await verify.Orders.SingleAsync(x => x.OrderId == orderId, ct);
            projected.Status.ShouldBe(OrderStatuses.PaymentProcessed);
        }

        // InventoryReserved (rank 3)
        await using (var ctx = new OrderSagaDbContext(options))
        {
            var consumer = new OrderReadModelProjectorConsumer(ctx, NullLogger<OrderReadModelProjectorConsumer>.Instance);
            await consumer.ProjectStatusAsync(orderId, OrderStatuses.InventoryReserved, ct);
        }

        await using (var verify = new OrderSagaDbContext(options))
        {
            var projected = await verify.Orders.SingleAsync(x => x.OrderId == orderId, ct);
            projected.Status.ShouldBe(OrderStatuses.InventoryReserved);
        }

        // Confirmed (rank 4)
        await using (var ctx = new OrderSagaDbContext(options))
        {
            var consumer = new OrderReadModelProjectorConsumer(ctx, NullLogger<OrderReadModelProjectorConsumer>.Instance);
            await consumer.ProjectStatusAsync(orderId, OrderStatuses.Confirmed, ct);
        }

        await using (var verify = new OrderSagaDbContext(options))
        {
            var projected = await verify.Orders.SingleAsync(x => x.OrderId == orderId, ct);
            projected.Status.ShouldBe(OrderStatuses.Confirmed);
        }
    }

    [Fact]
    public async Task ProjectStatusAsync_should_not_regress_when_lower_rank_arrives_after_higher()
    {
        var databaseName = $"orders-status-transition-{Guid.NewGuid():N}";
        var orderId = Guid.NewGuid();
        var options = CreateOptions(databaseName);
        var ct = TestContext.Current.CancellationToken;

        // Start with Confirmed (rank 4)
        await using (var ctx = new OrderSagaDbContext(options))
        {
            var consumer = new OrderReadModelProjectorConsumer(ctx, NullLogger<OrderReadModelProjectorConsumer>.Instance);
            await consumer.ProjectStatusAsync(orderId, OrderStatuses.Confirmed, ct);
        }

        // Attempt to apply Created (rank 1) out-of-order
        await using (var ctx = new OrderSagaDbContext(options))
        {
            var consumer = new OrderReadModelProjectorConsumer(ctx, NullLogger<OrderReadModelProjectorConsumer>.Instance);
            await consumer.ProjectStatusAsync(orderId, OrderStatuses.Created, ct);
        }

        await using var verificationContext = new OrderSagaDbContext(options);
        var projected = await verificationContext.Orders.SingleAsync(x => x.OrderId == orderId, ct);

        projected.Status.ShouldBe(OrderStatuses.Confirmed);
    }

    [Fact]
    public async Task ProjectStatusAsync_should_accept_same_rank_status_transition()
    {
        var databaseName = $"orders-status-transition-{Guid.NewGuid():N}";
        var orderId = Guid.NewGuid();
        var options = CreateOptions(databaseName);
        var ct = TestContext.Current.CancellationToken;

        // Start with PaymentProcessed (rank 2)
        await using (var ctx = new OrderSagaDbContext(options))
        {
            var consumer = new OrderReadModelProjectorConsumer(ctx, NullLogger<OrderReadModelProjectorConsumer>.Instance);
            await consumer.ProjectStatusAsync(orderId, OrderStatuses.PaymentProcessed, ct);
        }

        // Apply PaymentFailed (also rank 2) - should overwrite
        await using (var ctx = new OrderSagaDbContext(options))
        {
            var consumer = new OrderReadModelProjectorConsumer(ctx, NullLogger<OrderReadModelProjectorConsumer>.Instance);
            await consumer.ProjectStatusAsync(orderId, OrderStatuses.PaymentFailed, ct);
        }

        await using var verificationContext = new OrderSagaDbContext(options);
        var projected = await verificationContext.Orders.SingleAsync(x => x.OrderId == orderId, ct);

        projected.Status.ShouldBe(OrderStatuses.PaymentFailed);
    }

    [Fact]
    public async Task ProjectStatusAsync_should_not_overwrite_known_status_with_unknown()
    {
        var databaseName = $"orders-status-transition-{Guid.NewGuid():N}";
        var orderId = Guid.NewGuid();
        var options = CreateOptions(databaseName);
        var ct = TestContext.Current.CancellationToken;

        // Start with Created (rank 1)
        await using (var ctx = new OrderSagaDbContext(options))
        {
            var consumer = new OrderReadModelProjectorConsumer(ctx, NullLogger<OrderReadModelProjectorConsumer>.Instance);
            await consumer.ProjectStatusAsync(orderId, OrderStatuses.Created, ct);
        }

        // Apply unknown status (rank 0) - should not overwrite
        await using (var ctx = new OrderSagaDbContext(options))
        {
            var consumer = new OrderReadModelProjectorConsumer(ctx, NullLogger<OrderReadModelProjectorConsumer>.Instance);
            await consumer.ProjectStatusAsync(orderId, "SomeUnknownStatus", ct);
        }

        await using var verificationContext = new OrderSagaDbContext(options);
        var projected = await verificationContext.Orders.SingleAsync(x => x.OrderId == orderId, ct);

        projected.Status.ShouldBe(OrderStatuses.Created);
    }

    private static DbContextOptions CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<OrderSagaDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }
}
