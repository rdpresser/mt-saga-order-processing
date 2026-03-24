using MassTransit;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;

namespace MT.Saga.OrderProcessing.Saga;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State PaymentProcessing { get; private set; } = null!;
    public State InventoryReserving { get; private set; } = null!;
    public State Confirmed { get; private set; } = null!;
    public State Cancelled { get; private set; } = null!;

    public Event<EventContext<OrderCreated>> OrderCreated { get; private set; } = null!;
    public Event<EventContext<PaymentProcessed>> PaymentProcessed { get; private set; } = null!;
    public Event<EventContext<PaymentFailed>> PaymentFailed { get; private set; } = null!;
    public Event<EventContext<InventoryReserved>> InventoryReserved { get; private set; } = null!;
    public Event<EventContext<InventoryFailed>> InventoryFailed { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated, e => e.CorrelateById(ctx => ctx.Message.Payload.OrderId));
        Event(() => PaymentProcessed, e => e.CorrelateById(ctx => ctx.Message.Payload.OrderId));
        Event(() => PaymentFailed, e => e.CorrelateById(ctx => ctx.Message.Payload.OrderId));
        Event(() => InventoryReserved, e => e.CorrelateById(ctx => ctx.Message.Payload.OrderId));
        Event(() => InventoryFailed, e => e.CorrelateById(ctx => ctx.Message.Payload.OrderId));

        Initially(
            When(OrderCreated)
                .TransitionTo(PaymentProcessing)
                .Send(ctx => EventContext.Create(
                    sourceService: "orders",
                    entity: "order",
                    action: "process-payment",
                    payload: new ProcessPayment(ctx.Message.Payload.OrderId),
                    correlationId: ctx.Message.CorrelationId,
                    causationId: ctx.Message.EventId.ToString(),
                    userId: ctx.Message.UserId,
                    isAuthenticated: ctx.Message.IsAuthenticated,
                    version: ctx.Message.Version,
                    metadata: ctx.Message.Metadata)));

        During(PaymentProcessing,
            When(PaymentProcessed)
                .TransitionTo(InventoryReserving)
                .Send(ctx => EventContext.Create(
                    sourceService: "orders",
                    entity: "order",
                    action: "reserve-inventory",
                    payload: new ReserveInventory(ctx.Message.Payload.OrderId),
                    correlationId: ctx.Message.CorrelationId,
                    causationId: ctx.Message.EventId.ToString(),
                    userId: ctx.Message.UserId,
                    isAuthenticated: ctx.Message.IsAuthenticated,
                    version: ctx.Message.Version,
                    metadata: ctx.Message.Metadata)),
            When(PaymentFailed)
                .TransitionTo(Cancelled)
                .Publish(ctx => EventContext.Create(
                    sourceService: "orders",
                    entity: "order",
                    action: "cancelled",
                    payload: new OrderCancelled(ctx.Message.Payload.OrderId)))
                .Finalize());

        During(InventoryReserving,
            When(InventoryReserved)
                .TransitionTo(Confirmed)
                .Publish(ctx => EventContext.Create(
                    sourceService: "orders",
                    entity: "order",
                    action: "confirmed",
                    payload: new OrderConfirmed(ctx.Message.Payload.OrderId)))
                .Finalize(),
            When(InventoryFailed)
                .TransitionTo(Cancelled)
                .Send(ctx => EventContext.Create(
                    sourceService: "orders",
                    entity: "order",
                    action: "refund-payment",
                    payload: new RefundPayment(ctx.Message.Payload.OrderId),
                    correlationId: ctx.Message.CorrelationId,
                    causationId: ctx.Message.EventId.ToString(),
                    userId: ctx.Message.UserId,
                    isAuthenticated: ctx.Message.IsAuthenticated,
                    version: ctx.Message.Version,
                    metadata: ctx.Message.Metadata))
                .Publish(ctx => EventContext.Create(
                    sourceService: "orders",
                    entity: "order",
                    action: "cancelled",
                    payload: new OrderCancelled(ctx.Message.Payload.OrderId)))
                .Finalize());

        SetCompletedWhenFinalized();
    }
}
