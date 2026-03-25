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
    /// </summary>
    public static IRegistrationConfigurator AddPaymentServiceConsumers(
        this IRegistrationConfigurator cfg)
    {
        // Placeholder for future: Register ProcessPaymentConsumer and RefundPaymentConsumer when available
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
