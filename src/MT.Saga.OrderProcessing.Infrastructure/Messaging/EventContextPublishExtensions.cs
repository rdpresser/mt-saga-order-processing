using MassTransit;
using Microsoft.Extensions.Logging;
using MT.Saga.OrderProcessing.Contracts.Events;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public static class EventContextPublishExtensions
{
    public static async Task PublishEventContextWithRetryAsync<TPayload>(
        this IPublishEndpoint publishEndpoint,
        EventContext<TPayload> eventContext,
        ILogger logger,
        MessagingResilienceOptions resilienceOptions,
        CancellationToken cancellationToken = default)
        where TPayload : class
    {
        ArgumentNullException.ThrowIfNull(publishEndpoint);
        ArgumentNullException.ThrowIfNull(eventContext);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(resilienceOptions);

        var routingKey = TopicRoutingKeyHelper.GenerateRoutingKey(
            eventContext.SourceService,
            eventContext.Entity,
            eventContext.Action);

        var attempts = Math.Max(1, resilienceOptions.PublishMaxAttempts);
        var retryDelay = TimeSpan.FromMilliseconds(Math.Max(50, resilienceOptions.PublishRetryDelayMilliseconds));

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await publishEndpoint
                    .Publish(eventContext, context =>
                    {
                        if (context is RabbitMqSendContext rabbitMqSendContext)
                        {
                            rabbitMqSendContext.RoutingKey = routingKey;
                        }
                    }, cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < attempts)
            {
                logger.LogWarning(
                    ex,
                    "Publish attempt {Attempt}/{MaxAttempts} failed for {EventType} with routing key {RoutingKey}. Retrying.",
                    attempt,
                    attempts,
                    typeof(TPayload).Name,
                    routingKey);

                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Publish failed after {MaxAttempts} attempts for {EventType} with routing key {RoutingKey}.",
                    attempts,
                    typeof(TPayload).Name,
                    routingKey);
                throw new InvalidOperationException(
                    $"Failed to publish event context for '{typeof(TPayload).Name}' after {attempts} attempts using routing key '{routingKey}'.",
                    ex);
            }
        }
    }
}
