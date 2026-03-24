namespace MT.Saga.OrderProcessing.Contracts.Events;

public record OrderCreated(Guid OrderId);
public record PaymentProcessed(Guid OrderId);
public record PaymentFailed(Guid OrderId);
public record InventoryReserved(Guid OrderId);
public record InventoryFailed(Guid OrderId);
public record OrderConfirmed(Guid OrderId);
public record OrderCancelled(Guid OrderId);
