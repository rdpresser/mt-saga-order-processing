namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public static class TopicRoutingKeyHelper
{
    public static string GenerateRoutingKey(string sourceService, string entity, string action)
    {
        if (string.IsNullOrWhiteSpace(sourceService)) throw new ArgumentException("Source service cannot be empty.", nameof(sourceService));
        if (string.IsNullOrWhiteSpace(entity)) throw new ArgumentException("Entity cannot be empty.", nameof(entity));
        if (string.IsNullOrWhiteSpace(action)) throw new ArgumentException("Action cannot be empty.", nameof(action));

        return $"{sourceService}.{entity}.{action}".ToLowerInvariant();
    }

    public static string GenerateWildcardBindingKey(string sourceService, string entity)
    {
        if (string.IsNullOrWhiteSpace(sourceService)) throw new ArgumentException("Source service cannot be empty.", nameof(sourceService));
        if (string.IsNullOrWhiteSpace(entity)) throw new ArgumentException("Entity cannot be empty.", nameof(entity));

        return $"{sourceService}.{entity}.*".ToLowerInvariant();
    }
}
