using MassTransit;
using MassTransit.RabbitMqTransport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MT.Saga.OrderProcessing.Infrastructure.Persistence;

namespace MT.Saga.OrderProcessing.Infrastructure.Messaging.Configuration;

/// <summary>
/// Common MassTransit resilience and messaging policies.
/// Applies to all receive endpoints: retry, redelivery, outbox, kill switch.
/// </summary>
public static class CommonMassTransitPoliciesConfiguration
{
    /// <summary>
    /// Configuration options for MassTransit resilience policies.
    /// </summary>
    public sealed class MessagingPoliciesOptions
    {
        /// <summary>
        /// Prefetch count (RabbitMQ QoS).
        /// Default: 64 (MassTransit standard).
        /// Only lower if proven database bottleneck exists.
        /// </summary>
        public int PrefetchCount { get; set; } = 64;

        /// <summary>
        /// Application-level concurrent message limit.
        /// This is a subset of PrefetchCount and controls how many messages
        /// the consumer processes concurrently in-process.
        /// Default: 20 (conservative, DB-friendly).
        /// </summary>
        public int ConcurrentMessageLimit { get; set; } = 20;

        /// <summary>
        /// Maximum number of retry attempts for transient errors.
        /// Uses exponential backoff: 1s → 30s with 5s delta.
        /// Default: 5
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 5;

        /// <summary>
        /// Kill switch activation threshold (failed messages count).
        /// After this many failures, endpoint pauses.
        /// Default: 10
        /// </summary>
        public int KillSwitchActivationThreshold { get; set; } = 10;

        /// <summary>
        /// Kill switch trip threshold (percentage of failures).
        /// Default: 15% (0.15)
        /// </summary>
        public double KillSwitchTripThreshold { get; set; } = 0.15;

        /// <summary>
        /// Kill switch restart timeout (how long to pause before resuming).
        /// Default: 1 minute
        /// </summary>
        public TimeSpan KillSwitchRestartTimeout { get; set; } = TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Applies common resilience policies to a receive endpoint.
    /// Must be called for each receive endpoint to ensure consistency.
    ///
    /// Policies applied:
    /// - Retry (exponential backoff)
    /// - Kill switch (protects system under sustained failures)
    /// - Outbox (ensures reliable message publishing)
    /// </summary>
    public static void ConfigureCommonReceiveEndpointPolicies(
        this IRabbitMqReceiveEndpointConfigurator endpoint,
        IRegistrationContext context,
        MessagingPoliciesOptions options)
    {
        // RabbitMQ QoS: Prefetch count
        // Rationale: Default 64 balances throughput and memory.
        // Only lower if database is proven bottleneck.
        // Use ConcurrentMessageLimit for app-level control.
        endpoint.PrefetchCount = (ushort)Math.Max(1, options.PrefetchCount);

        // Application-level concurrency limit (subset of prefetch)
        // Protects database from concurrent worker threads
        endpoint.ConcurrentMessageLimit = Math.Max(1, options.ConcurrentMessageLimit);

        // Retry policy: Exponential backoff
        // Handles transient errors (deadlocks, timeouts)
        endpoint.UseMessageRetry(r =>
        {
            r.Exponential(
                retryLimit: Math.Max(1, options.MaxRetryAttempts),
                minInterval: TimeSpan.FromSeconds(1),
                maxInterval: TimeSpan.FromSeconds(30),
                intervalDelta: TimeSpan.FromSeconds(5));
        });

        // Kill switch: Protects system under sustained failures
        // Activates after N consecutive failures or X% failure rate
        // Pauses endpoint for configured restart timeout
        endpoint.UseKillSwitch(killSwitch =>
        {
            killSwitch.SetActivationThreshold(
                Math.Max(1, options.KillSwitchActivationThreshold));
            killSwitch.SetTripThreshold(
                Math.Max(0.01, Math.Min(1.0, options.KillSwitchTripThreshold)));
            killSwitch.SetRestartTimeout(
                options.KillSwitchRestartTimeout);
        });

        // Outbox: Ensures reliable message publishing
        // Buffers messages until consumer completes; discards on failure
        // Prevents duplicate publishes on retry
        endpoint.UseEntityFrameworkOutbox<OrderSagaDbContext>(context);
    }
}
