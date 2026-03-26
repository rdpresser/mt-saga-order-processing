namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrders;

public sealed record GetOrdersResponse(Guid OrderId, string Status, DateTime CreatedAt, DateTime? UpdatedAt);
