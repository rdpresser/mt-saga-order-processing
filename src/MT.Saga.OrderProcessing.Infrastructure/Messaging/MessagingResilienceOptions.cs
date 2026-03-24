namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

public sealed class MessagingResilienceOptions
{
    public int PrefetchCount { get; set; } = 16;
    public int MaxRetryAttempts { get; set; } = 5;
    public int PublishMaxAttempts { get; set; } = 3;
    public int PublishRetryDelayMilliseconds { get; set; } = 200;
}
