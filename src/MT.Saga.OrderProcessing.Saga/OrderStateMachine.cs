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

    public Event<OrderCreated> OrderCreated { get; private set; } = null!;
    public Event<PaymentProcessed> PaymentProcessed { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;
    public Event<InventoryReserved> InventoryReserved { get; private set; } = null!;
    public Event<InventoryFailed> InventoryFailed { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderCreated, e => e.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentProcessed, e => e.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => PaymentFailed, e => e.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => InventoryReserved, e => e.CorrelateById(ctx => ctx.Message.OrderId));
        Event(() => InventoryFailed, e => e.CorrelateById(ctx => ctx.Message.OrderId));

        Initially(
            When(OrderCreated)
                .TransitionTo(PaymentProcessing)
                .Send(ctx => new ProcessPayment(ctx.Message.OrderId)));

        During(PaymentProcessing,
            When(PaymentProcessed)
                .TransitionTo(InventoryReserving)
                .Send(ctx => new ReserveInventory(ctx.Message.OrderId)),
            When(PaymentFailed)
                .TransitionTo(Cancelled)
                .Publish(ctx => new OrderCancelled(ctx.Message.OrderId))
                .Finalize());

        During(InventoryReserving,
            When(InventoryReserved)
                .TransitionTo(Confirmed)
                .Publish(ctx => new OrderConfirmed(ctx.Message.OrderId))
                .Finalize(),
            When(InventoryFailed)
                .TransitionTo(Cancelled)
                .Send(ctx => new RefundPayment(ctx.Message.OrderId))
                .Publish(ctx => new OrderCancelled(ctx.Message.OrderId))
                .Finalize());

        SetCompletedWhenFinalized();
    }
}
