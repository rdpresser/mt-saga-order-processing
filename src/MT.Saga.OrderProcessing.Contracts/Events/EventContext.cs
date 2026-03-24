namespace MT.Saga.OrderProcessing.Contracts.Events;

public sealed record EventContext<TPayload>(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string SourceService,
    string Entity,
    string Action,
    TPayload Payload,
    string? CorrelationId = null,
    string? CausationId = null,
    string? UserId = null,
    bool IsAuthenticated = false,
    int Version = 1,
    IDictionary<string, object>? Metadata = null)
    where TPayload : class
{
    public string EventType => typeof(TPayload).Name;
    public string AggregateType => Entity;
}

public static class EventContext
{
    public static EventContext<TPayload> Create<TPayload>(
        string sourceService,
        string entity,
        string action,
        TPayload payload,
        string? correlationId = null,
        string? causationId = null,
        string? userId = null,
        bool isAuthenticated = false,
        int version = 1,
        IDictionary<string, object>? metadata = null)
        where TPayload : class
    {
        return new EventContext<TPayload>(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: DateTimeOffset.UtcNow,
            SourceService: sourceService,
            Entity: entity,
            Action: action,
            Payload: payload,
            CorrelationId: correlationId,
            CausationId: causationId,
            UserId: userId,
            IsAuthenticated: isAuthenticated,
            Version: version,
            Metadata: metadata);
    }
}
