using System.Reflection;
using MT.Saga.OrderProcessing.Contracts;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;
using MT.Saga.OrderProcessing.InventoryService.Consumers;
using MT.Saga.OrderProcessing.OrderService;
using MT.Saga.OrderProcessing.PaymentService.Consumers;
using MT.Saga.OrderProcessing.Saga;

namespace MT.Saga.OrderProcessing.Architecture.Tests;

public abstract class BaseTest
{
    protected static readonly Assembly ContractsAssembly = typeof(OrderStatuses).Assembly;
    protected static readonly Assembly SagaAssembly = typeof(OrderStateMachine).Assembly;
    protected static readonly Assembly InfrastructureAssembly = typeof(OrderSagaDbContext).Assembly;
    protected static readonly Assembly OrderServiceAssembly = typeof(OrderServiceEntryPoint).Assembly;
    protected static readonly Assembly PaymentServiceAssembly = typeof(ProcessPaymentConsumer).Assembly;
    protected static readonly Assembly InventoryServiceAssembly = typeof(ReserveInventoryConsumer).Assembly;
}
