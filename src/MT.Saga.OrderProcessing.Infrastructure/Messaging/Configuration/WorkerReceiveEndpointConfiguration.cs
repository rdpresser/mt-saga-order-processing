using MassTransit;
using MassTransit.RabbitMqTransport;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;

/// <summary>
/// Receive endpoint configuration for worker service queues.
/// Explicit configuration without reflection-based discovery.
/// </summary>
public static class WorkerReceiveEndpointConfiguration
{
    /// <summary>
    /// Configures "process-payment" receive endpoint for PaymentService.
    /// Registers ProcessPaymentConsumer and command handling.
    /// </summary>
    public static void ConfigurePaymentProcessingReceiveEndpoint(
        this IRabbitMqBusFactoryConfigurator cfg,
        IRegistrationContext context,
        CommonMassTransitPoliciesConfiguration.MessagingPoliciesOptions policyOptions)
    {
        cfg.ReceiveEndpoint("process-payment", ep =>
        {
            // Apply common resilience policies
            ep.ConfigureCommonReceiveEndpointPolicies(context, policyOptions);

            // Configure prefetch and concurrency
            ep.PrefetchCount = (ushort)policyOptions.PrefetchCount;
            ep.ConcurrentMessageLimit = policyOptions.ConcurrentMessageLimit;

            // Note: Actual consumer registration happens in worker service.
            // This just configures the endpoint topology.
            // Consumer will be bound via AddPaymentServiceConsumers() call.
        });
    }

    /// <summary>
    /// Configures "refund-payment" receive endpoint for PaymentService.
    /// Registers RefundPaymentConsumer and compensation command handling.
    /// </summary>
    public static void ConfigureRefundPaymentReceiveEndpoint(
        this IRabbitMqBusFactoryConfigurator cfg,
        IRegistrationContext context,
        CommonMassTransitPoliciesConfiguration.MessagingPoliciesOptions policyOptions)
    {
        cfg.ReceiveEndpoint("refund-payment", ep =>
        {
            // Apply common resilience policies
            ep.ConfigureCommonReceiveEndpointPolicies(context, policyOptions);

            // Configure prefetch and concurrency
            ep.PrefetchCount = (ushort)policyOptions.PrefetchCount;
            ep.ConcurrentMessageLimit = policyOptions.ConcurrentMessageLimit;

            // Note: Actual consumer registration happens in worker service.
            // Consumer will be bound via AddPaymentServiceConsumers() call.
        });
    }

    /// <summary>
    /// Configures "reserve-inventory" receive endpoint for InventoryService.
    /// Registers ReserveInventoryConsumer and command handling.
    /// </summary>
    public static void ConfigureReserveInventoryReceiveEndpoint(
        this IRabbitMqBusFactoryConfigurator cfg,
        IRegistrationContext context,
        CommonMassTransitPoliciesConfiguration.MessagingPoliciesOptions policyOptions)
    {
        cfg.ReceiveEndpoint("reserve-inventory", ep =>
        {
            // Apply common resilience policies
            ep.ConfigureCommonReceiveEndpointPolicies(context, policyOptions);

            // Configure prefetch and concurrency
            ep.PrefetchCount = (ushort)policyOptions.PrefetchCount;
            ep.ConcurrentMessageLimit = policyOptions.ConcurrentMessageLimit;

            // Note: Actual consumer registration happens in worker service.
            // Consumer will be bound via AddInventoryServiceConsumers() call.
        });
    }
}

/// <summary>
/// Extension methods for registering worker service consumers explicitly.
/// Replaces reflection-based consumer discovery in AddWorkerMassTransit.
///
/// Usage in worker service Program.cs:
///     services.AddWorkerServiceMassTransit(configuration, connectionString)
///         .AddPaymentServiceConsumers()
///         .AddInventoryServiceConsumers();
/// </summary>
public static class WorkerServiceConsumerExtensions
{
    /// <summary>
    /// Registers all Payment Service consumers.
    /// - ProcessPaymentConsumer (processes payment commands)
    /// - RefundPaymentConsumer (handles compensation/refunds)
    /// </summary>
    public static IRegistrationConfigurator AddPaymentServiceConsumers(
        this IRegistrationConfigurator registrar)
    {
        // Placeholder for future: Register ProcessPaymentConsumer and RefundPaymentConsumer
        // Implementation details will be added when payment service consumers are available
        return registrar;
    }

    /// <summary>
    /// Registers all Inventory Service consumers.
    /// - ReserveInventoryConsumer (processes inventory reservation commands)
    /// </summary>
    public static IRegistrationConfigurator AddInventoryServiceConsumers(
        this IRegistrationConfigurator registrar)
    {
        // Placeholder for future: Register ReserveInventoryConsumer
        // Implementation details will be added when inventory service consumers are available
        return registrar;
    }
}
