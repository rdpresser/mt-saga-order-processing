using MassTransit;
using MT.Saga.OrderProcessing.Contracts.Commands;
using MT.Saga.OrderProcessing.Contracts.Events;

namespace MT.Saga.OrderProcessing.PaymentService.Consumers;

public sealed class RefundPaymentConsumer(ILogger<RefundPaymentConsumer> logger) : IConsumer<EventContext<RefundPayment>>
{
    public Task Consume(ConsumeContext<EventContext<RefundPayment>> context)
    {
        logger.LogWarning(
            "Refund executed for OrderId: {OrderId}. ConversationId: {ConversationId}; CorrelationId: {CorrelationId}",
            context.Message.Payload.OrderId,
            context.ConversationId,
            context.CorrelationId);
        return Task.CompletedTask;
    }
}
