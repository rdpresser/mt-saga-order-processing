namespace MT.Saga.OrderProcessing.OrderService.Features.Orders.GetOrderById;

public sealed record GetOrderByIdResponse(Guid OrderId, string Status);
