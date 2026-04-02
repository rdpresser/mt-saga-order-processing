namespace MT.Saga.OrderProcessing.Infrastructure.Messaging;

/// <summary>
/// Centralized messaging resilience and concurrency configuration.
/// Binds from appsettings.json section "Messaging:Resilience".
/// Applied to all consumers via ConsumerDefinition classes and to publish operations.
/// </summary>
public sealed class MessagingResilienceOptions
{
    /// <summary>
    /// Maximum number of messages to prefetch from the broker per consumer.
    /// Balances throughput vs memory usage. Higher = more throughput but higher memory.
    /// Default: 16. Recommended: 8-64 depending on message size and processing cost.
    /// </summary>
    public int PrefetchCount { get; set; } = 16;

    /// <summary>
    /// Maximum number of concurrent messages processed by a single consumer endpoint.
    /// Limits parallel processing and prevents overload of downstream services.
    /// Default: 20. Recommended: 8-32 depending on resource constraints.
    /// </summary>
    public int ConcurrentMessageLimit { get; set; } = 20;

    /// <summary>
    /// Maximum number of retry attempts for consumer message handling.
    /// Applied to exponential backoff retry policy in consumer definitions.
    /// Default: 5. Recommended: 3-10 depending on failure expected likelihood.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Maximum number of retry attempts for publish operations.
    /// Applied when publishing events from consumers (lower tolerance than consumption).
    /// Default: 3. Recommended: 1-5 to fail fast on publish failures.
    /// </summary>
    public int PublishMaxAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds for publish operation retries.
    /// Used as base for exponential backoff: delay * (attempt_number).
    /// Default: 200ms. Recommended: 100-500ms.
    /// </summary>
    public int PublishRetryDelayMilliseconds { get; set; } = 200;

    /// <summary>
    /// Kill-switch activation threshold: number of consecutive failures before activation.
    /// When threshold is reached, the endpoint stops accepting messages for RestartTimeout duration.
    /// Default: 10. Recommended: 5-20 depending on expected transient failure rate.
    /// </summary>
    public int KillSwitchActivationThreshold { get; set; } = 10;

    /// <summary>
    /// Kill-switch trip threshold: failure ratio (0.0-1.0) to activate the kill-switch.
    /// Example: 0.15 = activate when 15% of messages fail in the sample window.
    /// Default: 0.15 (15%). Recommended: 0.10-0.25 depending on tolerance.
    /// </summary>
    public double KillSwitchTripThreshold { get; set; } = 0.15;

    /// <summary>
    /// Duration to keep the kill-switch active before attempting restart.
    /// During this period, messages are not processed (backpressure applied at broker).
    /// Default: 1 minute. Recommended: 30 seconds - 5 minutes.
    /// </summary>
    public TimeSpan KillSwitchRestartTimeout { get; set; } = TimeSpan.FromMinutes(1);
}
