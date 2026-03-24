namespace MT.Saga.OrderProcessing.Contracts.Commands;

public record ProcessPayment(Guid OrderId);
public record ReserveInventory(Guid OrderId);
public record RefundPayment(Guid OrderId);
public record CancelOrder(Guid OrderId);
