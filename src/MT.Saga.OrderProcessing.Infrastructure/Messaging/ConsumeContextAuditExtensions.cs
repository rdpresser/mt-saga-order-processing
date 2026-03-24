using MassTransit;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public static class ConsumeContextAuditExtensions
{
    public static string? ResolveCorrelationId<T>(this ConsumeContext<T> context)
        where T : class
    {
        return context.CorrelationId?.ToString()
            ?? context.ConversationId?.ToString()
            ?? context.RequestId?.ToString();
    }

    public static string? ResolveUserId<T>(this ConsumeContext<T> context)
        where T : class
    {
        if (context.Headers.TryGetHeader("user-id", out var userId) && userId is not null)
        {
            return userId.ToString();
        }

        if (context.Headers.TryGetHeader("x-user-id", out var xUserId) && xUserId is not null)
        {
            return xUserId.ToString();
        }

        return null;
    }

    public static bool ResolveIsAuthenticated<T>(this ConsumeContext<T> context)
        where T : class
    {
        if (TryReadBoolHeader(context, "is-authenticated", out var isAuthenticated))
        {
            return isAuthenticated;
        }

        if (TryReadBoolHeader(context, "x-is-authenticated", out isAuthenticated))
        {
            return isAuthenticated;
        }

        return false;
    }

    public static IDictionary<string, object> BuildAuditMetadata<T>(this ConsumeContext<T> context)
        where T : class
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["input-address"] = context.ReceiveContext.InputAddress?.ToString() ?? string.Empty,
            ["destination-address"] = context.DestinationAddress?.ToString() ?? string.Empty,
            ["source-address"] = context.SourceAddress?.ToString() ?? string.Empty,
            ["message-id"] = context.MessageId?.ToString() ?? string.Empty,
            ["conversation-id"] = context.ConversationId?.ToString() ?? string.Empty,
            ["correlation-id"] = context.CorrelationId?.ToString() ?? string.Empty,
            ["request-id"] = context.RequestId?.ToString() ?? string.Empty
        };

        if (context.Headers.TryGetHeader("MT-Redelivery-Count", out var redeliveryCount) && redeliveryCount is not null)
        {
            metadata["retry-attempt"] = redeliveryCount.ToString() ?? string.Empty;
        }

        if (context.Headers.TryGetHeader("retry-attempt", out var retryAttempt) && retryAttempt is not null)
        {
            metadata["retry-attempt"] = retryAttempt.ToString() ?? string.Empty;
        }

        if (context.Headers.TryGetHeader("queue", out var queue) && queue is not null)
        {
            metadata["queue"] = queue.ToString() ?? string.Empty;
        }

        return metadata;
    }

    private static bool TryReadBoolHeader<T>(ConsumeContext<T> context, string key, out bool value)
        where T : class
    {
        value = false;

        if (!context.Headers.TryGetHeader(key, out var headerValue) || headerValue is null)
        {
            return false;
        }

        if (headerValue is bool b)
        {
            value = b;
            return true;
        }

        return bool.TryParse(headerValue.ToString(), out value);
    }
}
