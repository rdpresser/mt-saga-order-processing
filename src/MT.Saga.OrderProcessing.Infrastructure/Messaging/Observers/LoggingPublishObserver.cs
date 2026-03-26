using MassTransit;
using Microsoft.Extensions.Logging;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Observers;

public sealed class LoggingPublishObserver(ILogger<LoggingPublishObserver> logger) : IPublishObserver
{
    public Task PrePublish<T>(PublishContext<T> context) where T : class
    {
        return Task.CompletedTask;
    }

    public Task PostPublish<T>(PublishContext<T> context) where T : class
    {
        return Task.CompletedTask;
    }

    public Task PublishFault<T>(PublishContext<T> context, Exception exception) where T : class
    {
        logger.LogError(
            exception,
            "Publish fault. MessageType={MessageType}; MessageId={MessageId}; CorrelationId={CorrelationId}; Destination={DestinationAddress}",
            typeof(T).Name,
            context.MessageId,
            context.CorrelationId,
            context.DestinationAddress);

        return Task.CompletedTask;
    }
}
