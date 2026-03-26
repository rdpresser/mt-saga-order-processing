using Microsoft.EntityFrameworkCore;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using MT.Saga.OrderProcessing.Saga;
using Shouldly;

namespace MT.Saga.OrderProcessing.Tests.Infrastructure;

public class SagaDbContextRowVersionMappingTests
{
    [Fact]
    public void OrderState_rowversion_should_map_to_postgres_xmin()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(OrderState));

        entity.ShouldNotBeNull();

        var rowVersionProperty = entity!.FindProperty(nameof(OrderState.RowVersion));
        rowVersionProperty.ShouldNotBeNull();
        rowVersionProperty!.IsConcurrencyToken.ShouldBeTrue();
        rowVersionProperty.ValueGenerated.ShouldBe(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
        rowVersionProperty.GetColumnName().ShouldBe("xmin");
        rowVersionProperty.GetColumnType().ShouldBe("xid");
    }

    [Fact]
    public void OrderReadModel_rowversion_should_map_to_postgres_xmin()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(OrderReadModel));

        entity.ShouldNotBeNull();

        var rowVersionProperty = entity!.FindProperty(nameof(OrderReadModel.RowVersion));
        rowVersionProperty.ShouldNotBeNull();
        rowVersionProperty!.IsConcurrencyToken.ShouldBeTrue();
        rowVersionProperty.ValueGenerated.ShouldBe(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
        rowVersionProperty.GetColumnName().ShouldBe("xmin");
        rowVersionProperty.GetColumnType().ShouldBe("xid");
    }

    private static OrderSagaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrderSagaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=rowversion_mapping_tests;Username=postgres;Password=postgres")
            .Options;

        return new OrderSagaDbContext(options);
    }
}
