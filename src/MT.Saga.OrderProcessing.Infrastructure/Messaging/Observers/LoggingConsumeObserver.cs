using MassTransit;
using Microsoft.Extensions.Logging;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Observers;

public sealed class LoggingConsumeObserver(ILogger<LoggingConsumeObserver> logger) : IConsumeObserver
{
    public Task PreConsume<T>(ConsumeContext<T> context) where T : class
    {
        return Task.CompletedTask;
    }

    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        return Task.CompletedTask;
    }

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
    {
        logger.LogError(
            exception,
            "Message consume fault. MessageType={MessageType}; MessageId={MessageId}; CorrelationId={CorrelationId}; InputAddress={InputAddress}",
            typeof(T).Name,
            context.MessageId,
            context.CorrelationId,
            context.ReceiveContext.InputAddress);

        return Task.CompletedTask;
    }
}
