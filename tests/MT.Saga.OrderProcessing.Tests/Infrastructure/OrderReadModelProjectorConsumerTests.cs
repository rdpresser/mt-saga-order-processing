using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MT.Saga.OrderProcessing.Infrastructure.Messaging.Consumers;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using Shouldly;
using System.Threading;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class OrderReadModelProjectorConsumerTests
{
    [Fact]
    public async Task ProjectStatusAsync_should_recover_when_insert_conflicts_with_competing_projection()
    {
        var databaseName = $"orders-read-model-{Guid.NewGuid():N}";
        var orderId = Guid.NewGuid();
        var options = CreateOptions(databaseName);

        await using var projectorContext = new DuplicateOnFirstInsertOrderSagaDbContext(options);
        var consumer = new OrderReadModelProjectorConsumer(projectorContext, NullLogger<OrderReadModelProjectorConsumer>.Instance);

        await consumer.ProjectStatusAsync(orderId, "PaymentProcessed", TestContext.Current.CancellationToken);

        await using var verificationContext = new OrderSagaDbContext(options);
        var projected = await verificationContext.Orders.SingleAsync(x => x.OrderId == orderId, TestContext.Current.CancellationToken);

        projected.Status.ShouldBe("PaymentProcessed");
    }

    private static DbContextOptions CreateOptions(string databaseName)
    {
        return new DbContextOptionsBuilder<OrderSagaDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    private sealed class DuplicateOnFirstInsertOrderSagaDbContext : OrderSagaDbContext
    {
        private readonly DbContextOptions _options;
        private bool _simulated;

        public DuplicateOnFirstInsertOrderSagaDbContext(DbContextOptions options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_simulated)
            {
                var pendingInsert = ChangeTracker.Entries<OrderReadModel>()
                    .SingleOrDefault(x => x.State == EntityState.Added);

                if (pendingInsert is not null)
                {
                    _simulated = true;

                    await using var competingContext = new OrderSagaDbContext(_options);
                    competingContext.Orders.Add(new OrderReadModel
                    {
                        OrderId = pendingInsert.Entity.OrderId,
                        Status = "Created",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });

                    await competingContext.SaveChangesAsync(cancellationToken);
                    throw new DbUpdateException("Simulated duplicate insert conflict.", new InvalidOperationException("Simulated duplicate key violation."));
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
