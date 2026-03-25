using MassTransit;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;

/// <summary>
/// Explicit worker service consumer configurations.
/// Replaces reflection-based AddConsumer(Type) discovery.
/// Each service defines its consumers explicitly and clearly.
/// </summary>
///
/// <summary>
/// Payment service consumers (explicit, no reflection).
/// Located in: MT.Saga.OrderProcessing.PaymentService
/// </summary>
public static class PaymentServiceConsumersConfiguration
{
    /// <summary>
    /// Registers payment service consumers.
    /// - ProcessPaymentConsumer: Handles ProcessPayment command
    /// - RefundPaymentConsumer: Handles RefundPayment command
    ///
    /// Usage in PaymentService Program.cs:
    ///     var cfg = new RegistrationConfigurator();
    ///     cfg.AddPaymentServiceConsumers();
    ///     bus.AddConsumers(cfg); // Using MassTransit's bus.AddConsumers factory
    /// </summary>
    public static IRegistrationConfigurator AddPaymentServiceConsumers(
        this IRegistrationConfigurator cfg)
    {
        // NOTE: Consumers from PaymentService are registered explicitly in PaymentService.Program.cs
        // This method is a placeholder for the pattern where service-specific consumers are
        // registered via explicit IRegistrationConfigurator calls (no reflection)

        return cfg;
    }
}

/// <summary>
/// Inventory service consumers (explicit, no reflection).
/// Located in: MT.Saga.OrderProcessing.InventoryService
/// </summary>
public static class InventoryServiceConsumersConfiguration
{
    /// <summary>
    /// Registers inventory service consumers.
    /// - ReserveInventoryConsumer: Handles ReserveInventory command
    /// </summary>
    public static IRegistrationConfigurator AddInventoryServiceConsumers(
        this IRegistrationConfigurator cfg)
    {
        // Placeholder for future: Register ReserveInventoryConsumer when available
        return cfg;
    }
}


/// <summary>
/// Receive endpoint configuration for worker services is in WorkerReceiveEndpointConfiguration.cs
/// This file consolidates consumer registration only.
/// </summary>
